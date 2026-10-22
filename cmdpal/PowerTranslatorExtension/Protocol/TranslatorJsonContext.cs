using System.Text.Json.Serialization;

namespace PowerTranslatorExtension.Protocol;

/// <summary>
/// Source-generated JSON metadata for every payload the extension sends or receives.
/// Avoids reflection-based serializer warm-up on the first query and keeps the extension AOT-safe.
/// </summary>
[JsonSerializable(typeof(Service.Youdao.V2.KeyResponse))]
[JsonSerializable(typeof(Service.Youdao.V2.TranslateResponse), TypeInfoPropertyName = "YoudaoV2Response")]
[JsonSerializable(typeof(Service.Youdao.Old.TranslateResponse), TypeInfoPropertyName = "YoudaoOldResponse")]
[JsonSerializable(typeof(Service.Youdao.Backup.TranslateResult), TypeInfoPropertyName = "YoudaoBackupResult")]
[JsonSerializable(typeof(Service.DeepL.DeepLTranslateResult))]
[JsonSerializable(typeof(Service.DeepL.DeepLTranslateRequestBody))]
[JsonSerializable(typeof(Service.Aliyun.AliyunTranslateResult))]
[JsonSerializable(typeof(Service.Aliyun.AliyunCsrfResp))]
[JsonSerializable(typeof(Suggest.SuggestInterface))]
[JsonSerializable(typeof(Service.Aliyun.AliyunTranslateResult.Data), TypeInfoPropertyName = "AliyunData")]
// Nested types that share a simple name with another registered type need explicit names,
// otherwise the generator only emits metadata for one of them (SYSLIB1031).
[JsonSerializable(typeof(Service.Youdao.V2.TranslateResponse.TranDictResult.Ec.Word), TypeInfoPropertyName = "YoudaoEcWord")]
[JsonSerializable(typeof(Service.Youdao.V2.TranslateResponse.TranDictResult.Ec.Word?), TypeInfoPropertyName = "YoudaoEcWordNullable")]
[JsonSerializable(typeof(Service.Youdao.V2.TranslateResponse.TranDictResult.Ec.Word.Trs), TypeInfoPropertyName = "YoudaoEcTrs")]
[JsonSerializable(typeof(Service.Youdao.V2.TranslateResponse.TranDictResult.Ec.Word.Trs[]), TypeInfoPropertyName = "YoudaoEcTrsArray")]
internal sealed partial class TranslatorJsonContext : JsonSerializerContext
{
}
