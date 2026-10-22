using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using PowerTranslatorExtension.Protocol;
using PowerTranslatorExtension.Service.Youdao.Utils;
using PowerTranslatorExtension.Utils;

namespace PowerTranslatorExtension.Service.Youdao.Old;

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

    public IEnumerable<ResultItem>? Transform()
    {
        if (this.errorCode != 0 || this.translateResult == null || this.translateResult.Length == 0)
            return null;
        var res = new List<ResultItem>
        {
            new ResultItem
            {
                Title = this.translateResult[0][0].tgt,
                SubTitle = this.translateResult[0][0].src,
                transType = this.type ?? "Translate",
            }
        };
        if (this.smartResult?.entries != null)
        {
            foreach (var s in this.smartResult.Value.entries)
            {
                var t = s.Replace("\r\n", " ").TrimStart().Replace(" ", "");
                if (string.IsNullOrEmpty(t))
                    continue;
                res.Add(new ResultItem { Title = t, SubTitle = Loc.Get("Tag_SmartResult") });
            }
        }
        foreach (var val in res)
            val.fromApiName = "Youdao Old Web Api";
        return res;
    }
}

public class YoudaoTranslator : ITranslator, IDisposable
{
    private HttpClient? client;
    private readonly string userAgent = UtilsFun.GetRandomUserAgent();
    private readonly string bv;
    private bool inited;

    public bool Inited => inited;
    public string Name => "Youdao Old Web Api";

    public YoudaoTranslator()
    {
        bv = Md5Hex(userAgent);
    }

    public void Init() => Reset();

    public void Reset()
    {
        var c = new HttpClient(UtilsFun.httpClientDefaultHandler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        c.DefaultRequestHeaders.Add("User-Agent", userAgent);
        c.DefaultRequestHeaders.Referrer = new Uri("https://fanyi.youdao.com/");
        c.DefaultRequestHeaders.Add("Origin", "https://fanyi.youdao.com");
        c.DefaultRequestHeaders.Add("Cookie", YoudaoUtils.RandomCookie());
        var old = client;
        client = c;
        old?.Dispose();
        inited = true;
    }

    private static string Md5Hex(string src)
    {
        return Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(src)));
    }

    public ITranslateResult? Translate(string src, string toLan = "AUTO", string fromLan = "AUTO")
    {
        var c = client;
        if (!inited || c == null)
            return null;
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
        var res = c.PostAsync("https://fanyi.youdao.com/translate_o?smartresult=dict&smartresult=rule", data.ToFormContent())
            .GetAwaiter().GetResult();
        var content = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        return UtilsFun.ParseJson(content, TranslatorJsonContext.Default.YoudaoOldResponse);
    }

    public void Dispose()
    {
        client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
