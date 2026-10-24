using Wox.Plugin;

namespace Translator
{
    public class CustomActionContext
    {
        public required ActionContext actionContext { get; set; }
        public required PluginInitContext pluginInitContext { get; set; }

    }
    public class ResultItem
    {
        public string Title { get; set; } = default!;
        public string SubTitle { get; set; } = default!;
        public Func<CustomActionContext, bool>? Action { get; set; }
        public string? CopyTgt { get; set; }
        public string? iconPath { get; set; }
        public string? transType { get; set; }
        public string? fromApiName { get; set; }
        public string? Description { get; set; }
        /// <summary>English word to open in the online dictionary (the source word, or the translation of CJK input).</summary>
        public string? DictWord { get; set; }
        /// <summary>Text for "Read" when it differs from the title (dictionary results read the English word, not the Chinese).</summary>
        public string? ReadText { get; set; }
    }
}