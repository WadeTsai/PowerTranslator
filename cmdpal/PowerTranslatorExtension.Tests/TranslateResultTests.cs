using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerTranslatorExtension.Service.DeepL;

namespace PowerTranslatorExtension.Tests;

[TestClass]
public class TranslateResultTests
{
    [TestMethod]
    public void DeepLTransform_FlattensAllBeamsAndSentences()
    {
        var response = new DeepLTranslateResult
        {
            Result = new DeepLTranslateResult.InnerResult
            {
                SourceLang = "ZH",
                TargetLang = "EN",
                Translations =
                [
                    new DeepLTranslateResult.Translation
                    {
                        Beams =
                        [
                            new DeepLTranslateResult.Beams
                            {
                                Sentences =
                                [
                                    new DeepLTranslateResult.Sentence { Text = "hello" },
                                    new DeepLTranslateResult.Sentence { Text = "world" },
                                ],
                            },
                        ],
                    },
                ],
            },
        };

        var items = response.Transform()!.ToList();

        CollectionAssert.AreEqual(new[] { "hello", "world" }, items.Select(item => item.Title).ToArray());
        Assert.IsTrue(items.All(item => item.SubTitle == "ZH -> EN"));
        Assert.IsTrue(items.All(item => item.CopyTgt == item.Title));
        Assert.IsTrue(items.All(item => item.fromApiName == "DeepL"));
    }
}
