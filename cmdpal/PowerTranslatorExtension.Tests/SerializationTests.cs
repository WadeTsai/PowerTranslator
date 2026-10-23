using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerTranslatorExtension.Protocol;
using PowerTranslatorExtension.Utils;

namespace PowerTranslatorExtension.Tests;

[TestClass]
public class SerializationTests
{
    [TestMethod]
    public void DeepLRequestBody_SerializesWithSourceGeneration()
    {
        var body = new Service.DeepL.DeepLTranslateRequestBody(new Service.DeepL.Params("EN", null, "你好"));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(body, TranslatorJsonContext.Default.DeepLTranslateRequestBody));

        Assert.AreEqual("LMT_handle_jobs", json.RootElement.GetProperty("method").GetString());
        Assert.AreEqual("你好", json.RootElement.GetProperty("params").GetProperty("jobs")[0].GetProperty("sentences")[0].GetProperty("text").GetString());
        Assert.IsFalse(json.RootElement.GetProperty("params").GetProperty("commonJobParams").TryGetProperty("regionalVariant", out _));
    }

    [TestMethod]
    public void DeepLRequestBody_SerializesRegionalVariant()
    {
        var body = new Service.DeepL.DeepLTranslateRequestBody(new Service.DeepL.Params("ZH", null, "hello world", "zh-Hant"));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(body, TranslatorJsonContext.Default.DeepLTranslateRequestBody));

        var p = json.RootElement.GetProperty("params");
        Assert.AreEqual("ZH", p.GetProperty("lang").GetProperty("target_lang").GetString());
        Assert.AreEqual("zh-Hant", p.GetProperty("commonJobParams").GetProperty("regionalVariant").GetString());
    }
}
