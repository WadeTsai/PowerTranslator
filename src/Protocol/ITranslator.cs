namespace Translator.Protocol;


public abstract class ITranslateResult
{
    public abstract IEnumerable<ResultItem>? Transform();
}

/// <summary>
/// Thrown when a translation service refuses or fails a request (rate limit, HTTP error),
/// so the next service is tried and the reason can be shown if every service fails.
/// </summary>
public class TranslatorUnavailableException(string reason) : Exception(reason)
{
}

public abstract class ITranslator
{
    public abstract ITranslateResult? Translate(string src, string toLan, string fromLan);
    public abstract void Reset();
}