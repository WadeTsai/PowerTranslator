using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Translator.Protocol;
using Translator.Service.Youdao.Utils;
using Translator.Utils;
using Wox.Plugin.Logger;

namespace Translator.Service.Youdao.V2;

public class KeyResponse
{
    public class Data
    {
        public string? secretKey { get; set; }
    }
    public int code { get; set; }
    public Data? data { get; set; }
}

public class TranslateResponse : ITranslateResult
{
    public struct TranDictResult
    {
        public struct Ce
        {
            public struct Word
            {
                public struct Trs
                {
                    public string? voice { get; set; }
                    [JsonPropertyName("#text")]
                    public string? text { get; set; }
                    [JsonPropertyName("#tran")]
                    public string? tran { get; set; }
                }
                public Trs[]? trs { get; set; }
                public string? phone { get; set; }
                [JsonPropertyName("return-phrase")]
                public string return_phrase { get; set; }
            }
            public Word? word { get; set; }
        }
        public struct Ec
        {
            public struct Word
            {
                public struct Trs
                {
                    public string? pos { get; set; }
                    public string? tran { get; set; }
                }
                public Trs[]? trs { get; set; }
                public string? usphone { get; set; }
                [JsonPropertyName("return-phrase")]
                public string return_phrase { get; set; }
            }
            public string[]? exam_type { get; set; }
            public Word? word { get; set; }
        }
        public Ce? ce { get; set; }
        public Ec? ec { get; set; }
    }

    public struct TranslateResult
    {
        public string tgt { get; set; }
        public string src { get; set; }
        public string? srcPronounce { get; set; }
        public string? tgtPronounce { get; set; }
    }

    public int code { get; set; }
    public TranDictResult? dictResult { get; set; }
    public TranslateResult[][]? translateResult { get; set; }
    public string? type { get; set; }

    public override IEnumerable<ResultItem>? Transform()
    {
        if (this.code != 0)
            return null;
        List<ResultItem> res = new List<ResultItem>();
        foreach (var tres in this.translateResult![0])
        {
            string? srcpron = tres.srcPronounce ?? dictResult?.ec?.word?.usphone;
            string? tgtpron = tres.tgtPronounce;
            res.Add(new ResultItem
            {
                Title = tres.tgt,
                SubTitle = tres.src + (srcpron != null ? $" ({srcpron})" : ""),
                transType = this.type ?? "unknow type",
                CopyTgt = tres.tgt,
                Description = $"{tres.tgt}{(tgtpron != null ? $"\n({tgtpron})" : "")}\n{tres.src} {(srcpron != null ? $" ({srcpron})" : "")}"
            });
        }
        if (this.dictResult != null)
        {
            if (this.dictResult?.ce != null)
            {
                var ce = this.dictResult?.ce;
                if (ce?.word?.trs != null)
                {
                    foreach (var trs in ce?.word?.trs!)
                    {
                        res.Add(new ResultItem
                        {
                            Title = trs.text ?? "[None]",
                            SubTitle = trs.tran ?? "",
                            transType = "[Smart Result]"
                        });
                    }
                }
            }
            else if (this.dictResult?.ec != null)
            {
                var ec = this.dictResult?.ec;
                if (ec?.word?.trs != null)
                {
                    foreach (var trs in ec?.word?.trs!)
                    {
                        res.Add(new ResultItem
                        {
                            Title = trs.tran ?? "[None]",
                            SubTitle = trs.pos ?? ""
                        });
                    }
                }
                if (ec?.exam_type != null)
                {
                    res.Add(new ResultItem
                    {
                        Title = String.Join(" | ", ec?.exam_type!),
                        SubTitle = "exam",
                        transType = "exam"
                    });
                }
            }
        }
        res.each((val) =>
        {
            val.fromApiName = "Youdao Web Api";
        });
        return res;
    }
}

public class YoudaoTranslator : ITranslator
{
    private static readonly byte[] encryptKey = MD5.HashData(Encoding.UTF8.GetBytes("ydsecret://query/key/B*RGygVywfNBwpmBaZg*WT7SIOUP2T0C9WHMZN39j^DAdaZhAnxvGcCY6VYFwnHl"));
    private static readonly byte[] iv = MD5.HashData(Encoding.UTF8.GetBytes("ydsecret://query/iv/C@lZe2YzHtZ2CYgaXKSVfsb7Y4QWHjITPPZ0nQp87fBeJ!Iv6v^6fvi2WN@bYpJ4"));
    private HttpClient client;
    private string secretKey;
    private string userAgent;

    public YoudaoTranslator()
    {
        this.userAgent = UtilsFun.GetRandomUserAgent();
        this.client = BuildClient(this.userAgent);

        var keyUrl = "https://dict.youdao.com/webtranslate/key?" + BaseBody("asdjnjfenknafdfsdfsd")
            .Prepend(new("keyid", "webfanyi-key-getter"))
            .ToFormString();
        var res = this.client.GetAsync(keyUrl).GetAwaiter().GetResult();
        var keyRes = UtilsFun.ParseJson(res.Content.ReadAsStringAsync().GetAwaiter().GetResult(), TranslatorJsonContext.Default.KeyResponse);

        if (keyRes?.code != 0 || keyRes.data?.secretKey == null)
        {
            Log.Error($"err in get secretKey, {keyRes?.ToString()}", typeof(YoudaoTranslator));
            throw new Exception("err in get secretKey");
        }

        this.secretKey = keyRes.data.secretKey;
    }

    private static HttpClient BuildClient(string userAgent)
    {
        var c = new HttpClient(UtilsFun.httpClientDefaultHandler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        c.DefaultRequestHeaders.Add("User-Agent", userAgent);
        c.DefaultRequestHeaders.Add("Referer", "https://fanyi.youdao.com/");
        c.DefaultRequestHeaders.Add("Origin", "https://fanyi.youdao.com");
        c.DefaultRequestHeaders.Add("Cookie", YoudaoUtils.RandomCookie());
        return c;
    }

    public override TranslateResponse? Translate(string src, string toLan = "auto", string fromLan = "auto")
    {
        toLan = YoudaoUtils.Instance.GetYoudaoLanguageCode(toLan);
        fromLan = YoudaoUtils.Instance.GetYoudaoLanguageCode(fromLan);
        KeyValuePair<string, string>[] data =
        [
            new("i", src),
            new("from", fromLan),
            new("to", toLan),
            new("dictResult", "true"),
            new("keyid", "webfanyi"),
        ];
        var res = this.client.PostAsync("https://dict.youdao.com/webtranslate", data.Concat(BaseBody(this.secretKey)).ToFormContent())
                                        .GetAwaiter()
                                        .GetResult();

        var content = Decrypt(res.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        return UtilsFun.ParseJson(content, TranslatorJsonContext.Default.YoudaoV2Response);
    }

    private static string Md5Hex(string src)
    {
        return Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(src)));
    }

    private static string Sign(string t, string key)
    {
        return Md5Hex($"client=fanyideskweb&mysticTime={t}&product=webfanyi&key={key}");
    }

    private static KeyValuePair<string, string>[] BaseBody(string key)
    {
        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        return
        [
            new("sign", Sign(now, key)),
            new("client", "fanyideskweb"),
            new("product", "webfanyi"),
            new("appVersion", "1.0.0"),
            new("vendor", "web"),
            new("pointParam", "client,mysticTime,product"),
            new("mysticTime", now),
            new("keyfrom", "fanyi.web"),
        ];
    }

    private static string Decrypt(string src)
    {
        var cipher = Convert.FromBase64String(src.Replace('-', '+').Replace('_', '/'));
        using var aes = Aes.Create();
        aes.Key = encryptKey;
        return Encoding.UTF8.GetString(aes.DecryptCbc(cipher, iv));
    }

    public override void Reset()
    {
        this.userAgent = UtilsFun.GetRandomUserAgent();
        var old = this.client;
        this.client = BuildClient(this.userAgent);
        old.Dispose();
    }
}
