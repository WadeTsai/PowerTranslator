using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerTranslatorExtension.Middleware.Alias;
using PowerTranslatorExtension.Middleware.LanguageCodeHelper;

namespace PowerTranslatorExtension.Tests;

[TestClass]
public class MiddlewareTests
{
    [TestMethod]
    public void CultureAliasMiddleware_ReturnsAliasOrOriginalValue()
    {
        var middleware = new CultureAliasMiddleware(new Dictionary<string, string>
        {
            ["zhs"] = "zh-Hans",
        });

        Assert.AreEqual("zh-Hans", middleware.GetCultureFromAlias("zhs"));
        Assert.AreEqual("en", middleware.GetCultureFromAlias("en"));
    }

    [TestMethod]
    public void DiffCultureLanguageCodeMiddleware_AppliesBaseLanguageOverride()
    {
        var middleware = new DiffCultureLanguageCodeMiddleware(new Dictionary<string, string>
        {
            ["pt"] = "pt-PT",
        });

        Assert.AreEqual("pt-PT", middleware.GetLanguageCode("pt-BR"));
        Assert.AreEqual("fr", middleware.GetLanguageCode("fr-CA"));
    }
}
