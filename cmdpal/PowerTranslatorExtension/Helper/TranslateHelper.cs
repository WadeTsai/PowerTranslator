using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PowerTranslatorExtension.Protocol;
using PowerTranslatorExtension.Utils;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
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
    // Recent successful results, so typing the same text again does not send another request.
    private const int maxCachedResults = 256;
    private readonly ConcurrentDictionary<(string src, string toLan), ITranslateResult> resultCache = new();
    private Task<bool>? initTask;
    private long lastInitTime;
    private List<ITranslator> translators;
    private readonly Func<bool> isCodeModeEnabled;
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
                // English words / short phrases: offline ECDICT (Traditional Chinese) + dictionaryapi.dev (English).
                new Service.Dictionary.WordLookupTranslator(() => SettingsManager.Instance.EnableEnglishDefinitions),
                // Sentences and everything the dictionaries cannot answer.
                new Service.DeepL.DeepLTranslator(),
                // Fallback when DeepL is rate-limited or unreachable.
                new Service.Google.GoogleTranslator(),
            ],
            defaultLanguageKey,
            () => SettingsManager.Instance.EnableCodeMode)
    {
        UtilsFun.onHttpDefaultHandlerChange += this.Reload;
    }

    internal TranslateHelper(
        IEnumerable<ITranslator> translators,
        string defaultLanguageKey,
        Func<bool> isCodeModeEnabled)
    {
        ArgumentNullException.ThrowIfNull(translators);
        ArgumentNullException.ThrowIfNull(isCodeModeEnabled);

        this.translators = translators.ToList();
        this.defaultLanguageKey = defaultLanguageKey;
        this.isCodeModeEnabled = isCodeModeEnabled;
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
    /// <summary>Tries each translator in order; failure reasons are collected into <paramref name="errors"/>.</summary>
    private ITranslateResult? Translate(string text, string toLan, List<string> errors)
    {
        if (resultCache.TryGetValue((text, toLan), out var cached))
            return cached;

        ITranslateResult? result;
        translateLock.EnterReadLock();
        try
        {
            result = translators.FirstNotNoneCast((it) =>
            {
                try
                {
                    return it.Translate(text, toLan, "auto");
                }
                catch (Exception err)
                {
                    UtilsFun.LogMessage($"{it.Name}: {err.Message}");
                    errors.Add($"{it.Name}: {err.Message}");
                    return null;
                }
            });
        }
        finally
        {
            translateLock.ExitReadLock();
        }

        if (result != null)
        {
            if (resultCache.Count >= maxCachedResults)
                resultCache.Clear();
            resultCache[(text, toLan)] = result;
        }
        return result;
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
        string toLan = toLanguage != null ? cultureAliasHelper.GetCultureFromAlias(toLanguage) : target.toLan;
        var errors = new List<string>();
        ITranslateResult? translateResult = Translate(src, toLan, errors);

        if (translateResult != null)
        {
            var tres = translateResult!.Transform();
            if (tres != null)
            {
                foreach (var item in tres)
                {
                    item.DictWord ??= DictionaryWord(src, item.Title);
                    res.Add(item);
                }
            }
        }
        else
        {
            res.Add(new ResultItem
            {
                Title = errors.Count > 0 ? Loc.Get("Status_TranslateFailed_Title") : Loc.Get("Status_TranslateError_Title"),
                SubTitle = errors.Count > 0 ? string.Join(" · ", errors) : Loc.Get("Status_TranslateError_Subtitle"),
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
        resultCache.Clear();
        Task.Factory.StartNew(() =>
        {
            translateLock.EnterWriteLock();
            try
            {
                foreach (var translator in translators)
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

    /// <summary>
    /// Cambridge and Oxford are English dictionaries, so jump with the English side of the translation:
    /// the typed word, or the translation when the input is Chinese / Japanese / Korean.
    /// </summary>
    public static string DictionaryWord(string src, string translated) =>
        Service.DeepL.DeepLTranslator.ContainsCjk(src) ? translated : src;

    /// <summary>Reads the text aloud with the built-in Windows speech synthesizer.</summary>
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
                using var synthesizer = new SpeechSynthesizer();
                if (PickVoice(txt) is { } voice)
                    synthesizer.Voice = voice;
                var stream = synthesizer.SynthesizeTextToStreamAsync(txt).AsTask().GetAwaiter().GetResult();
                player = new MediaPlayer
                {
                    Source = MediaSource.CreateFromStream(stream, stream.ContentType),
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
                ended.Wait(TimeSpan.FromSeconds(30));
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

    // Prefer a Taiwanese voice for Chinese text and an English voice otherwise; fall back to the system default.
    private static VoiceInformation? PickVoice(string text)
    {
        string[] preferred = Service.DeepL.DeepLTranslator.ContainsCjk(text)
            ? ["zh-TW", "zh-HK", "zh"]
            : ["en-US", "en"];
        var voices = SpeechSynthesizer.AllVoices;
        foreach (var lang in preferred)
        {
            var voice = voices.FirstOrDefault(v => v.Language.StartsWith(lang, StringComparison.OrdinalIgnoreCase));
            if (voice != null)
                return voice;
        }
        return null;
    }
}
