using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Translator.Protocol;
using Translator.Utils;


namespace Translator.Service.DeepL;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Wox.Plugin.Logger;

public class DeepLTranslateResult : ITranslateResult
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("result")]
    public _Result? Result { get; set; }

    public bool IsSuccess()
    {
        return Result != null;
    }

    /// <summary>Guarantees Traditional characters even if DeepL ignores the regional variant.</summary>
    public void ConvertToTraditional()
    {
        foreach (var translation in Result?.Translations ?? [])
            foreach (var beam in translation.Beams)
                foreach (var sentence in beam.Sentences)
                    sentence.Text = ChineseConverter.ToTraditional(sentence.Text);
    }

    public override IEnumerable<ResultItem>? Transform()
    {
        if (!IsSuccess())
            return null;
        List<ResultItem> res = new List<ResultItem>();
        foreach (var translation in this.Result!.Translations)
        {
            foreach (var beam in translation.Beams)
            {
                foreach (var sentence in beam.Sentences)
                {
                    res.Add(new ResultItem
                    {
                        Title = sentence.Text,
                        SubTitle = $"{this.Result!.SourceLang} -> {this.Result!.TargetLang}",
                        CopyTgt = sentence.Text,
                        fromApiName = "DeepL",
                    });
                }
            }
        }
        return res;
    }

    public class _Result
    {
        [JsonPropertyName("source_lang")]
        public required string SourceLang { get; set; }

        [JsonPropertyName("target_lang")]
        public required string TargetLang { get; set; }

        [JsonPropertyName("source_lang_is_confident")]
        public bool SourceLangIsConfident { get; set; }

        [JsonPropertyName("translations")]
        public required Translation[] Translations { get; set; }
    }

    public class Sentence
    {
        [JsonPropertyName("text")]
        public required string Text { get; set; }
    }

    public class Beams
    {
        [JsonPropertyName("num_symbols")]
        public int NumSymbols { get; set; }

        [JsonPropertyName("sentences")]
        public required Sentence[] Sentences { get; set; }
    }

    public class Translation
    {
        [JsonPropertyName("beams")]
        public required Beams[] Beams { get; set; }

        [JsonPropertyName("quality")]
        public string? Quality { get; set; }
    }
}


public class DeepLTranslateRequestBody
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("method")]
    public string Method { get; set; } = "LMT_handle_jobs";

    [JsonPropertyName("params")]
    public Params Params { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    public DeepLTranslateRequestBody(Params _params)
    {
        this.Params = _params;
        this.Id = (int)(_params.Timestamp % 100000000);
    }
}

public class Params
{
    public class Job
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "default";

        [JsonPropertyName("sentences")]
        public List<Sentence> Sentences { get; set; }

        [JsonPropertyName("preferred_num_beams")]
        public int PreferredNumBeams { get; set; } = 4;
        public Job(string text)
        {
            this.Sentences = new List<Sentence>{
                new Sentence{
                    Id = 1,
                    Prefix = "",
                    Text = text
                }
            };
        }
    }
    public class Sentence
    {
        [JsonPropertyName("text")]
        public required string Text { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("prefix")]
        public string? Prefix { get; set; }
    }
    public class Language
    {
        [JsonPropertyName("target_lang")]
        public required string TargetLang { get; set; }

        [JsonPropertyName("source_lang_computed")]
        public string? SourceLangComputed { get; set; }
    }
    public class CommonJobParams
    {
        [JsonPropertyName("quality")]
        public required string Quality { get; set; }

        [JsonPropertyName("mode")]
        public required string Mode { get; set; }

        [JsonPropertyName("browserType")]
        public int BrowserType { get; set; }

        [JsonPropertyName("textType")]
        public required string TextType { get; set; }

        // e.g. "zh-Hant" / "zh-Hans"; the web client sends it for regional target variants.
        [JsonPropertyName("regionalVariant")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RegionalVariant { get; set; }
    }

    [JsonPropertyName("jobs")]
    public List<Job> Jobs { get; set; }

    [JsonPropertyName("lang")]
    public Language Lang { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = -1;

    [JsonPropertyName("commonJobParams")]
    public CommonJobParams commonJobParams { get; set; } = new CommonJobParams
    {
        Quality = "fast",
        Mode = "translate",
        BrowserType = 1,
        TextType = "plaintext"
    };

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // 使用當前時間戳

    public Params(string targetLan, string? srcLan, string text, string? regionalVariant = null)
    {
        this.commonJobParams.RegionalVariant = regionalVariant;
        this.Lang = new Language
        {
            TargetLang = targetLan,
            SourceLangComputed = srcLan
        };
        this.Jobs = new List<Job>{
            new Job(text)
        };
    }
}

public class SplitTextResp
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("result")]
    public _Result Result { get; set; }

    public class _Result
    {
        [JsonPropertyName("lang")]
        public Language Lang { get; set; }

        [JsonPropertyName("texts")]
        public List<Text> Texts { get; set; }

        public class Language
        {
            [JsonPropertyName("detected")]
            public string Detected { get; set; }

            [JsonPropertyName("isConfident")]
            public bool IsConfident { get; set; }
        }

        public class Text
        {
            [JsonPropertyName("chunks")]
            public List<Chunk> Chunks { get; set; }

            public class Chunk
            {
                [JsonPropertyName("sentences")]
                public List<Sentence> Sentences { get; set; }

                public class Sentence
                {
                    [JsonPropertyName("prefix")]
                    public string Prefix { get; set; }

                    [JsonPropertyName("text")]
                    public string Text { get; set; }
                }
            }
        }
    }
}

public class SplitTextReqParams
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; }

    [JsonPropertyName("params")]
    public RequestParams Params { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    // 建構函式
    public SplitTextReqParams(List<string> texts)
    {
        JsonRpc = "2.0";
        Method = "LMT_split_text";
        Id = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 100000000);
        Params = new RequestParams
        {
            Texts = texts,
            CommonJobParams = new _CommonJobParams
            {
                Mode = "translate",
                TextType = "plaintext"
            },
            Lang = new Language
            {
                LangUserSelected = "auto",
            }
        };
    }
    public class _CommonJobParams
    {
        [JsonPropertyName("mode")]
        public string Mode { get; set; }

        [JsonPropertyName("textType")]
        public required string TextType { get; set; }
    }
    public class Language
    {
        [JsonPropertyName("lang_user_selected")]
        public string LangUserSelected { get; set; }

        [JsonPropertyName("preference")]
        public Preference? Preference { get; set; }
    }
    public class Preference
    {
        [JsonPropertyName("weight")]
        public Dictionary<string, double> Weight { get; set; }

        [JsonPropertyName("default")]
        public string Default { get; set; }
    }
    public class RequestParams
    {
        [JsonPropertyName("texts")]
        public List<string> Texts { get; set; }

        [JsonPropertyName("commonJobParams")]
        public required _CommonJobParams CommonJobParams { get; set; }

        [JsonPropertyName("lang")]
        public required Language Lang { get; set; }
    }
}

public class DeepLTranslator : ITranslator
{
    private HttpClient client;
    // The free web endpoint throttles aggressively; after a 429 skip DeepL for a while
    // instead of hitting the limit again on every keystroke.
    private static readonly TimeSpan rateLimitCooldown = TimeSpan.FromSeconds(60);
    private long blockedUntil;
    private const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0";
    private const string apiEndPoint = "https://www2.deepl.com/jsonrpc?client=chrome-extension,1.33.0";
    public DeepLTranslator()
    {
        client = BuildClient();
    }

    private static HttpClient BuildClient()
    {
        var c = new HttpClient(UtilsFun.httpClientDefaultHandler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        c.DefaultRequestHeaders.Host = "www2.deepl.com";
        c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        c.DefaultRequestHeaders.Connection.Add("keep-alive");
        return c;
    }

    public SplitTextResp? SplitText(string rawText)
    {
        var body = new SplitTextReqParams(new List<string>{
            rawText
        });
        var response = this.client.PostAsJsonAsync(apiEndPoint, body).GetAwaiter().GetResult();
        var respDecode = response.Content.ReadFromJsonAsync<SplitTextResp>().GetAwaiter().GetResult();

        return respDecode;
    }


    /// <summary>
    /// Maps a culture name to DeepL's target language and regional variant.
    /// "auto" translates CJK text to English and everything else to Traditional Chinese.
    /// </summary>
    public static (string TargetLang, string? RegionalVariant) ResolveTarget(string src, string toLan)
    {
        var target = toLan.Trim();
        if (target.Equals("auto", StringComparison.OrdinalIgnoreCase))
            target = ContainsCjk(src) ? "en" : "zh-Hant";
        if (ChineseConverter.IsTraditionalTarget(target))
            return ("ZH", "zh-Hant");
        if (target.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase)
            || target.Equals("zhs", StringComparison.OrdinalIgnoreCase)
            || target.Equals("zh-CN", StringComparison.OrdinalIgnoreCase))
            return ("ZH", "zh-Hans");
        var dash = target.IndexOf('-');
        return ((dash > 0 ? target[..dash] : target).ToUpperInvariant(), null);
    }

    public static bool ContainsCjk(string text)
    {
        foreach (var c in text)
        {
            if (c is (>= '\u3040' and <= '\u30ff') or (>= '\u3400' and <= '\u9fff') or (>= '\uac00' and <= '\ud7af') or (>= '\uf900' and <= '\ufaff'))
                return true;
        }
        return false;
    }

    public override DeepLTranslateResult? Translate(string src, string toLan, string? fromLan)
    {
        var remaining = Interlocked.Read(ref blockedUntil) - UtilsFun.GetUtcTimeNow();
        if (remaining > 0)
            throw new TranslatorUnavailableException($"rate-limited, retrying in {(remaining + 999) / 1000}s");

        var (targetLang, variant) = ResolveTarget(src, toLan);
        if (fromLan == "auto")
            fromLan = null;
        var body = new DeepLTranslateRequestBody(new Params(targetLang, fromLan, src, variant));
        var bodyStr = JsonSerializer.Serialize(body, TranslatorJsonContext.Default.DeepLTranslateRequestBody);
        var response = this.client.PostAsync(apiEndPoint, new StringContent(bodyStr, Encoding.UTF8, "application/json"))
                                            .GetAwaiter()
                                            .GetResult();
        var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            Interlocked.Exchange(ref blockedUntil, UtilsFun.GetUtcTimeNow() + (long)rateLimitCooldown.TotalMilliseconds);
            throw new TranslatorUnavailableException("rate-limited (HTTP 429)");
        }
        var res = UtilsFun.ParseJson(content, TranslatorJsonContext.Default.DeepLTranslateResult);
        if (res == null || !res.IsSuccess())
        {
            if (!response.IsSuccessStatusCode)
                throw new TranslatorUnavailableException($"HTTP {(int)response.StatusCode}");
            return null;
        }
        if (variant == "zh-Hant")
            res.ConvertToTraditional();
        return res;
    }

    public override void Reset()
    {
        var old = client;
        client = BuildClient();
        old.Dispose();
        Interlocked.Exchange(ref blockedUntil, 0);
    }
}
