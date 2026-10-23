using System.Text.Json.Serialization;

namespace PowerTranslatorExtension.Protocol;

/// <summary>
/// Source-generated JSON metadata for every payload the extension sends or receives.
/// Required for trimming / Native AOT: no reflection-based serializer is used.
/// </summary>
[JsonSerializable(typeof(Service.DeepL.DeepLTranslateResult))]
[JsonSerializable(typeof(Service.DeepL.DeepLTranslateRequestBody))]
[JsonSerializable(typeof(Service.FreeDictionary.FreeDictionaryEntry[]))]
internal sealed partial class TranslatorJsonContext : JsonSerializerContext
{
}
