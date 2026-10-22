using System.Text.Json.Serialization;

namespace Translator.Protocol;

/// <summary>
/// Source-generated JSON metadata for every payload the plugin sends or receives.
/// Avoids reflection-based serializer warm-up on the first query.
/// </summary>
[JsonSerializable(typeof(Service.Youdao.V2.KeyResponse))]
[JsonSerializable(typeof(Service.Youdao.V2.TranslateResponse), TypeInfoPropertyName = "YoudaoV2Response")]
[JsonSerializable(typeof(Service.Youdao.old.TranslateResponse), TypeInfoPropertyName = "YoudaoOldResponse")]
[JsonSerializable(typeof(Service.Youdao.Backup.TranslateResult), TypeInfoPropertyName = "YoudaoBackupResult")]
[JsonSerializable(typeof(Service.DeepL.DeepLTranslateResult))]
[JsonSerializable(typeof(Service.DeepL.DeepLTranslateRequestBody))]
[JsonSerializable(typeof(Suggest.SuggestInterface))]
// Nested types that share a simple name with another registered type need explicit names,
// otherwise the generator only emits metadata for one of them (SYSLIB1031).
[JsonSerializable(typeof(Service.Youdao.V2.TranslateResponse.TranDictResult.Ec.Word), TypeInfoPropertyName = "YoudaoEcWord")]
[JsonSerializable(typeof(Service.Youdao.V2.TranslateResponse.TranDictResult.Ec.Word?), TypeInfoPropertyName = "YoudaoEcWordNullable")]
[JsonSerializable(typeof(Service.Youdao.V2.TranslateResponse.TranDictResult.Ec.Word.Trs), TypeInfoPropertyName = "YoudaoEcTrs")]
[JsonSerializable(typeof(Service.Youdao.V2.TranslateResponse.TranDictResult.Ec.Word.Trs[]), TypeInfoPropertyName = "YoudaoEcTrsArray")]
[JsonSerializable(typeof(Service.DeepL.Params.Sentence), TypeInfoPropertyName = "DeepLJobSentence")]
[JsonSerializable(typeof(List<Service.DeepL.Params.Sentence>), TypeInfoPropertyName = "DeepLJobSentenceList")]
internal sealed partial class TranslatorJsonContext : JsonSerializerContext
{
}
