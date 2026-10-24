using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PowerTranslatorExtension.Protocol;
using PowerTranslatorExtension.Service.Ecdict;
using PowerTranslatorExtension.Service.FreeDictionary;
using PowerTranslatorExtension.Utils;

namespace PowerTranslatorExtension.Service.Dictionary;

public sealed class WordLookupResult : ITranslateResult
{
    public const string EcdictApiName = "ECDICT";
    public const string FreeDictionaryApiName = "Free Dictionary API";
    public const int MaxEnglishSenses = 6;

    public string Query { get; init; } = string.Empty;
    /// <summary>ECDICT entries for the query (exact spelling first); empty when not found or not requested.</summary>
    public IReadOnlyList<EcdictEntry> Entries { get; init; } = [];
    /// <summary>The ECDICT entry of the base form when the query is an inflection (went -> go).</summary>
    public EcdictEntry? LemmaEntry { get; init; }
    public EnglishDefinition? English { get; init; }
    /// <summary>English target: show English definitions only (Free Dictionary, else ECDICT's offline ones).</summary>
    public bool EnglishOnly { get; init; }

    private static readonly Dictionary<char, string> inflectionNames = new()
    {
        ['p'] = "過去式",
        ['d'] = "過去分詞",
        ['i'] = "現在分詞",
        ['3'] = "第三人稱單數",
        ['r'] = "比較級",
        ['t'] = "最高級",
        ['s'] = "複數",
    };

    public static string DescribeInflection(string types) =>
        string.Join("、", types.Select(t => inflectionNames.GetValueOrDefault(t)).Where(n => n != null));

    /// <summary>Up to two senses per part of speech first, so verbs are not hidden behind many noun senses.</summary>
    public static IEnumerable<EnglishSense> PickSenses(IReadOnlyList<EnglishSense> senses)
    {
        var picked = senses
            .Select((sense, idx) => (sense, idx))
            .GroupBy(x => x.sense.PartOfSpeech)
            .SelectMany(g => g.Take(2))
            .Take(MaxEnglishSenses)
            .ToList();
        var rest = senses.Select((sense, idx) => (sense, idx)).Where(x => !picked.Contains(x));
        return picked.Concat(rest).Take(MaxEnglishSenses).OrderBy(x => x.idx).Select(x => x.sense);
    }

    // ECDICT definitions start with a WordNet part-of-speech tag: n. / v / a. / s. (satellite adjective) / r. (adverb).
    private static readonly Dictionary<string, string> wordNetPartsOfSpeech = new()
    {
        ["n"] = "noun",
        ["v"] = "verb",
        ["a"] = "adjective",
        ["s"] = "adjective",
        ["r"] = "adverb",
        ["adj"] = "adjective",
        ["adv"] = "adverb",
    };

    /// <summary>Splits "n. an expression of greeting" into ("noun", "an expression of greeting").</summary>
    public static (string PartOfSpeech, string Definition) ParseEcdictDefinition(string line)
    {
        var space = line.IndexOf(' ');
        if (space > 0 && wordNetPartsOfSpeech.TryGetValue(line[..space].TrimEnd('.'), out var pos))
            return (pos, line[(space + 1)..].Trim());
        return (string.Empty, line);
    }

    /// <summary>ECDICT's offline English definitions, used when the online dictionary has no answer.</summary>
    private IEnumerable<ResultItem> TransformEcdictDefinitions()
    {
        var main = Entries.Count > 0 ? Entries[0] : null;
        var source = main is { Definitions.Count: > 0 } ? main : LemmaEntry is { Definitions.Count: > 0 } ? LemmaEntry : null;
        if (source == null)
            yield break;
        var phonetic = source.Phonetic.Length > 0 ? $" /{source.Phonetic}/" : string.Empty;
        var head = source == main ? $"{source.Word}{phonetic}" : $"{main!.Word} → {source.Word}{phonetic}";
        foreach (var line in source.Definitions.Take(MaxEnglishSenses))
        {
            var (pos, definition) = ParseEcdictDefinition(line);
            yield return new ResultItem
            {
                Title = definition,
                SubTitle = pos.Length > 0 ? $"[{pos}] {head} · {EcdictApiName}" : $"{head} · {EcdictApiName}",
                CopyTgt = definition,
                fromApiName = EcdictApiName,
                ReadText = source.Word,
            };
        }
    }

    public IEnumerable<ResultItem>? Transform()
    {
        var res = new List<ResultItem>();
        var main = !EnglishOnly && Entries.Count > 0 ? Entries[0] : null;
        if (EnglishOnly && English == null)
            res.AddRange(TransformEcdictDefinitions());
        if (main != null)
        {
            var phonetic = English?.Phonetic ?? (main.Phonetic.Length > 0 ? main.Phonetic : null);
            var head = phonetic == null ? main.Word : $"{main.Word} /{phonetic}/";
            res.Add(new ResultItem
            {
                Title = main.Summary,
                SubTitle = $"{head} · {EcdictApiName}",
                CopyTgt = main.Summary,
                fromApiName = EcdictApiName,
                ReadText = main.Word,
            });
            if (main.Translations.Count > 1)
            {
                res.AddRange(main.Translations.Select(t => new ResultItem
                {
                    Title = t,
                    SubTitle = main.Word,
                    fromApiName = EcdictApiName,
                    ReadText = main.Word,
                }));
            }
            if (LemmaEntry != null)
            {
                var forms = DescribeInflection(main.InflectionTypes);
                res.Add(new ResultItem
                {
                    Title = LemmaEntry.Summary,
                    SubTitle = forms.Length > 0
                        ? $"{main.Word} → {LemmaEntry.Word}（{forms}）· {EcdictApiName}"
                        : $"{main.Word} → {LemmaEntry.Word} · {EcdictApiName}",
                    fromApiName = EcdictApiName,
                    ReadText = LemmaEntry.Word,
                });
            }
            foreach (var other in Entries.Skip(1).Take(2))
            {
                res.Add(new ResultItem
                {
                    Title = other.Summary,
                    SubTitle = $"{other.Word} · {EcdictApiName}",
                    fromApiName = EcdictApiName,
                    ReadText = other.Word,
                });
            }
        }

        if (English != null)
        {
            var first = main == null;
            foreach (var sense in PickSenses(English.Senses))
            {
                var pos = string.IsNullOrEmpty(sense.PartOfSpeech) ? string.Empty : $"[{sense.PartOfSpeech}] ";
                var word = first && English.Phonetic != null ? $"{English.Word} /{English.Phonetic}/" : English.Word;
                res.Add(new ResultItem
                {
                    Title = sense.Definition,
                    SubTitle = $"{pos}{word} · {FreeDictionaryApiName}",
                    Description = sense.Example,
                    fromApiName = FreeDictionaryApiName,
                    ReadText = English.Word,
                });
                first = false;
            }
        }
        return res.Count > 0 ? res : null;
    }
}

/// <summary>
/// Looks up English words and short phrases: Traditional Chinese translations come from the
/// bundled ECDICT dictionary (offline) and English definitions from dictionaryapi.dev.
/// Returns null for anything else so the caller falls back to the sentence translator.
/// </summary>
public sealed partial class WordLookupTranslator : ITranslator, IDisposable
{
    private static readonly TimeSpan englishWaitWithChinese = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan englishWaitAlone = TimeSpan.FromSeconds(4);

    private readonly Func<EcdictDictionary?> dictionary;
    private readonly FreeDictionaryClient? ownedClient;
    private readonly Func<string, CancellationToken, Task<EnglishDefinition?>>? englishLookup;
    private readonly Func<bool> isEnglishEnabled;

    public bool Inited { get; private set; }
    public string Name => "ECDICT + Free Dictionary";

    public WordLookupTranslator(Func<bool> isEnglishEnabled)
    {
        ownedClient = new FreeDictionaryClient();
        dictionary = () => EcdictDictionary.Shared;
        englishLookup = ownedClient.LookupAsync;
        this.isEnglishEnabled = isEnglishEnabled;
    }

    internal WordLookupTranslator(
        EcdictDictionary? dictionary,
        Func<string, CancellationToken, Task<EnglishDefinition?>>? englishLookup,
        Func<bool>? isEnglishEnabled = null)
    {
        this.dictionary = () => dictionary;
        this.englishLookup = englishLookup;
        this.isEnglishEnabled = isEnglishEnabled ?? (() => englishLookup != null);
    }

    public void Init()
    {
        // Opening the bundled dictionary is cheap; dictionaryapi.dev needs no handshake.
        if (dictionary() == null)
            UtilsFun.LogMessage($"ECDICT data not found at {EcdictDictionary.DefaultPath}; only English definitions are available.");
        Inited = true;
    }

    public void Reset()
    {
        ownedClient?.Reset();
    }

    public static bool IsEnglishWord(string src) => EnglishWordRegex().IsMatch(src.Trim());

    public static bool WantsChinese(string toLan)
    {
        var target = toLan.Trim();
        return target.Equals("auto", StringComparison.OrdinalIgnoreCase)
            || target.Equals("zh-Hant", StringComparison.OrdinalIgnoreCase)
            || target.Equals("zht", StringComparison.OrdinalIgnoreCase)
            || target.Equals("zh-TW", StringComparison.OrdinalIgnoreCase)
            || target.Equals("zh-HK", StringComparison.OrdinalIgnoreCase);
    }

    public static bool WantsEnglish(string toLan) =>
        toLan.Trim().Equals("en", StringComparison.OrdinalIgnoreCase)
        || toLan.Trim().StartsWith("en-", StringComparison.OrdinalIgnoreCase);

    public static bool IsApplicable(string src, string toLan) =>
        IsEnglishWord(src) && (WantsChinese(toLan) || WantsEnglish(toLan));

    public ITranslateResult? Translate(string src, string toLan, string fromLan)
    {
        if (!IsApplicable(src, toLan))
            return null;
        var word = src.Trim();
        var wantChinese = WantsChinese(toLan);

        using var cts = new CancellationTokenSource();
        Task<EnglishDefinition?>? englishTask = null;
        if (englishLookup != null && isEnglishEnabled())
            englishTask = SafeLookup(word, cts.Token);

        IReadOnlyList<EcdictEntry> entries = [];
        EcdictEntry? lemmaEntry = null;
        // English target: ECDICT still supplies the base form and offline definitions.
        var dict = dictionary();
        if (dict != null)
        {
            entries = dict.Lookup(word);
            if (entries.Count > 0 && entries[0].Lemma is { } lemma)
                lemmaEntry = dict.Find(lemma);
        }

        EnglishDefinition? english = null;
        if (englishTask != null)
        {
            var wait = entries.Count > 0 ? englishWaitWithChinese : englishWaitAlone;
            if (englishTask.Wait(wait))
                english = englishTask.Result;
            else
                cts.Cancel();
            // dictionaryapi.dev has no entries for most inflections; retry with the base form.
            var lemma = entries.Count > 0 ? entries[0].Lemma : null;
            if (english == null && lemma != null && englishTask.IsCompleted)
            {
                var lemmaTask = SafeLookup(lemma, cts.Token);
                if (lemmaTask.Wait(englishWaitWithChinese))
                    english = lemmaTask.Result;
            }
        }

        var result = new WordLookupResult
        {
            Query = word,
            Entries = entries,
            LemmaEntry = lemmaEntry,
            English = english,
            EnglishOnly = !wantChinese,
        };
        // Nothing to show: let the sentence translators take over.
        return result.Transform() == null ? null : result;
    }

    private async Task<EnglishDefinition?> SafeLookup(string word, CancellationToken cancellationToken)
    {
        try
        {
            return await englishLookup!(word, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
                UtilsFun.LogMessage($"Free Dictionary: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        ownedClient?.Dispose();
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z'\-\.]*(?: [A-Za-z'\-\.]+){0,3}$")]
    private static partial Regex EnglishWordRegex();
}
