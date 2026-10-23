using Translator.Service.Dictionary;
using Translator.Service.Ecdict;
using Wox.Plugin.Logger;
namespace Translator.Suggest;

/// <summary>Offline word completions with Traditional Chinese glosses from the bundled ECDICT.</summary>
public class SuggestHelper
{
    public const int MaxSuggestions = 5;
    private readonly Func<EcdictDictionary?> dictionary;

    public SuggestHelper()
        : this(() => EcdictDictionary.Shared)
    {
    }

    internal SuggestHelper(Func<EcdictDictionary?> dictionary)
    {
        this.dictionary = dictionary;
    }

    public List<ResultItem> QuerySuggest(string query)
    {
        var dict = dictionary();
        if (dict == null || !WordLookupTranslator.IsEnglishWord(query))
            return new List<ResultItem>();
        try
        {
            var key = EcdictDictionary.NormalizeKey(query);
            return dict.Suggest(query, MaxSuggestions + 1)
                .Where(e => !string.Equals(EcdictDictionary.NormalizeKey(e.Word), key, StringComparison.Ordinal))
                .Take(MaxSuggestions)
                .Select(e => new ResultItem
                {
                    Title = e.Summary,
                    SubTitle = e.Word,
                    fromApiName = WordLookupResult.EcdictApiName,
                    transType = "[Suggest]",
                })
                .ToList();
        }
        catch (Exception err)
        {
            Log.Error($"Suggest failed: {err.Message}", typeof(SuggestHelper));
            return new List<ResultItem>();
        }
    }
}
