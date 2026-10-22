using System.Net.Http;
using Wox.Plugin;
using Translator.Utils;
namespace Translator.Suggest;

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
    private IPublicAPI api;
    private HttpClient client;
    public SuggestHelper(IPublicAPI api)
    {
        client = BuildClient();
        this.api = api;
        UtilsFun.onHttpDefaultHandlerChange += () => client = BuildClient();
    }
    private static HttpClient BuildClient()
    {
        return new HttpClient(UtilsFun.httpClientDefaultHandler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }
    public List<ResultItem> QuerySuggest(string query)
    {

        KeyValuePair<string, string>[] data = [new("kw", query)];
        try
        {
            var res = client.PostAsync("https://fanyi.baidu.com/sug", data.ToFormContent())
                                        .GetAwaiter()
                                        .GetResult();
            var sug = UtilsFun.ParseJson(res.Content.ReadAsStringAsync().GetAwaiter().GetResult(), Protocol.TranslatorJsonContext.Default.SuggestInterface);
            if (sug == null || sug.data == null || sug.data.Length == 0)
                return new List<ResultItem>();
            var result = new List<ResultItem>();
            foreach (var item in sug.data)
            {
                result.Add(new ResultItem
                {
                    Title = item.v,
                    SubTitle = $"{item.k}",
                    fromApiName = "Baidu",
                    transType = "[Suggest]"

                });

            }
            return result;
        }
        catch (Exception err)
        {
            return new List<ResultItem>{
                    new ResultItem{
                        Title = "some err happen in suggest",
                        SubTitle = err.Message
                    }
                };
        }
    }
}
