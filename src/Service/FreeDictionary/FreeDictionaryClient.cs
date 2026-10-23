using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Translator.Protocol;
using Translator.Utils;

namespace Translator.Service.FreeDictionary;

// Response model of https://dictionaryapi.dev (an array of entries per word).
public sealed class FreeDictionaryEntry
{
    [JsonPropertyName("word")]
    public string Word { get; set; } = string.Empty;

    [JsonPropertyName("phonetic")]
    public string? Phonetic { get; set; }

    [JsonPropertyName("phonetics")]
    public FreeDictionaryPhonetic[]? Phonetics { get; set; }

    [JsonPropertyName("meanings")]
    public FreeDictionaryMeaning[]? Meanings { get; set; }
}

public sealed class FreeDictionaryPhonetic
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("audio")]
    public string? Audio { get; set; }
}

public sealed class FreeDictionaryMeaning
{
    [JsonPropertyName("partOfSpeech")]
    public string PartOfSpeech { get; set; } = string.Empty;

    [JsonPropertyName("definitions")]
    public FreeDictionaryDefinition[]? Definitions { get; set; }

    [JsonPropertyName("synonyms")]
    public string[]? Synonyms { get; set; }
}

public sealed class FreeDictionaryDefinition
{
    [JsonPropertyName("definition")]
    public string Definition { get; set; } = string.Empty;

    [JsonPropertyName("example")]
    public string? Example { get; set; }

    [JsonPropertyName("synonyms")]
    public string[]? Synonyms { get; set; }
}

/// <summary>An English definition flattened out of the dictionaryapi.dev response.</summary>
public sealed record EnglishSense(string PartOfSpeech, string Definition, string? Example);

/// <summary>The English-English dictionary result for one word.</summary>
public sealed class EnglishDefinition
{
    public string Word { get; init; } = string.Empty;
    public string? Phonetic { get; init; }
    public IReadOnlyList<EnglishSense> Senses { get; init; } = [];

    public static EnglishDefinition? FromEntries(FreeDictionaryEntry[]? entries)
    {
        if (entries == null || entries.Length == 0)
            return null;
        var senses = entries
            .SelectMany(e => e.Meanings ?? [])
            .SelectMany(m => (m.Definitions ?? [])
                .Where(d => !string.IsNullOrWhiteSpace(d.Definition))
                .Select(d => new EnglishSense(m.PartOfSpeech, d.Definition.Trim(), NullIfBlank(d.Example))))
            .ToList();
        if (senses.Count == 0)
            return null;
        var phonetic = entries
            .Select(e => e.Phonetic)
            .Concat(entries.SelectMany(e => e.Phonetics ?? []).Select(p => p.Text))
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
        return new EnglishDefinition
        {
            Word = entries[0].Word,
            Phonetic = phonetic?.Trim().Trim('/', '[', ']'),
            Senses = senses,
        };
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>Looks up English words in the free English dictionary at https://dictionaryapi.dev.</summary>
public sealed partial class FreeDictionaryClient : IDisposable
{
    private const string Endpoint = "https://api.dictionaryapi.dev/api/v2/entries/en/";
    private const int CacheLimit = 256;
    private HttpClient client;
    // Results (including "not found") are cached because the palette re-queries while typing.
    private readonly ConcurrentDictionary<string, EnglishDefinition?> cache = new(StringComparer.OrdinalIgnoreCase);

    public FreeDictionaryClient()
    {
        client = BuildClient();
    }

    private static HttpClient BuildClient()
    {
        var c = new HttpClient(UtilsFun.httpClientDefaultHandler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(4),
        };
        c.DefaultRequestHeaders.Add("Accept", "application/json");
        return c;
    }

    public void Reset()
    {
        var old = Interlocked.Exchange(ref client, BuildClient());
        old.Dispose();
    }

    public static string BuildUrl(string word) => Endpoint + Uri.EscapeDataString(word.Trim().ToLowerInvariant());

    public async Task<EnglishDefinition?> LookupAsync(string word, CancellationToken cancellationToken = default)
    {
        var key = word.Trim();
        if (key.Length == 0)
            return null;
        if (cache.TryGetValue(key, out var cached))
            return cached;

        using var response = await client.GetAsync(BuildUrl(key), cancellationToken).ConfigureAwait(false);
        EnglishDefinition? result = null;
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            result = Parse(json);
        }
        if (cache.Count >= CacheLimit)
            cache.Clear();
        cache[key] = result;
        return result;
    }

    public static EnglishDefinition? Parse(string json) =>
        EnglishDefinition.FromEntries(UtilsFun.ParseJson(json, TranslatorJsonContext.Default.FreeDictionaryEntryArray));

    public void Dispose()
    {
        client.Dispose();
    }
}
