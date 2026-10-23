using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using PowerTranslatorExtension.Protocol;
using PowerTranslatorExtension.Utils;

namespace PowerTranslatorExtension.Service.DeepL;

public class DeepLTranslateResult : ITranslateResult
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("result")]
    public InnerResult? Result { get; set; }

    public bool IsSuccess() => Result != null;

    /// <summary>Guarantees Traditional characters even if DeepL ignores the regional variant.</summary>
    public void ConvertToTraditional()
    {
        foreach (var translation in Result?.Translations ?? [])
            foreach (var beam in translation.Beams)
                foreach (var sentence in beam.Sentences)
                    sentence.Text = ChineseConverter.ToTraditional(sentence.Text);
    }

    public IEnumerable<ResultItem>? Transform()
    {
        if (!IsSuccess())
            return null;
        var res = new List<ResultItem>();
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

    public class InnerResult
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

    public DeepLTranslateRequestBody(Params p)
    {
        this.Params = p;
        this.Id = (int)(p.Timestamp % 100000000);
    }
}

public class Params
{
    public class Job
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "default";

        [JsonPropertyName("sentences")]
        public List<JobSentence> Sentences { get; set; }

        [JsonPropertyName("preferred_num_beams")]
        public int PreferredNumBeams { get; set; } = 4;

        public Job(string text)
        {
            this.Sentences = new List<JobSentence>
            {
                new JobSentence { Id = 1, Prefix = "", Text = text },
            };
        }
    }

    public class JobSentence
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

        // e.g. "zh-Hant" / "zh-Hans" / "en-US"; the web client sends it for regional target variants.
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
        TextType = "plaintext",
    };

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public Params(string targetLan, string? srcLan, string text, string? regionalVariant = null)
    {
        this.Lang = new Language { TargetLang = targetLan, SourceLangComputed = srcLan };
        this.Jobs = new List<Job> { new Job(text) };
        this.commonJobParams.RegionalVariant = regionalVariant;
    }
}

public class DeepLTranslator : ITranslator, IDisposable
{
    private HttpClient client;
    private const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0";
    private const string apiEndPoint = "https://www2.deepl.com/jsonrpc?client=chrome-extension,1.33.0";
    // The free web endpoint throttles aggressively; after a 429 skip DeepL for a while
    // instead of hitting the limit again on every keystroke.
    private static readonly TimeSpan rateLimitCooldown = TimeSpan.FromSeconds(60);
    private long blockedUntil;
    private bool inited;

    public bool Inited => inited;
    public string Name => "DeepL";

    public DeepLTranslator()
    {
        client = BuildClient();
    }

    private static HttpClient BuildClient()
    {
        var c = new HttpClient(UtilsFun.httpClientDefaultHandler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        c.DefaultRequestHeaders.Host = "www2.deepl.com";
        c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        c.DefaultRequestHeaders.Connection.Add("keep-alive");
        return c;
    }

    public void Init()
    {
        inited = true;
    }

    public void Reset()
    {
        client.Dispose();
        client = BuildClient();
        Interlocked.Exchange(ref blockedUntil, 0);
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

    public ITranslateResult? Translate(string src, string toLan, string fromLan)
    {
        var remaining = Interlocked.Read(ref blockedUntil) - UtilsFun.GetUtcTimeNow();
        if (remaining > 0)
            throw new TranslatorUnavailableException($"rate-limited, retrying in {(remaining + 999) / 1000}s");

        var (targetLang, variant) = ResolveTarget(src, toLan);
        string? from = string.Equals(fromLan, "auto", StringComparison.OrdinalIgnoreCase) ? null : fromLan;
        var body = new DeepLTranslateRequestBody(new Params(targetLang, from, src, variant));
        var bodyStr = JsonSerializer.Serialize(body, TranslatorJsonContext.Default.DeepLTranslateRequestBody);
        var response = client.PostAsync(apiEndPoint, new StringContent(bodyStr, Encoding.UTF8, "application/json"))
            .GetAwaiter().GetResult();
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

    public void Dispose()
    {
        client.Dispose();
        GC.SuppressFinalize(this);
    }
}
