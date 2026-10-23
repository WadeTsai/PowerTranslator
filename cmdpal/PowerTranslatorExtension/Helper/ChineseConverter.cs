using System;
using System.Runtime.InteropServices;

namespace PowerTranslatorExtension.Utils;

/// <summary>
/// Converts Simplified Chinese characters to Traditional Chinese with the Windows NLS API
/// (LCMapStringEx + LCMAP_TRADITIONAL_CHINESE). Used as a safety net so online results never
/// show Simplified characters when Traditional Chinese was requested.
/// </summary>
public static partial class ChineseConverter
{
    private const uint LCMAP_TRADITIONAL_CHINESE = 0x04000000;

    public static bool IsTraditionalTarget(string toLan)
    {
        var t = toLan.Trim();
        return t.Equals("zh-Hant", StringComparison.OrdinalIgnoreCase)
            || t.Equals("zht", StringComparison.OrdinalIgnoreCase)
            || t.Equals("zh-TW", StringComparison.OrdinalIgnoreCase)
            || t.Equals("zh-HK", StringComparison.OrdinalIgnoreCase)
            || t.Equals("ZH-HANT", StringComparison.OrdinalIgnoreCase);
    }

    public static string ToTraditional(string text)
    {
        if (string.IsNullOrEmpty(text) || !OperatingSystem.IsWindows())
            return text;
        try
        {
            var size = LCMapStringEx("zh-TW", LCMAP_TRADITIONAL_CHINESE, text, text.Length, null, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (size <= 0)
                return text;
            var buffer = new char[size];
            size = LCMapStringEx("zh-TW", LCMAP_TRADITIONAL_CHINESE, text, text.Length, buffer, size, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            return size > 0 ? new string(buffer, 0, size) : text;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return text;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int LCMapStringEx(
        string lpLocaleName,
        uint dwMapFlags,
        string lpSrcStr,
        int cchSrc,
        [Out] char[]? lpDestStr,
        int cchDest,
        IntPtr lpVersionInformation,
        IntPtr lpReserved,
        IntPtr sortHandle);
}
