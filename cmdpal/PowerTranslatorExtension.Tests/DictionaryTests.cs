using System.Buffers.Binary;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerTranslatorExtension.Service.Dictionary;
using PowerTranslatorExtension.Service.Ecdict;
using PowerTranslatorExtension.Service.FreeDictionary;
using PowerTranslatorExtension.Suggest;

namespace PowerTranslatorExtension.Tests;

/// <summary>Writes a small file in the same format as tools/ecdict/build_ecdict.py.</summary>
internal static class EcdictFixture
{
    // word, phonetic, translation (lines joined by "\n"), exchange, tag, frq[, definition]
    private static readonly string[][] Rows =
    [
        ["apple", "'æpl", "n. 蘋果, 傢伙\\n[醫] 蘋果", "s:apples", "zk gk", "2695", "n. fruit with red or yellow or green skin\\nn. native Eurasian tree"],
        ["application", ".æpli'keiʃәn", "n. 應用, 申請", "s:applications", "cet4", "700"],
        ["apply", "ә'plai", "v. 申請, 應用", "p:applied/d:applied/3:applies/i:applying", "zk", "500"],
        ["appeal", "ә'pi:l", "n. 呼籲, 上訴", "", "", "1200"],
        ["go", "gәu", "v. 去, 走", "p:went/d:gone/i:going/3:goes", "zk", "50", "v change location; move, travel, or proceed"],
        ["went", "went", "go的過去式", "0:go/1:p", "", "0"],
        ["US", "", "[經] 美國", "", "", "0"],
        ["us", "ʌs", "pron. 我們", "", "zk", "113"],
        ["take off", "", "脫下, 起飛", "", "", "0"],
    ];

    public static string Create()
    {
        var records = Rows
            .Select(r => (Key: EcdictDictionary.NormalizeKey(r[0]), Row: r))
            .Select(x => (KeyBytes: Encoding.UTF8.GetBytes(x.Key), WordBytes: Encoding.UTF8.GetBytes(x.Row[0]),
                Data: Encoding.UTF8.GetBytes(string.Join('\x1f', new[] { x.Key }.Concat(x.Row)))))
            .OrderBy(r => r.KeyBytes, ByteComparer.Instance)
            .ThenBy(r => r.WordBytes, ByteComparer.Instance)
            .ToList();

        var path = Path.Combine(Path.GetTempPath(), $"ecdict-test-{Guid.NewGuid():N}.bin");
        using var f = File.Create(path);
        f.Write("PTECD001"u8);
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, records.Count);
        f.Write(buf);
        uint offset = 0;
        foreach (var r in records)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buf, offset);
            f.Write(buf);
            offset += (uint)r.Data.Length;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(buf, offset);
        f.Write(buf);
        foreach (var r in records)
            f.Write(r.Data);
        return path;
    }

    private sealed class ByteComparer : IComparer<byte[]>
    {
        public static readonly ByteComparer Instance = new();

        public int Compare(byte[]? x, byte[]? y) => x.AsSpan().SequenceCompareTo(y.AsSpan());
    }
}

[TestClass]
public class EcdictDictionaryTests
{
    private static string path = string.Empty;
    private static EcdictDictionary dict = null!;

    [ClassInitialize]
    public static void Setup(TestContext _)
    {
        path = EcdictFixture.Create();
        dict = EcdictDictionary.Open(path);
    }

    [ClassCleanup]
    public static void Cleanup()
    {
        dict.Dispose();
        File.Delete(path);
    }

    [TestMethod]
    public void Lookup_FindsWordCaseInsensitivelyAndSplitsTranslations()
    {
        var entry = dict.Find("  Apple ")!;

        Assert.AreEqual("apple", entry.Word);
        Assert.AreEqual("'æpl", entry.Phonetic);
        CollectionAssert.AreEqual(new[] { "n. 蘋果, 傢伙", "[醫] 蘋果" }, entry.Translations.ToArray());
        Assert.AreEqual("n. 蘋果, 傢伙；[醫] 蘋果", entry.Summary);
        Assert.AreEqual(2695, entry.Frequency);
    }

    [TestMethod]
    public void Lookup_PrefersExactCaseThenLowerCase()
    {
        CollectionAssert.AreEqual(new[] { "US", "us" }, dict.Lookup("US").Select(e => e.Word).ToArray());
        CollectionAssert.AreEqual(new[] { "us", "US" }, dict.Lookup("Us").Select(e => e.Word).ToArray());
    }

    [TestMethod]
    public void Lookup_HandlesPhrasesAndMissingWords()
    {
        Assert.AreEqual("脫下, 起飛", dict.Find("take   off")!.Summary);
        Assert.IsNull(dict.Find("zzz"));
        Assert.IsNull(dict.Find("appl"));
        Assert.IsNull(dict.Find(""));
    }

    [TestMethod]
    public void Entry_ExposesLemmaOfInflections()
    {
        var went = dict.Find("went")!;

        Assert.AreEqual("go", went.Lemma);
        Assert.AreEqual("p", went.InflectionTypes);
        Assert.IsNull(dict.Find("go")!.Lemma);
    }

    [TestMethod]
    public void Suggest_ReturnsPrefixMatchesByFrequency()
    {
        CollectionAssert.AreEqual(
            new[] { "apply", "application", "appeal", "apple" },
            dict.Suggest("app", limit: 10).Select(e => e.Word).ToArray());
        Assert.HasCount(2, dict.Suggest("app", limit: 2));
        Assert.IsEmpty(dict.Suggest("xyz"));
    }

    [TestMethod]
    public void Open_RejectsUnknownFiles()
    {
        var bogus = Path.GetTempFileName();
        try
        {
            File.WriteAllText(bogus, "not a dictionary");
            Assert.ThrowsExactly<InvalidDataException>(() => EcdictDictionary.Open(bogus));
            Assert.IsNull(EcdictDictionary.TryOpen(bogus));
            Assert.IsNull(EcdictDictionary.TryOpen(bogus + ".missing"));
        }
        finally
        {
            File.Delete(bogus);
        }
    }
}

[TestClass]
public class FreeDictionaryTests
{
    private const string HelloJson = """
        [{"word":"hello","phonetic":"/həˈləʊ/","phonetics":[{"text":"/həˈləʊ/","audio":""}],
          "meanings":[
            {"partOfSpeech":"noun","definitions":[{"definition":"\"Hello!\" or an equivalent greeting.","synonyms":[]}],"synonyms":["greeting"]},
            {"partOfSpeech":"verb","definitions":[{"definition":"To greet with \"hello\".","example":"I helloed him."}]}]}]
        """;

    [TestMethod]
    public void Parse_FlattensMeaningsAndStripsPhoneticSlashes()
    {
        var def = FreeDictionaryClient.Parse(HelloJson)!;

        Assert.AreEqual("hello", def.Word);
        Assert.AreEqual("həˈləʊ", def.Phonetic);
        Assert.HasCount(2, def.Senses);
        Assert.AreEqual(new EnglishSense("verb", "To greet with \"hello\".", "I helloed him."), def.Senses[1]);
    }

    [TestMethod]
    public void Parse_ReturnsNullForErrorsAndEmptyResults()
    {
        Assert.IsNull(FreeDictionaryClient.Parse("""{"title":"No Definitions Found"}"""));
        Assert.IsNull(FreeDictionaryClient.Parse("[]"));
        Assert.IsNull(FreeDictionaryClient.Parse("not json"));
    }

    [TestMethod]
    public void BuildUrl_EscapesWord()
    {
        Assert.AreEqual("https://api.dictionaryapi.dev/api/v2/entries/en/take%20off", FreeDictionaryClient.BuildUrl(" Take off "));
    }
}

[TestClass]
public class WordLookupTranslatorTests
{
    private static string path = string.Empty;
    private static EcdictDictionary dict = null!;

    private static readonly EnglishDefinition AppleDefinition = new()
    {
        Word = "apple",
        Phonetic = "ˈæp.əl",
        Senses = [new EnglishSense("noun", "A common, round fruit.", "An apple a day.")],
    };

    [ClassInitialize]
    public static void Setup(TestContext _)
    {
        path = EcdictFixture.Create();
        dict = EcdictDictionary.Open(path);
    }

    [ClassCleanup]
    public static void Cleanup()
    {
        dict.Dispose();
        File.Delete(path);
    }

    private static WordLookupTranslator Create(Func<string, EnglishDefinition?>? english, List<string>? calls = null) =>
        new(dict, english == null ? null : (word, _) =>
        {
            calls?.Add(word);
            return Task.FromResult(english(word));
        });

    [TestMethod]
    [DataRow("apple", "auto", true)]
    [DataRow("apple", "zh-Hant", true)]
    [DataRow("take off", "zht", true)]
    [DataRow("apple", "en", true)]
    [DataRow("apple", "zh-Hans", false)]
    [DataRow("apple", "ja", false)]
    [DataRow("蘋果", "auto", false)]
    [DataRow("this is a long sentence", "auto", false)]
    public void IsApplicable_OnlyForEnglishWordsToTraditionalChineseOrEnglish(string src, string toLan, bool expected)
    {
        Assert.AreEqual(expected, WordLookupTranslator.IsApplicable(src, toLan));
    }

    [TestMethod]
    public void Translate_CombinesEcdictAndEnglishDefinitions()
    {
        var items = Create(w => string.Equals(w, "apple", StringComparison.OrdinalIgnoreCase) ? AppleDefinition : null).Translate("Apple", "auto", "auto")!.Transform()!.ToList();

        Assert.AreEqual("n. 蘋果, 傢伙；[醫] 蘋果", items[0].Title);
        Assert.AreEqual("apple /ˈæp.əl/ · ECDICT", items[0].SubTitle);
        Assert.AreEqual("ECDICT", items[0].fromApiName);
        Assert.AreEqual("n. 蘋果, 傢伙", items[1].Title);
        Assert.AreEqual("[醫] 蘋果", items[2].Title);
        Assert.AreEqual("A common, round fruit.", items[3].Title);
        Assert.AreEqual("[noun] apple · Free Dictionary API", items[3].SubTitle);
        Assert.AreEqual("An apple a day.", items[3].Description);
        Assert.HasCount(4, items);
    }

    [TestMethod]
    public void Translate_WorksOfflineWithEcdictOnly()
    {
        var items = Create(english: null).Translate("apple", "zh-Hant", "auto")!.Transform()!.ToList();

        Assert.AreEqual("apple /'æpl/ · ECDICT", items[0].SubTitle);
        Assert.IsTrue(items.All(i => i.fromApiName == "ECDICT"));
    }

    [TestMethod]
    public void Translate_ShowsLemmaForInflections()
    {
        var calls = new List<string>();
        var items = Create(w => null, calls).Translate("went", "auto", "auto")!.Transform()!.ToList();

        Assert.AreEqual("go的過去式", items[0].Title);
        Assert.AreEqual("v. 去, 走", items[1].Title);
        Assert.AreEqual("went → go（過去式）· ECDICT", items[1].SubTitle);
        CollectionAssert.AreEqual(new[] { "went", "go" }, calls);
    }

    [TestMethod]
    public void Translate_ReadsTheEnglishWordNotTheChinese()
    {
        var apple = Create(w => string.Equals(w, "apple", StringComparison.OrdinalIgnoreCase) ? AppleDefinition : null).Translate("Apple", "auto", "auto")!.Transform()!.ToList();
        Assert.IsTrue(apple.All(i => i.ReadText == "apple"));

        var went = Create(w => null).Translate("went", "auto", "auto")!.Transform()!.ToList();
        Assert.AreEqual("went", went[0].ReadText);
        Assert.AreEqual("go", went[1].ReadText);
    }

    [TestMethod]
    public void Translate_EnglishTargetSkipsChinese()
    {
        var items = Create(w => AppleDefinition).Translate("apple", "en", "auto")!.Transform()!.ToList();

        Assert.HasCount(1, items);
        Assert.AreEqual("[noun] apple /ˈæp.əl/ · Free Dictionary API", items[0].SubTitle);
    }

    [TestMethod]
    public void Translate_EnglishTargetFallsBackToEcdictDefinitions()
    {
        var items = Create(w => null).Translate("apple", "en", "auto")!.Transform()!.ToList();

        Assert.HasCount(2, items);
        Assert.AreEqual("fruit with red or yellow or green skin", items[0].Title);
        Assert.AreEqual("[noun] apple /'æpl/ · ECDICT", items[0].SubTitle);
        Assert.AreEqual("native Eurasian tree", items[1].Title);
        Assert.IsTrue(items.All(i => i.fromApiName == "ECDICT"));
    }

    [TestMethod]
    public void Translate_EnglishTargetWorksWithEnglishDefinitionsDisabled()
    {
        var items = Create(english: null).Translate("apple", "en", "auto")!.Transform()!.ToList();

        Assert.AreEqual("fruit with red or yellow or green skin", items[0].Title);
    }

    [TestMethod]
    public void Translate_EnglishTargetUsesLemmaDefinitionsForInflections()
    {
        var items = Create(w => null).Translate("went", "en", "auto")!.Transform()!.ToList();

        Assert.HasCount(1, items);
        Assert.AreEqual("change location; move, travel, or proceed", items[0].Title);
        Assert.AreEqual("[verb] went → go /gәu/ · ECDICT", items[0].SubTitle);
    }

    [TestMethod]
    public void Translate_EnglishTargetReturnsNullWithoutAnyDefinition()
    {
        Assert.IsNull(Create(w => null).Translate("appeal", "en", "auto"));
    }

    [TestMethod]
    [DataRow("n. an expression of greeting", "noun", "an expression of greeting")]
    [DataRow("v change location", "verb", "change location")]
    [DataRow("a. delighting the senses", "adjective", "delighting the senses")]
    [DataRow("s. (of weather) highly enjoyable", "adjective", "(of weather) highly enjoyable")]
    [DataRow("r. quickly", "adverb", "quickly")]
    [DataRow("adv. quickly", "adverb", "quickly")]
    [DataRow("plural of apple", "", "plural of apple")]
    public void ParseEcdictDefinition_SplitsWordNetPartOfSpeech(string line, string pos, string definition)
    {
        Assert.AreEqual((pos, definition), WordLookupResult.ParseEcdictDefinition(line));
    }

    [TestMethod]
    public void Translate_ReturnsNullWhenNothingFoundSoDeepLCanTakeOver()
    {
        Assert.IsNull(Create(w => null).Translate("qwertyuiop", "auto", "auto"));
        Assert.IsNull(Create(w => AppleDefinition).Translate("a whole sentence here now", "auto", "auto"));
    }

    [TestMethod]
    public void Translate_IgnoresEnglishLookupFailures()
    {
        var translator = new WordLookupTranslator(dict, (_, _) => Task.FromException<EnglishDefinition?>(new HttpRequestException("offline")));

        var items = translator.Translate("apple", "auto", "auto")!.Transform()!.ToList();

        Assert.IsTrue(items.All(i => i.fromApiName == "ECDICT"));
    }

    [TestMethod]
    public void SuggestHelper_ReturnsCompletionsWithoutTheQueryItself()
    {
        var suggest = new SuggestHelper(() => dict);

        var items = suggest.QuerySuggest("apply");
        Assert.IsEmpty(items);

        items = suggest.QuerySuggest("App");
        CollectionAssert.AreEqual(new[] { "apply", "application", "appeal", "apple" }, items.Select(i => i.SubTitle).ToArray());
        Assert.AreEqual("v. 申請, 應用", items[0].Title);
        Assert.IsEmpty(suggest.QuerySuggest("蘋果"));
        Assert.IsEmpty(new SuggestHelper(() => null).QuerySuggest("app"));
    }
}

/// <summary>Sanity checks against the real data/ecdict.bin when it was generated before the build.</summary>
[TestClass]
public class BundledEcdictTests
{
    [TestMethod]
    public void BundledDictionary_IsTraditionalChinese()
    {
        var dict = EcdictDictionary.Shared;
        if (dict == null)
        {
            Assert.Inconclusive("data/ecdict.bin was not generated; run tools/ecdict/build_ecdict.py");
            return;
        }

        Assert.IsGreaterThan(500_000, dict.Count);
        StringAssert.Contains(dict.Find("software")!.Summary, "軟體");
        StringAssert.Contains(dict.Find("network")!.Summary, "網路");
        Assert.AreEqual("go", dict.Find("went")!.Lemma);
        foreach (var word in new[] { "software", "information", "network", "language", "computer" })
        {
            var summary = dict.Find(word)!.Summary;
            // Characters that only exist in Simplified Chinese.
            foreach (var simplified in "软网络语计机讯")
                Assert.DoesNotContain(simplified.ToString(), summary, $"{word}: {summary}");
        }
    }
}

[TestClass]
public class WordLookupResultTests
{
    [TestMethod]
    public void PickSenses_KeepsEveryPartOfSpeechVisible()
    {
        var senses = Enumerable.Range(1, 8).Select(i => new EnglishSense("noun", $"n{i}", null))
            .Append(new EnglishSense("verb", "v1", null))
            .ToList();

        var picked = WordLookupResult.PickSenses(senses).Select(s => s.Definition).ToArray();

        CollectionAssert.AreEqual(new[] { "n1", "n2", "n3", "n4", "n5", "v1" }, picked);
    }
}
