using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Translator.Protocol;
using Translator.Utils;
using Wox.Plugin.Logger;

namespace Translator.Service.Cambridge;

public class CambridgeSense
{
    public string PartOfSpeech { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
}

public class TranslateResult : ITranslateResult
{
    public const int MaxSenses = 8;

    public string Word { get; set; } = string.Empty;
    public string? Phonetic { get; set; }
    public List<CambridgeSense> Senses { get; set; } = [];

    public override IEnumerable<ResultItem>? Transform()
    {
        if (Senses.Count == 0)
            return null;

        var senses = Senses.Take(MaxSenses).ToList();
        var phonetic = string.IsNullOrEmpty(Phonetic) ? "-" : Phonetic;
        var partsOfSpeech = string.Join(", ", senses.Select(s => s.PartOfSpeech).Where(p => p.Length > 0).Distinct());

        var res = new List<ResultItem>
        {
            new ResultItem
            {
                Title = string.Join("; ", senses.Select(s => s.Translation).Distinct()),
                SubTitle = $"{Word}(/{phonetic}/) [{partsOfSpeech}] Cambridge",
            }
        };
        foreach (var sense in senses)
        {
            res.Add(new ResultItem
            {
                Title = sense.Translation,
                SubTitle = string.IsNullOrEmpty(sense.PartOfSpeech) ? sense.Definition : $"[{sense.PartOfSpeech}] {sense.Definition}",
            });
        }
        foreach (var val in res)
            val.fromApiName = "Cambridge Dictionary";
        return res;
    }
}

/// <summary>
/// Looks up English words in the Cambridge English-Chinese (Traditional) dictionary.
/// Only handles English words or short phrases translated to Traditional Chinese (or auto);
/// returns null otherwise so the caller can fall back to the other translators.
/// </summary>
public partial class CambridgeTranslator : ITranslator, IDisposable
{
    private const string urlPattern = "https://dictionary.cambridge.org/dictionary/english-chinese-traditional/{0}";
    private const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";
    private HttpClient client;

    public CambridgeTranslator()
    {
        client = BuildClient();
    }

    private static HttpClient BuildClient()
    {
        var c = new HttpClient(UtilsFun.httpClientDefaultHandler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromMilliseconds(3000),
        };
        c.DefaultRequestHeaders.Add("User-Agent", userAgent);
        c.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");
        c.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9,zh-TW;q=0.8");
        return c;
    }

    public override void Reset()
    {
        client.Dispose();
        client = BuildClient();
    }

    public static bool IsApplicable(string src, string toLan)
    {
        var target = toLan.Trim();
        var isTraditionalTarget = target.Equals("zh-Hant", StringComparison.OrdinalIgnoreCase)
            || target.Equals("zht", StringComparison.OrdinalIgnoreCase)
            || target.Equals("zh-TW", StringComparison.OrdinalIgnoreCase)
            || target.Equals("auto", StringComparison.OrdinalIgnoreCase);
        return isTraditionalTarget && EnglishWordRegex().IsMatch(src.Trim());
    }

    public static string BuildUrl(string word)
    {
        var slug = WhitespaceRegex().Replace(word.Trim().ToLowerInvariant(), "-");
        return string.Format(urlPattern, Uri.EscapeDataString(slug));
    }

    public override ITranslateResult? Translate(string src, string toLan, string fromLan)
    {
        if (!IsApplicable(src, toLan))
            return null;

        var response = client.GetAsync(BuildUrl(src)).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            Log.Warn($"Cambridge request failed: {(int)response.StatusCode}", typeof(CambridgeTranslator));
            return null;
        }
        var html = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        var result = Parse(html, src.Trim());
        return result.Senses.Count > 0 ? result : null;
    }

    public static TranslateResult Parse(string html, string word)
    {
        var result = new TranslateResult { Word = word };

        // Each part of speech has its own entry block; fall back to the whole page if none are found.
        var entries = EntryRegex().Split(html).Skip(1).ToList();
        if (entries.Count == 0)
            entries.Add(html);

        foreach (var entry in entries)
        {
            var headword = CleanText(HeadwordRegex().Match(entry).Groups[1].Value);
            if (headword.Length > 0 && result.Word == word)
                result.Word = headword;
            result.Phonetic ??= NullIfEmpty(CleanText(IpaRegex().Match(entry).Groups[1].Value));
            var pos = CleanText(PosRegex().Match(entry).Groups[1].Value);

            foreach (var block in DefBlockRegex().Split(entry).Skip(1))
            {
                var translation = CleanText(TransRegex().Match(block).Groups[1].Value);
                if (translation.Length == 0)
                    continue;
                result.Senses.Add(new CambridgeSense
                {
                    PartOfSpeech = pos,
                    Definition = CleanText(DefRegex().Match(block).Groups[1].Value).TrimEnd(':', ' '),
                    Translation = translation,
                });
            }
        }
        return result;
    }

    private static string? NullIfEmpty(string s) => s.Length == 0 ? null : s;

    private static string CleanText(string html)
    {
        var text = WebUtility.HtmlDecode(TagRegex().Replace(html, string.Empty));
        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    public void Dispose()
    {
        client.Dispose();
        GC.SuppressFinalize(this);
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z'\-]*(?: [A-Za-z'\-]+){0,3}$")]
    private static partial Regex EnglishWordRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"<div class=""[^""]*\bentry-body__el\b[^""]*""")]
    private static partial Regex EntryRegex();

    [GeneratedRegex(@"<div class=""[^""]*\bdef-block\b[^""]*""")]
    private static partial Regex DefBlockRegex();

    [GeneratedRegex(@"<span class=""hw dhw"">(.*?)</span>", RegexOptions.Singleline)]
    private static partial Regex HeadwordRegex();

    [GeneratedRegex(@"<span class=""pos dpos""[^>]*>(.*?)</span>", RegexOptions.Singleline)]
    private static partial Regex PosRegex();

    [GeneratedRegex(@"<span class=""ipa dipa[^""]*"">(.*?)</span>/", RegexOptions.Singleline)]
    private static partial Regex IpaRegex();

    [GeneratedRegex(@"<div class=""def ddef_d[^""]*"">(.*?)</div>", RegexOptions.Singleline)]
    private static partial Regex DefRegex();

    [GeneratedRegex(@"<span class=""trans dtrans[^""]*""[^>]*>(.*?)</span>", RegexOptions.Singleline)]
    private static partial Regex TransRegex();
}
