using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PowerTranslatorExtension.Protocol;
using PowerTranslatorExtension.Utils;
using Windows.Media.Core;
using Windows.Media.Playback;
namespace PowerTranslatorExtension;

public class TranslateFailedException : Exception
{
    public TranslateFailedException() : base("translate failed")
    {

    }
}

public class TranslateHelper
{
    public struct TranslateTarget
    {
        public string src;
        public string toLan;
    }
    public const string toLanSplit = "->";
    // Usable as soon as any translator is ready; the others keep retrying in the background.
    public bool inited => this.translators.Any(t => t.Inited);
    private bool allInited => this.translators.All(t => t.Inited);
    // How long a query waits for an in-flight initialization before showing the "initializing" placeholder.
    private static readonly TimeSpan initWaitTimeout = TimeSpan.FromSeconds(5);
    private readonly object initLock = new();
    // Translations share the read lock so the main and second-language queries run concurrently;
    // Reload takes the write lock so no request uses a client while it is being replaced.
    private readonly ReaderWriterLockSlim translateLock = new();
    private Task<bool>? initTask;
    private long lastInitTime;
    private List<ITranslator> translators;
    private readonly Func<bool> isCodeModeEnabled;
    private readonly ITranslator? cambridgeTranslator;
    private readonly Func<bool> isCambridgeEnabled;
    private bool isSpeaking;
    public string defaultLanguageKey = "auto";
    private Middleware.Alias.CultureAliasMiddleware cultureAliasHelper = new(
        new Dictionary<string, string>{
            { "zhs", "zh-Hans" },
            { "zht", "zh-Hant" },
        }
    );
    public TranslateHelper(string defaultLanguageKey = "auto")
        : this(
            [
                new Service.Youdao.V2.YoudaoTranslator(),
                new Service.Youdao.Old.YoudaoTranslator(),
                new Service.DeepL.DeepLTranslator(),
                new Service.Youdao.Backup.BackUpTranslator(),
            ],
            defaultLanguageKey,
            () => SettingsManager.Instance.EnableCodeMode,
            new Service.Cambridge.CambridgeTranslator(),
            () => SettingsManager.Instance.UseCambridgeTraditional)
    {
        UtilsFun.onHttpDefaultHandlerChange += this.Reload;
    }

    internal TranslateHelper(
        IEnumerable<ITranslator> translators,
        string defaultLanguageKey,
        Func<bool> isCodeModeEnabled,
        ITranslator? cambridgeTranslator = null,
        Func<bool>? isCambridgeEnabled = null)
    {
        ArgumentNullException.ThrowIfNull(translators);
        ArgumentNullException.ThrowIfNull(isCodeModeEnabled);

        this.translators = translators.ToList();
        this.defaultLanguageKey = defaultLanguageKey;
        this.isCodeModeEnabled = isCodeModeEnabled;
        this.cambridgeTranslator = cambridgeTranslator;
        this.isCambridgeEnabled = isCambridgeEnabled ?? (() => false);
    }

    public TranslateTarget ParseRawSrc(string src)
    {
        string _src, _toLan;

        if (src.Contains(toLanSplit))
        {
            var srcArr = src.Split(toLanSplit);
            _src = srcArr.First().TrimEnd().TrimStart();
            _toLan = srcArr.Last().TrimEnd().TrimStart();
        }
        else
        {
            _src = src;
            _toLan = this.defaultLanguageKey;
        }
        if (isCodeModeEnabled())
        {
            _src = UtilsFun.ConvertSnakeCaseOrCamelCaseToNormalSpace(_src);
        }

        return new TranslateTarget
        {
            src = _src,
            toLan = cultureAliasHelper.GetCultureFromAlias(_toLan)
        };
    }
    private ITranslateResult? TranslateWithCambridge(string text, string toLan)
    {
        if (cambridgeTranslator == null || !isCambridgeEnabled())
            return null;
        try
        {
            return cambridgeTranslator.Translate(text, toLan, "en");
        }
        catch (Exception err)
        {
            UtilsFun.LogMessage($"{cambridgeTranslator.Name}: {err.Message}");
            return null;
        }
    }
    private ITranslateResult? Translate(string text, string toLan)
    {
        translateLock.EnterReadLock();
        try
        {
            var cambridgeResult = TranslateWithCambridge(text, toLan);
            if (cambridgeResult != null)
                return cambridgeResult;
            return translators.FirstNotNoneCast((it) =>
            {
                try
                {
                    return it.Translate(text, toLan, "auto");
                }
                catch (Exception err)
                {
                    UtilsFun.LogMessage($"{it.Name}: {err.Message}");
                    return null;
                }
            });
        }
        finally
        {
            translateLock.ExitReadLock();
        }
    }
    public List<ResultItem> QueryTranslate(string raw, string? toLanguage = null)
    {
        var res = new List<ResultItem>();
        if (raw.Length == 0)
            return res;

        if (!allInited)
        {
            var init = WarmUp();
            if (!inited)
                init.Wait(initWaitTimeout);
        }
        if (!inited)
        {
            res.Add(new ResultItem
            {
                Title = Loc.Get("Status_Initializing_Title"),
                SubTitle = Loc.Get("Status_Initializing_Subtitle")
            });
            return res;
        }

        var target = ParseRawSrc(raw);
        string src = target.src;
        string toLan = toLanguage ?? target.toLan;
        ITranslateResult? translateResult = Translate(src, toLan);

        if (translateResult != null)
        {
            var tres = translateResult!.Transform();
            if (tres != null)
                res.AddRange(tres);
        }
        else
        {
            res.Add(new ResultItem
            {
                Title = Loc.Get("Status_TranslateError_Title"),
                SubTitle = Loc.Get("Status_TranslateError_Subtitle"),
                Action = () =>
                {
                    UtilsFun.SetClipboardText("https://github.com/N0I0C0K/PowerTranslator/issues?q=");
                    //this.publicAPI.ShowMsg("Copy!", "The URL has been copied, Go to your browser and visit the website for help.");
                    return true;
                }
            });
        }

        return res;
    }
    /// <summary>
    /// Starts initializing the translators in the background (or returns the in-flight attempt).
    /// Called at startup so the first query does not pay for the key exchange.
    /// </summary>
    public Task<bool> WarmUp()
    {
        lock (initLock)
        {
            if (allInited)
                return Task.FromResult(true);
            if (initTask is { IsCompleted: false })
                return initTask;
            // Retry failed initializations at most every 30 seconds.
            var now = UtilsFun.GetUtcTimeNow();
            if (initTask != null && now - lastInitTime < 1000 * 30)
                return initTask;
            lastInitTime = now;
            initTask = Task.Run(InitTranslators);
            return initTask;
        }
    }

    private bool InitTranslators()
    {
        UtilsFun.LogMessage("custom init");
        var actions = translators.Where(t => !t.Inited).Select(translator => Task.Run(() =>
        {
            try
            {
                UtilsFun.LogMessage($"start init {translator.Name}");
                translator.Init();
                UtilsFun.LogMessage($"init {translator.Name} success: {translator.Inited}");
            }
            catch (Exception ex)
            {
                UtilsFun.LogMessage($"Init {translator.Name} Error occurred: {(ex.InnerException ?? ex).Message}");
            }
        })).ToArray();
        Task.WaitAll(actions);
        UtilsFun.LogMessage("custom init complete");
        return inited;
    }

    public void Reload()
    {
        Task.Factory.StartNew(() =>
        {
            translateLock.EnterWriteLock();
            try
            {
                foreach (var translator in cambridgeTranslator == null ? translators : translators.Append(cambridgeTranslator))
                {
                    try
                    {
                        translator.Reset();
                    }
                    catch (Exception ex)
                    {
                        UtilsFun.LogMessage($"Reset {translator.Name} failed: {ex.Message}");
                    }
                }
            }
            finally
            {
                translateLock.ExitWriteLock();
            }
        });
    }

    public void Read(string? txt)
    {
        if (isSpeaking || string.IsNullOrEmpty(txt))
            return;
        Task.Factory.StartNew(() =>
        {
            isSpeaking = true;
            MediaPlayer? player = null;
            try
            {
                var uri = new Uri($"https://dict.youdao.com/dictvoice?audio={Uri.EscapeDataString(txt.removeSpecialCharacter())}&le=zh");
                uri.fixChinese();
                player = new MediaPlayer
                {
                    Source = MediaSource.CreateFromUri(uri),
                    Volume = 1.0,
                };
                var ended = new ManualResetEventSlim(false);
                player.MediaEnded += (s, e) => ended.Set();
                player.MediaFailed += (s, e) =>
                {
                    UtilsFun.LogMessage($"TTS failed: {e.ErrorMessage}");
                    ended.Set();
                };
                player.Play();
                ended.Wait(TimeSpan.FromSeconds(15));
            }
            catch (Exception ex)
            {
                UtilsFun.LogMessage($"Read failed: {ex.Message}");
            }
            finally
            {
                player?.Dispose();
                isSpeaking = false;
            }
        });
    }
}
