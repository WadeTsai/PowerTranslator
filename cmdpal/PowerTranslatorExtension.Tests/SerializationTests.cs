using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerTranslatorExtension.Protocol;
using PowerTranslatorExtension.Utils;
using V2Translator = PowerTranslatorExtension.Service.Youdao.V2.YoudaoTranslator;

namespace PowerTranslatorExtension.Tests;

[TestClass]
public class SerializationTests
{
    [TestMethod]
    public void YoudaoV2Decrypt_RoundTripsUrlSafeBase64AesPayload()
    {
        const string plain = "{\"code\":0,\"translateResult\":[[{\"tgt\":\"你好\",\"src\":\"hello\"}]]}";
        using var aes = Aes.Create();
        aes.Key = MD5.HashData(Encoding.UTF8.GetBytes("ydsecret://query/key/B*RGygVywfNBwpmBaZg*WT7SIOUP2T0C9WHMZN39j^DAdaZhAnxvGcCY6VYFwnHl"));
        var iv = MD5.HashData(Encoding.UTF8.GetBytes("ydsecret://query/iv/C@lZe2YzHtZ2CYgaXKSVfsb7Y4QWHjITPPZ0nQp87fBeJ!Iv6v^6fvi2WN@bYpJ4"));
        var cipher = Convert.ToBase64String(aes.EncryptCbc(Encoding.UTF8.GetBytes(plain), iv))
            .Replace('+', '-').Replace('/', '_');

        Assert.AreEqual(plain, V2Translator.Decrypt(cipher));
    }

    [TestMethod]
    public void YoudaoV2Response_DeserializesBothDictionaryShapesWithSourceGeneration()
    {
        const string ce = """{"code":0,"type":"zh-CHS2en","translateResult":[[{"tgt":"hello","src":"你好"}]],"dictResult":{"ce":{"word":{"trs":[{"#text":"hello","#tran":"你好"}],"return-phrase":"你好"}}}}""";
        const string ec = """{"code":0,"type":"en2zh-CHS","translateResult":[[{"tgt":"你好","src":"hello"}]],"dictResult":{"ec":{"word":{"usphone":"həˈloʊ","trs":[{"pos":"int.","tran":"你好"}],"return-phrase":"hello"}}}}""";

        var ceItems = UtilsFun.ParseJson(ce, TranslatorJsonContext.Default.YoudaoV2Response)!.Transform()!.ToList();
        var ecItems = UtilsFun.ParseJson(ec, TranslatorJsonContext.Default.YoudaoV2Response)!.Transform()!.ToList();

        Assert.AreEqual("hello", ceItems[1].Title);
        Assert.AreEqual("你好", ceItems[1].SubTitle);
        Assert.AreEqual("hello (həˈloʊ)", ecItems[0].SubTitle);
        Assert.AreEqual("你好", ecItems[1].Title);
        Assert.AreEqual("int.", ecItems[1].SubTitle);
    }

    [TestMethod]
    public void DeepLRequestBody_SerializesWithSourceGeneration()
    {
        var body = new Service.DeepL.DeepLTranslateRequestBody(new Service.DeepL.Params("EN", null, "你好"));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(body, TranslatorJsonContext.Default.DeepLTranslateRequestBody));

        Assert.AreEqual("LMT_handle_jobs", json.RootElement.GetProperty("method").GetString());
        Assert.AreEqual("你好", json.RootElement.GetProperty("params").GetProperty("jobs")[0].GetProperty("sentences")[0].GetProperty("text").GetString());
    }
}
