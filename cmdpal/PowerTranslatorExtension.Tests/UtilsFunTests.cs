using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerTranslatorExtension.Utils;

namespace PowerTranslatorExtension.Tests;

[TestClass]
public class UtilsFunTests
{
    private sealed class JsonValue
    {
        public int Value { get; set; }
    }

    [TestMethod]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow("hello", true)]
    [DataRow("hello world", true)]
    [DataRow("C:\\temp", false)]
    [DataRow("path/to/file", false)]
    public void WhetherTranslate_FiltersEmptyValuesAndPaths(string? input, bool expected)
    {
        Assert.AreEqual(expected, UtilsFun.WhetherTranslate(input));
    }

    [TestMethod]
    public void ToEnPunctuation_ReplacesSupportedChinesePunctuation()
    {
        Assert.AreEqual("one,two;three(four)", "one，two；three（four）".ToEnPunctuation());
    }

    [TestMethod]
    public void ParseJson_ReturnsValueForValidJson()
    {
        var result = UtilsFun.ParseJson<JsonValue>("{\"Value\":42}");

        Assert.IsNotNull(result);
        Assert.AreEqual(42, result.Value);
    }

    [TestMethod]
    public void ParseJson_ReturnsNullForInvalidJson()
    {
        Assert.IsNull(UtilsFun.ParseJson<JsonValue>("not-json"));
    }

    [TestMethod]
    public void ToFormString_JoinsPairsInOrder()
    {
        KeyValuePair<string, string>[] pairs = [new("Query", "hello"), new("Target", "en")];

        Assert.AreEqual("Query=hello&Target=en", pairs.ToFormString());
    }

    [TestMethod]
    public void ToFormString_EncodesReservedCharacters()
    {
        KeyValuePair<string, string>[] pairs = [new("q", "rock & roll=1+1"), new("kw", "你好")];

        Assert.AreEqual("q=rock%20%26%20roll%3D1%2B1&kw=%E4%BD%A0%E5%A5%BD", pairs.ToFormString());
    }

    [TestMethod]
    public async Task ToFormContent_UsesFormUrlEncodedContentType()
    {
        KeyValuePair<string, string>[] pairs = [new("a", "b c")];

        using var content = pairs.ToFormContent();

        Assert.AreEqual("application/x-www-form-urlencoded", content.Headers.ContentType?.MediaType);
        Assert.AreEqual("a=b%20c", await content.ReadAsStringAsync());
    }

    [TestMethod]
    public void FirstNotNoneCast_ReturnsFirstNonNullProjection()
    {
        var result = new[] { "", "first", "second" }
            .FirstNotNoneCast<string, string>(value => string.IsNullOrEmpty(value) ? null : value);

        Assert.AreEqual("first", result);
    }

    [TestMethod]
    public void Enumerate_AddsZeroBasedIndexes()
    {
        var result = new[] { "a", "b", "c" }.Enumerate().ToArray();

        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, result.Select(value => value.idx).ToArray());
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, result.Select(value => value.item).ToArray());
    }

    [TestMethod]
    [DataRow("camelCase", "camel Case")]
    [DataRow("PascalCase", "Pascal Case")]
    [DataRow("snake_case", "snake case")]
    [DataRow("already spaced", "already spaced")]
    public void ConvertSnakeCaseOrCamelCaseToNormalSpace_ConvertsIdentifiers(string input, string expected)
    {
        Assert.AreEqual(expected, UtilsFun.ConvertSnakeCaseOrCamelCaseToNormalSpace(input));
    }

    [TestMethod]
    public void RemoveSpecialCharacter_PreservesWordsWhitespaceAndChinese()
    {
        Assert.AreEqual("hello  世界 42", "hello, 世界!42".removeSpecialCharacter());
    }
}
