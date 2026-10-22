using System.Collections.Generic;

namespace PowerTranslatorExtension.Service.Youdao.Utils;

public class YoudaoUtils
{
    Middleware.LanguageCodeHelper.DiffCultureLanguageCodeMiddleware youdaoLanguageCodeHelper = new(
        new Dictionary<string, string>{
            { "zh-Hans", "zh-CHS" },
            { "zh-Hant", "zh-CHT" },
            { "zh-CN", "zh-CHS" },
            { "zh-TW", "zh-CHT" },
            { "zh-SG", "zh-CHS" },
            {"zh", "zh-CHS"}
        }
    );


    /// <summary>
    /// Random anonymous visitor cookie, as set by the Youdao web page.
    /// </summary>
    public static string RandomCookie()
    {
        var r = System.Random.Shared;
        return $"OUTFOX_SEARCH_USER_ID_NCOO={r.Next(100000000, 999999999)}.{r.Next(100000000, 999999999)};" +
               $"OUTFOX_SEARCH_USER_ID={r.Next(100000000, 999999999)}@{r.Next(1, 255)}.{r.Next(1, 255)}.{r.Next(1, 255)}.{r.Next(1, 255)}";
    }

    public string GetYoudaoLanguageCode(string curtualName)
    {
        return youdaoLanguageCodeHelper.GetLanguageCode(curtualName);
    }

    public static YoudaoUtils Instance { get; } = new YoudaoUtils();
}