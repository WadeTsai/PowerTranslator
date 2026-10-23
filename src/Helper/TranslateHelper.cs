using System.Linq;
using Wox.Plugin;
using Wox.Plugin.Logger;
using Translator.Utils;
using Translator.Protocol;
namespace Translator;

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
    public bool inited => this.translators.Any(t => t != null);
    private object initLock = new Object();
    private long lastInitTime = 0;
    private IPublicAPI publicAPI;
    private List<ITranslator?> translators;
    private List<(string name, Func<ITranslator> create)> translatorGenerators;
    private bool isSpeaking = false;
    private bool isIniting = false;
    public string defaultLanguageKey = "auto";
    private Middleware.Alias.CultureAliasHelper cultureAliasHelper = new(
        new Dictionary<string, string>{
            { "zhs", "zh-Hans" },
            { "zht", "zh-Hant" },
        }
    );
    public TranslateHelper(IPublicAPI publicAPI, string defaultLanguageKey = "auto")
    {
        translatorGenerators = new List<(string, Func<ITranslator>)>{
            // English words / short phrases: offline ECDICT (Traditional Chinese) + dictionaryapi.dev (English).
            ("ECDICT", () => new Service.Dictionary.WordLookupTranslator(() => SettingHelper.Instance.enableEnglishDefinitions)),
            // Sentences and everything the dictionaries cannot answer.
            ("DeepL", () => new Service.DeepL.DeepLTranslator()),
        };
        this.translators = translatorGenerators.Select(_ => (ITranslator?)null).ToList();
        // make sure do it before init translator
        UtilsFun.ChangeDefaultHttpHandlerProxy(SettingHelper.Instance.useSystemProxy, false);

        this.InitTranslator();
        this.publicAPI = publicAPI;
        this.defaultLanguageKey = defaultLanguageKey;

        UtilsFun.onHttpDefaultHandlerChange += this.Reload;
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
        if (SettingHelper.Instance.enableCodeMode)
        {
            _src = UtilsFun.ConvertSnakeCaseOrCamelCaseToNormalSpace(_src);
        }

        return new TranslateTarget
        {
            src = _src,
            toLan = cultureAliasHelper.GetCultureFromAlias(_toLan)
        };
    }
    private ITranslateResult? Translate(string text, string toLan)
    {
        return translators.Enumerate().FirstNotNoneCast((data) =>
        {
            var (idx, it) = data;
            try
            {
                if (it == null)
                {

                    throw new Exception($"{this.translatorGenerators[idx].name} is null");
                }
                return it?.Translate(text, toLan, "auto");
            }
            catch (Exception err)
            {
                Log.Error(err.Message, typeof(TranslateHelper));
                return null;
            }
        });
    }
    public List<ResultItem> QueryTranslate(string raw, string translateFrom = "user input", string? toLanguage = null)
    {
        var res = new List<ResultItem>();
        if (raw.Length == 0)
            return res;

        if (!inited)
        {
            if (!this.isIniting)
            {
                Task.Factory.StartNew(() =>
                {
                    if (this.InitTranslator())
                        this.publicAPI?.ChangeQuery(raw, true);
                });
            }
            res.Add(new ResultItem
            {
                Title = "initializing other apis...",
                SubTitle = "please try later"
            });
            return res;
        }

        var target = ParseRawSrc(raw);
        string src = target.src;
        string toLan = toLanguage ?? target.toLan;
        ITranslateResult? translateResult = Translate(src, toLan);
        var transformedResults = translateResult?.Transform()?.ToList();

        if (transformedResults is { Count: > 0 })
        {
            res.AddRange(transformedResults);
        }
        else
        {
            var hasExplicitTargetLanguage = !string.Equals(toLan, "auto", StringComparison.OrdinalIgnoreCase);

            res.Add(new ResultItem
            {
                Title = hasExplicitTargetLanguage
                    ? "No translation returned"
                    : "Translation failed. Check your network connection.",
                SubTitle = hasExplicitTargetLanguage
                    ? "The input may already be in the target language, or the translation service may be unavailable."
                    : "Press Enter to get help",
                Action = (ev) =>
                {
                    UtilsFun.SetClipboardText("https://github.com/N0I0C0K/PowerTranslator/issues?q=");
                    this.publicAPI.ShowMsg("Copy!", "The URL has been copied, Go to your browser and visit the website for help.");
                    return true;
                }
            });
        }

        return res;
    }

    /// <summary>Reads the text aloud with the built-in Windows speech engine (SAPI).</summary>
    public void Read(string? txt)
    {
        if (isSpeaking || txt == null || txt.Length == 0)
            return;
        var thread = new Thread(() =>
        {
            this.isSpeaking = true;
            try
            {
                var type = Type.GetTypeFromProgID("SAPI.SpVoice");
                if (type == null)
                    return;
                dynamic voice = Activator.CreateInstance(type)!;
                // Prefer a Traditional Chinese (zh-TW, LCID 0x404) voice for Chinese text, an English one otherwise.
                var language = Service.DeepL.DeepLTranslator.ContainsCjk(txt) ? "404" : "409";
                dynamic tokens = voice.GetVoices($"Language={language}", "");
                if (tokens.Count > 0)
                    voice.Voice = tokens.Item(0);
                voice.Speak(txt, 0);
            }
            catch (Exception err)
            {
                Log.Warn($"Read failed: {err.Message}", typeof(TranslateHelper));
            }
            finally
            {
                this.isSpeaking = false;
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public bool InitTranslator()
    {
        var now = UtilsFun.GetUtcTimeNow();
        if (now - this.lastInitTime < 1000 * 30 || this.inited || this.isIniting)
            return false;
        lock (this.initLock)
        {
            this.isIniting = true;
            this.lastInitTime = now;
            Log.Info("custom init", typeof(TranslateHelper));

            var actions = translatorGenerators.Select((tp, idx) =>
            {
                return Task.Factory.StartNew(() =>
                {
                    Log.Info($"start init {tp.name} {idx}", typeof(TranslateHelper));
                    if (translators[idx] != null)
                        return;
                    try
                    {
                        this.translators[idx] = tp.create();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"{idx} Error occurred: {(ex.InnerException ?? ex).Message}", typeof(Translator));
                    }
                });
            });

            try
            {
                Task.WhenAll(actions).GetAwaiter().GetResult();
            }
            catch (AggregateException ex)
            {
                // 捕獲並處理 Task.WhenAll 返回的聚合異常
                foreach (Exception innerException in ex.InnerExceptions)
                {
                    Log.Error($"Error occurred during task execution: {innerException.Message}", typeof(Translator));
                }
                return false;
            }
            catch (Exception err)
            {
                Log.Warn(err.Message, typeof(Translator));
                return false;
            }
            finally
            {
                this.isIniting = false;
                Log.Info("custom init complete", typeof(TranslateHelper));
            }
            return true;
        }
    }

    public void Reload()
    {
        foreach (var translator in translators)
        {
            Task.Factory.StartNew(() =>
            {
                translator?.Reset();
            });
        }
    }
}
