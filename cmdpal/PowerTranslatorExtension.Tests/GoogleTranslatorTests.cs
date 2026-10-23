using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerTranslatorExtension.Service.Google;

namespace PowerTranslatorExtension.Tests;

[TestClass]
public class GoogleTranslatorTests
{
    [TestMethod]
    [DataRow("hello world", "auto", "zh-TW")]
    [DataRow("你好世界", "auto", "en")]
    [DataRow("hello", "zh-Hant", "zh-TW")]
    [DataRow("hello", "zht", "zh-TW")]
    [DataRow("hello", "zhs", "zh-CN")]
    [DataRow("hello", "ja", "ja")]
    [DataRow("hello", "en-US", "en")]
    public void ResolveTarget_MapsCultureToGoogleCode(string src, string toLan, string expected)
    {
        Assert.AreEqual(expected, GoogleTranslator.ResolveTarget(src, toLan));
    }

    [TestMethod]
    public void ParseResponse_JoinsSegmentsAndReadsDetectedSource()
    {
        const string content = """[[["今日は天気がいいです。","The weather is nice today.",null,null,3],["散歩しましょう","Let's walk",null,null,3]],null,"en",null,null,null,1]""";

        var result = GoogleTranslator.ParseResponse(content, "ja");

        Assert.IsNotNull(result);
        Assert.AreEqual("今日は天気がいいです。散歩しましょう", result.Text);
        Assert.AreEqual("en", result.SourceLang);
        var items = result.Transform()!.ToList();
        Assert.HasCount(1, items);
        Assert.AreEqual("en -> ja", items[0].SubTitle);
        Assert.AreEqual(GoogleTranslator.ApiName, items[0].fromApiName);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not json")]
    [DataRow("[null,null,\"en\"]")]
    [DataRow("[[],null,\"en\"]")]
    public void ParseResponse_ReturnsNullForUnusableContent(string content)
    {
        Assert.IsNull(GoogleTranslator.ParseResponse(content, "ja"));
    }
}
