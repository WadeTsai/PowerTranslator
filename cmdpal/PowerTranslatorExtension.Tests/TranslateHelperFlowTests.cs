using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerTranslatorExtension.Protocol;

namespace PowerTranslatorExtension.Tests;

[TestClass]
public class TranslateHelperFlowTests
{
    [TestMethod]
    public void ParseRawSrc_ParsesTargetAliasAndNormalizesCodeWhenEnabled()
    {
        var helper = CreateHelper([], defaultLanguage: "en", codeModeEnabled: true);

        var target = helper.ParseRawSrc("camelCase -> zht");

        Assert.AreEqual("camel Case", target.src);
        Assert.AreEqual("zh-Hant", target.toLan);
    }

    [TestMethod]
    public void QueryTranslate_UsesParsedInputAndFirstTranslatorResult()
    {
        var translator = new FakeTranslator
        {
            Result = new FakeTranslateResult(new ResultItem { Title = "hello", SubTitle = "你好" }),
        };
        var helper = CreateHelper([translator], defaultLanguage: "zhs");

        var results = helper.QueryTranslate("你好 -> zht");

        Assert.HasCount(1, results);
        Assert.AreEqual("hello", results[0].Title);
        Assert.HasCount(1, translator.Calls);
        AssertCall(translator.Calls[0], "你好", "zh-Hant", "auto");
    }

    [TestMethod]
    public void QueryTranslate_FallsBackToNextTranslatorWhenFirstReturnsNull()
    {
        var primary = new FakeTranslator();
        var fallback = new FakeTranslator
        {
            Result = new FakeTranslateResult(new ResultItem { Title = "fallback", SubTitle = "source" }),
        };
        var helper = CreateHelper([primary, fallback], defaultLanguage: "en");

        var results = helper.QueryTranslate("source");

        Assert.HasCount(1, results);
        Assert.AreEqual("fallback", results[0].Title);
        Assert.HasCount(1, primary.Calls);
        Assert.HasCount(1, fallback.Calls);
        AssertCall(primary.Calls[0], "source", "en", "auto");
        AssertCall(fallback.Calls[0], "source", "en", "auto");
    }

    [TestMethod]
    public void QueryTranslate_ExplicitTargetLanguageOverridesParsedTarget()
    {
        var translator = new FakeTranslator
        {
            Result = new FakeTranslateResult(new ResultItem { Title = "bonjour", SubTitle = "hello" }),
        };
        var helper = CreateHelper([translator], defaultLanguage: "en");

        _ = helper.QueryTranslate("hello -> zhs", toLanguage: "fr");

        Assert.HasCount(1, translator.Calls);
        AssertCall(translator.Calls[0], "hello", "fr", "auto");
    }

    [TestMethod]
    public void QueryTranslate_EmptyInputSkipsTranslation()
    {
        var translator = new FakeTranslator();
        var helper = CreateHelper([translator], defaultLanguage: "en");

        var results = helper.QueryTranslate(string.Empty);

        Assert.IsEmpty(results);
        Assert.IsEmpty(translator.Calls);
    }

    [TestMethod]
    public void QueryTranslate_UsesCambridgeFirstWhenEnabled()
    {
        var cambridge = new FakeTranslator
        {
            Result = new FakeTranslateResult(new ResultItem { Title = "蘋果", SubTitle = "apple" }),
        };
        var translator = new FakeTranslator();
        var helper = CreateHelper([translator], defaultLanguage: "zht", cambridge: cambridge, cambridgeEnabled: true);

        var results = helper.QueryTranslate("apple");

        Assert.HasCount(1, results);
        Assert.AreEqual("蘋果", results[0].Title);
        Assert.HasCount(1, cambridge.Calls);
        AssertCall(cambridge.Calls[0], "apple", "zh-Hant", "en");
        Assert.IsEmpty(translator.Calls);
    }

    [TestMethod]
    public void QueryTranslate_FallsBackWhenCambridgeReturnsNull()
    {
        var cambridge = new FakeTranslator();
        var translator = new FakeTranslator
        {
            Result = new FakeTranslateResult(new ResultItem { Title = "蘋果", SubTitle = "apple" }),
        };
        var helper = CreateHelper([translator], defaultLanguage: "zht", cambridge: cambridge, cambridgeEnabled: true);

        var results = helper.QueryTranslate("apple");

        Assert.HasCount(1, results);
        Assert.HasCount(1, cambridge.Calls);
        Assert.HasCount(1, translator.Calls);
    }

    [TestMethod]
    public void QueryTranslate_SkipsCambridgeWhenDisabled()
    {
        var cambridge = new FakeTranslator
        {
            Result = new FakeTranslateResult(new ResultItem { Title = "蘋果", SubTitle = "apple" }),
        };
        var translator = new FakeTranslator
        {
            Result = new FakeTranslateResult(new ResultItem { Title = "蘋果", SubTitle = "apple" }),
        };
        var helper = CreateHelper([translator], defaultLanguage: "zht", cambridge: cambridge, cambridgeEnabled: false);

        _ = helper.QueryTranslate("apple");

        Assert.IsEmpty(cambridge.Calls);
        Assert.HasCount(1, translator.Calls);
    }

    private static TranslateHelper CreateHelper(
        IEnumerable<ITranslator> translators,
        string defaultLanguage,
        bool codeModeEnabled = false,
        ITranslator? cambridge = null,
        bool cambridgeEnabled = false)
    {
        return new TranslateHelper(translators, defaultLanguage, () => codeModeEnabled, cambridge, () => cambridgeEnabled);
    }

    private static void AssertCall(FakeTranslator.Call call, string text, string targetLanguage, string sourceLanguage)
    {
        Assert.AreEqual(text, call.Text);
        Assert.AreEqual(targetLanguage, call.TargetLanguage);
        Assert.AreEqual(sourceLanguage, call.SourceLanguage);
    }

    private sealed class FakeTranslateResult(params ResultItem[] items) : ITranslateResult
    {
        public IEnumerable<ResultItem> Transform() => items;
    }

    private sealed class FakeTranslator : ITranslator
    {
        public sealed record Call(string Text, string TargetLanguage, string SourceLanguage);

        public List<Call> Calls { get; } = [];
        public ITranslateResult? Result { get; init; }
        public bool Inited => true;
        public string Name => "Fake";

        public void Init()
        {
        }

        public void Reset()
        {
        }

        public ITranslateResult? Translate(string src, string toLan, string fromLan)
        {
            Calls.Add(new Call(src, toLan, fromLan));
            return Result;
        }
    }
}
