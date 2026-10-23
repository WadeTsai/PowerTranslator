using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using PowerTranslatorExtension.Protocol;
using PowerTranslatorExtension.Utils;

namespace PowerTranslatorExtension.Service.Google;

public sealed class GoogleTranslateResult(string text, string sourceLang, string targetLang) : ITranslateResult
{
    public string Text { get; } = text;
    public string SourceLang { get; } = sourceLang;
    public string TargetLang { get; } = targetLang;

    public IEnumerable<ResultItem>? Transform() =>
    [
        new ResultItem
        {
            Title = Text,
            SubTitle = $"{SourceLang} -> {TargetLang}",
            CopyTgt = Text,
            fromApiName = GoogleTranslator.ApiName,
        },
    ];
}

/// <summary>
/// Fallback sentence translator using Google's public translate endpoint (client=gtx).
/// Used when DeepL is rate-limited or unreachable.
/// </summary>
public sealed partial class GoogleTranslator : ITranslator, IDisposable
{
    public const string ApiName = "Google";
    private const string apiEndPoint = "https://translate.googleapis.com/translate_a/single";
    private HttpClient client;

    public bool Inited => true;
    public string Name => ApiName;

    public GoogleTranslator()
    {
        client = BuildClient();
    }

    private static HttpClient BuildClient() =>
        new(UtilsFun.httpClientDefaultHandler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(10),
        };

    public void Init()
    {
    }

    public void Reset()
    {
        var old = client;
        client = BuildClient();
        old.Dispose();
    }

    /// <summary>
    /// Maps a culture name to Google's language code.
    /// "auto" translates CJK text to English and everything else to Traditional Chinese.
    /// </summary>
    public static string ResolveTarget(string src, string toLan)
    {
        var target = toLan.Trim();
        if (target.Equals("auto", StringComparison.OrdinalIgnoreCase))
            target = DeepL.DeepLTranslator.ContainsCjk(src) ? "en" : "zh-Hant";
        if (ChineseConverter.IsTraditionalTarget(target))
            return "zh-TW";
        if (target.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase)
            || target.Equals("zhs", StringComparison.OrdinalIgnoreCase)
            || target.Equals("zh-CN", StringComparison.OrdinalIgnoreCase))
            return "zh-CN";
        var dash = target.IndexOf('-');
        return (dash > 0 ? target[..dash] : target).ToLowerInvariant();
    }

    /// <summary>Parses the gtx response: [[["translated", "source", ...], ...], null, "detected source", ...].</summary>
    public static GoogleTranslateResult? ParseResponse(string content, string targetLang)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0 || root[0].ValueKind != JsonValueKind.Array)
                return null;
            var text = new StringBuilder();
            foreach (var segment in root[0].EnumerateArray())
            {
                if (segment.ValueKind == JsonValueKind.Array && segment.GetArrayLength() > 0 && segment[0].ValueKind == JsonValueKind.String)
                    text.Append(segment[0].GetString());
            }
            if (text.Length == 0)
                return null;
            var sourceLang = root.GetArrayLength() > 2 && root[2].ValueKind == JsonValueKind.String ? root[2].GetString()! : "auto";
            var translated = targetLang == "zh-TW" ? ChineseConverter.ToTraditional(text.ToString()) : text.ToString();
            return new GoogleTranslateResult(translated, sourceLang, targetLang);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public ITranslateResult? Translate(string src, string toLan, string fromLan)
    {
        var targetLang = ResolveTarget(src, toLan);
        var sourceLang = string.IsNullOrEmpty(fromLan) ? "auto" : fromLan;
        var url = $"{apiEndPoint}?client=gtx&sl={Uri.EscapeDataString(sourceLang)}&tl={Uri.EscapeDataString(targetLang)}&dt=t&q={Uri.EscapeDataString(src)}";
        using var response = client.GetAsync(url).GetAwaiter().GetResult();
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new TranslatorUnavailableException("rate-limited (HTTP 429)");
        if (!response.IsSuccessStatusCode)
            throw new TranslatorUnavailableException($"HTTP {(int)response.StatusCode}");
        var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        return ParseResponse(content, targetLang);
    }

    public void Dispose()
    {
        client.Dispose();
    }
}
