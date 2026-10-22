using System.Security.Cryptography;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Translator.Utils;
using Translator.Protocol;
using Translator.Service.Youdao.Utils;

namespace Translator.Service.Youdao.old;

public class TranslateResponse : ITranslateResult
{
    public struct ResStruct
    {
        public string tgt { get; set; }
        public string src { get; set; }
    }
    public struct Entry
    {
        public string[] entries { get; set; }
        public int type { get; set; }
    }
    public int errorCode { get; set; }
    public ResStruct[][]? translateResult { get; set; }
    public string? type { get; set; }
    public Entry? smartResult { get; set; }

    public override IEnumerable<ResultItem>? Transform()
    {
        if (this.errorCode != 0)
            return null;
        List<ResultItem> res = new List<ResultItem>();
        res.Add(new ResultItem
        {
            Title = this.translateResult![0][0].tgt,
            SubTitle = $"{this.translateResult![0][0].src}",
            transType = this.type ?? "Translate"
        });
        if (this.smartResult != null)
        {
            this.smartResult?.entries.each((s) =>
            {
                string t = s.Replace("\r\n", " ").TrimStart().Replace(" ", "");
                if (string.IsNullOrEmpty(t))
                    return;
                res.Add(new ResultItem
                {
                    Title = t,
                    SubTitle = "[smart result]"
                });
            });
        }
        res.each((val) =>
        {
            val.fromApiName = "Youdao Old Web Api";
        });
        return res;
    }
}

public class YoudaoTranslator : ITranslator
{
    private HttpClient? client;
    private readonly string userAgent;
    private readonly string bv;
    public YoudaoTranslator()
    {
        this.userAgent = UtilsFun.GetRandomUserAgent();
        this.bv = Md5Hex(this.userAgent);
        this.Reset();
    }
    public override void Reset()
    {
        var c = new HttpClient(UtilsFun.httpClientDefaultHandler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        c.DefaultRequestHeaders.Add("User-Agent", userAgent);
        c.DefaultRequestHeaders.Add("Referer", "https://fanyi.youdao.com/");
        c.DefaultRequestHeaders.Add("Origin", "https://fanyi.youdao.com");
        c.DefaultRequestHeaders.Add("Cookie", YoudaoUtils.RandomCookie());
        var old = client;
        client = c;
        old?.Dispose();
    }
    private static string Md5Hex(string src)
    {
        return Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(src)));
    }

    public override TranslateResponse? Translate(string src, string toLan = "AUTO", string fromLan = "AUTO")
    {
        toLan = YoudaoUtils.Instance.GetYoudaoLanguageCode(toLan);
        fromLan = YoudaoUtils.Instance.GetYoudaoLanguageCode(fromLan);

        var ts = UtilsFun.GetUtcTimeNow().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var salt = $"{ts}{Random.Shared.Next(0, 9)}";
        var sign = Md5Hex($"fanyideskweb{src}{salt}Ygy_4c=r#e#4EX^NUGUc5");
        KeyValuePair<string, string>[] data =
        [
            new("i", src),
            new("from", fromLan),
            new("to", toLan),
            new("smartresult", "dict"),
            new("client", "fanyideskweb"),
            new("salt", salt),
            new("sign", sign),
            new("lts", ts),
            new("bv", bv),
            new("doctype", "json"),
            new("version", "2.1"),
            new("keyfrom", "fanyi.web"),
            new("action", "FY_BY_REALTlME"),
        ];
        var res = client!.PostAsync("https://fanyi.youdao.com/translate_o?smartresult=dict&smartresult=rule", data.ToFormContent())
                                    .GetAwaiter()
                                    .GetResult();
        string contentStr = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        try
        {
            return JsonSerializer.Deserialize(contentStr, TranslatorJsonContext.Default.YoudaoOldResponse);
        }
        catch (JsonException)
        {
            throw new Exception($"can not find data in response content({contentStr}), http state code:{res.StatusCode}");
        }
    }
}
