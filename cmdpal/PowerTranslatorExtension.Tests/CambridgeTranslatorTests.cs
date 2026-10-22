using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerTranslatorExtension.Service.Cambridge;

namespace PowerTranslatorExtension.Tests;

[TestClass]
public class CambridgeTranslatorTests
{
    private const string SampleHtml = """
        <div class="page">
        <div class="pr entry-body__el">
          <div class="pos-header dpos-h">
            <div class="di-title"><span class="headword hdb tw-bw dhw dpos-h_hw "><span class="hw dhw">apple</span></span></div>
            <div class="posgram dpos-g hdib lmr-5"><span class="pos dpos" title="A word that refers to a person, place, idea, event or thing.">noun</span></div>
            <span class="uk dpron-i "><span class="region dreg">uk</span><span class="pron dpron">/<span class="ipa dipa lpr-2 lpl-1">ˈæp.<span class="sp dsp">ə</span>l</span>/</span></span>
          </div>
          <div class="pos-body">
            <div class="def-block ddef_block " data-wl-senseid="ID_00000001_01">
              <div class="ddef_h"><div class="def ddef_d db">a round fruit with firm, white flesh and a green, red, or yellow <a class="query" href="#">skin</a> </div></div>
              <div class="def-body ddef_b"><span class="trans dtrans dtrans-se  break-cj" lang="zh-Hant">蘋果</span>
                <div class="examp dexamp"><span class="eg deg">to peel an apple</span><span class="trans dtrans dtrans-se hdb break-cj" lang="zh-Hant">削蘋果皮</span></div>
              </div>
            </div>
          </div>
        </div>
        <div class="pr entry-body__el">
          <div class="posgram dpos-g hdib lmr-5"><span class="pos dpos" title="">adjective</span></div>
          <div class="def-block ddef_block ">
            <div class="def ddef_d db">made with apples &amp; sugar: </div>
            <div class="def-body ddef_b"><span class="trans dtrans dtrans-se  break-cj" lang="zh-Hant">蘋果做的</span></div>
          </div>
        </div>
        </div>
        """;

    [TestMethod]
    public void Parse_ExtractsHeadwordPhoneticAndSenses()
    {
        var result = CambridgeTranslator.Parse(SampleHtml, "Apple");

        Assert.AreEqual("apple", result.Word);
        Assert.AreEqual("ˈæp.əl", result.Phonetic);
        Assert.HasCount(2, result.Senses);
        Assert.AreEqual("noun", result.Senses[0].PartOfSpeech);
        Assert.AreEqual("a round fruit with firm, white flesh and a green, red, or yellow skin", result.Senses[0].Definition);
        Assert.AreEqual("蘋果", result.Senses[0].Translation);
        Assert.AreEqual("adjective", result.Senses[1].PartOfSpeech);
        Assert.AreEqual("made with apples & sugar", result.Senses[1].Definition);
        Assert.AreEqual("蘋果做的", result.Senses[1].Translation);
    }

    [TestMethod]
    public void Parse_ReturnsNoSensesForChallengePage()
    {
        var result = CambridgeTranslator.Parse("<html><title>Just a moment...</title></html>", "apple");

        Assert.IsEmpty(result.Senses);
        Assert.IsNull(result.Transform());
    }

    [TestMethod]
    public void Transform_PutsSummaryFirstThenEachSense()
    {
        var items = CambridgeTranslator.Parse(SampleHtml, "apple").Transform()!.ToList();

        Assert.HasCount(3, items);
        Assert.AreEqual("蘋果; 蘋果做的", items[0].Title);
        Assert.AreEqual("apple(/ˈæp.əl/) [noun, adjective] Cambridge", items[0].SubTitle);
        Assert.AreEqual("蘋果", items[1].Title);
        Assert.AreEqual("[noun] a round fruit with firm, white flesh and a green, red, or yellow skin", items[1].SubTitle);
        Assert.IsTrue(items.All(i => i.fromApiName == "Cambridge Dictionary"));
    }

    [TestMethod]
    [DataRow("apple", "zh-Hant", true)]
    [DataRow("look up", "auto", true)]
    [DataRow("well-known", "zht", true)]
    [DataRow("apple", "zh-Hans", false)]
    [DataRow("apple", "ja", false)]
    [DataRow("蘋果", "zh-Hant", false)]
    [DataRow("this sentence has far too many words", "zh-Hant", false)]
    public void IsApplicable_OnlyEnglishWordsToTraditionalOrAuto(string src, string toLan, bool expected)
    {
        Assert.AreEqual(expected, CambridgeTranslator.IsApplicable(src, toLan));
    }

    [TestMethod]
    public void BuildUrl_UsesLowercaseHyphenatedSlug()
    {
        Assert.AreEqual(
            "https://dictionary.cambridge.org/dictionary/english-chinese-traditional/look-up",
            CambridgeTranslator.BuildUrl(" Look  Up "));
    }
}
