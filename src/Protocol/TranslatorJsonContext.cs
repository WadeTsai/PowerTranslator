using System.Text.Json.Serialization;

namespace Translator.Protocol;

/// <summary>
/// Source-generated JSON metadata for every payload the plugin sends or receives.
/// Avoids reflection-based serializer warm-up on the first query.
/// </summary>
[JsonSerializable(typeof(Service.DeepL.DeepLTranslateResult))]
[JsonSerializable(typeof(Service.DeepL.DeepLTranslateRequestBody))]
[JsonSerializable(typeof(Service.FreeDictionary.FreeDictionaryEntry[]))]
// Nested types that share a simple name with another registered type need explicit names,
// otherwise the generator only emits metadata for one of them (SYSLIB1031).
[JsonSerializable(typeof(Service.DeepL.Params.Sentence), TypeInfoPropertyName = "DeepLJobSentence")]
[JsonSerializable(typeof(List<Service.DeepL.Params.Sentence>), TypeInfoPropertyName = "DeepLJobSentenceList")]
internal sealed partial class TranslatorJsonContext : JsonSerializerContext
{
}
