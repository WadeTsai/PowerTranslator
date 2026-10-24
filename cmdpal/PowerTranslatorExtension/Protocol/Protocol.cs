using System;
using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerTranslatorExtension.Protocol;


public class ResultItem
{
    public string Title { get; set; } = default!;
    public string SubTitle { get; set; } = default!;
    public Func<bool>? Action { get; set; }
    public string? CopyTgt { get; set; }
    public IconInfo? icon { get; set; }
    public string? transType { get; set; }
    public string? fromApiName { get; set; }
    public string? Description { get; set; }
    /// <summary>English word to open in the online dictionary (the source word, or the translation of CJK input).</summary>
    public string? DictWord { get; set; }
    /// <summary>Text for "Read aloud" when it differs from the title (dictionary results read the English word, not the Chinese).</summary>
    public string? ReadText { get; set; }
}

public interface ITranslateResult
{
    public IEnumerable<ResultItem>? Transform();
}

/// <summary>
/// Thrown when a translation service refuses or fails a request (rate limit, HTTP error),
/// so the next service is tried and the reason can be shown if every service fails.
/// </summary>
public class TranslatorUnavailableException(string reason) : Exception(reason)
{
}

public interface ITranslator
{
    public ITranslateResult? Translate(string src, string toLan, string fromLan);
    public void Init();
    public void Reset();
    public bool Inited { get; }
    public string Name { get; }
}