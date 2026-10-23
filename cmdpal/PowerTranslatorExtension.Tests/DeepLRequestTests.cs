using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerTranslatorExtension.Service.DeepL;

namespace PowerTranslatorExtension.Tests;

[TestClass]
public class DeepLRequestTests
{
    [TestMethod]
    public void Params_CreatesSinglePlainTextTranslationJob()
    {
        var parameters = new Params("EN", "ZH", "你好");

        Assert.AreEqual("EN", parameters.Lang.TargetLang);
        Assert.AreEqual("ZH", parameters.Lang.SourceLangComputed);
        Assert.HasCount(1, parameters.Jobs);
        Assert.HasCount(1, parameters.Jobs[0].Sentences);
        Assert.AreEqual("你好", parameters.Jobs[0].Sentences[0].Text);
        Assert.AreEqual("plaintext", parameters.commonJobParams.TextType);
    }

    [TestMethod]
    public void RequestBody_DerivesIdAndUsesExpectedJsonRpcShape()
    {
        var parameters = new Params("EN", null, "hello") { Timestamp = 123456789 };
        var request = new DeepLTranslateRequestBody(parameters);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(request));

        Assert.AreEqual(23456789, request.Id);
        Assert.AreEqual("2.0", json.RootElement.GetProperty("jsonrpc").GetString());
        Assert.AreEqual("LMT_handle_jobs", json.RootElement.GetProperty("method").GetString());
        Assert.AreEqual("EN", json.RootElement.GetProperty("params").GetProperty("lang").GetProperty("target_lang").GetString());
    }

    [TestMethod]
    [DataRow("hello world", "auto", "ZH", "zh-Hant")]
    [DataRow("你好世界", "auto", "EN", null)]
    [DataRow("こんにちは", "auto", "EN", null)]
    [DataRow("hello", "zh-Hant", "ZH", "zh-Hant")]
    [DataRow("hello", "zh-TW", "ZH", "zh-Hant")]
    [DataRow("hello", "zh-Hans", "ZH", "zh-Hans")]
    [DataRow("hello", "ja", "JA", null)]
    [DataRow("hello", "en-US", "EN", null)]
    public void ResolveTarget_MapsCultureAndAutoToDeepLTarget(string src, string toLan, string expectedLang, string? expectedVariant)
    {
        var (lang, variant) = DeepLTranslator.ResolveTarget(src, toLan);

        Assert.AreEqual(expectedLang, lang);
        Assert.AreEqual(expectedVariant, variant);
    }
}
