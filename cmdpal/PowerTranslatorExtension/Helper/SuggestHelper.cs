using System;
using System.Collections.Generic;
using System.Net.Http;
using PowerTranslatorExtension.Protocol;
using PowerTranslatorExtension.Utils;

namespace PowerTranslatorExtension.Suggest;

public class SuggestInterface
{
    public struct SuggestItem
    {
        public string k { get; set; }
        public string v { get; set; }
    }
    public int errno { get; set; }
    public SuggestItem[]? data { get; set; }
}

public class SuggestHelper
{
    private HttpClient client;

    public SuggestHelper()
    {
        client = new HttpClient(UtilsFun.httpClientDefaultHandler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(5),
        };
        UtilsFun.onHttpDefaultHandlerChange += () =>
        {
            client = new HttpClient(UtilsFun.httpClientDefaultHandler, disposeHandler: false)
            {
                Timeout = TimeSpan.FromSeconds(5),
            };
        };
    }

    public List<ResultItem> QuerySuggest(string query)
    {
        KeyValuePair<string, string>[] body = [new("kw", query)];
        try
        {
            var res = client.PostAsync("https://fanyi.baidu.com/sug", body.ToFormContent())
                .GetAwaiter().GetResult();
            var raw = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var sug = UtilsFun.ParseJson(raw, Protocol.TranslatorJsonContext.Default.SuggestInterface);
            if (sug == null || sug.data == null || sug.data.Length == 0)
                return new List<ResultItem>();
            var result = new List<ResultItem>(sug.data.Length);
            foreach (var item in sug.data)
            {
                result.Add(new ResultItem
                {
                    Title = item.v,
                    SubTitle = item.k,
                    fromApiName = "Baidu",
                    transType = "[Suggest]",
                });
            }
            return result;
        }
        catch (Exception err)
        {
            UtilsFun.LogMessage($"Suggest failed: {err.Message}");
            return new List<ResultItem>();
        }
    }
}
