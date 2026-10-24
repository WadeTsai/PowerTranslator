using Wox.Plugin.Logger;
using System.Windows;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;
using Wox.Plugin;

namespace Translator.Utils
{
    public static partial class UtilsFun
    {
        public static HttpClientHandler httpClientDefaultHandler = CreateHandler(useSystemProxy: false);
        public static Action? onHttpDefaultHandlerChange;
        public static string[] user_agents = {
            "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/39.0.2171.71 Safari/537.36",
            "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:34.0) Gecko/20100101 Firefox/34.0",
            "Opera/9.80 (Macintosh; Intel Mac OS X 10.6.8; U; en) Presto/2.8.131 Version/11.11"
        };
        public static string GetRandomUserAgent()
        {
            return UtilsFun.user_agents[DateTime.Now.Millisecond % UtilsFun.user_agents.Length];
        }
        public static bool WhetherTranslate(string? s)
        {
            return s != null && s.Length > 0 && !s.Contains("\\") && !s.Contains("/");
        }
        public static string ToEnPunctuation(this string src)
        {
            return src.Replace('，', ',').Replace('；', ';').Replace('（', '(').Replace('）', ')');
        }
        public static T? ParseJson<T>(string src, JsonTypeInfo<T> typeInfo)
        {
            try
            {
                return JsonSerializer.Deserialize(src, typeInfo);
            }
            catch (JsonException)
            {
                return default(T);
            }
        }
        public static T? ParseJson<T>(string src)
        {
            try
            {
                var res = JsonSerializer.Deserialize<T>(src);
                return res;
            }
            catch (JsonException)
            {
                return default(T);
            }
        }
        public static void SetClipboardText(string s)
        {
            Clipboard.SetDataObject(s);
        }
        public static long GetUtcTimeNow()
        {
            return DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }
        public static long GetNowTicksMilliseconds()
        {
            return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        }
        public static string ToFormateTime(this long ticks)
        {
            var time = new DateTime(ticks * TimeSpan.TicksPerMillisecond);
            return String.Format("{0:yyyy/MM/dd hh:mm:ss}:{1}", time, time.Millisecond);
        }
        public static string? GetClipboardText()
        {
            try
            {
                string? res = null;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Clipboard.ContainsText())
                        res = Clipboard.GetText();
                });
                return res;
            }
            catch (Exception err)
            {
                Log.Error(err.Message, typeof(UtilsFun));
                return string.Empty;
            }
        }

        public static void each<T>(this IEnumerable<T> src, Action<T> action)
        {
            foreach (var item in src)
            {
                action(item);
            }
        }
        /// <summary>
        /// Encodes key/value pairs as application/x-www-form-urlencoded (also valid as a query string).
        /// </summary>
        public static string ToFormString(this IEnumerable<KeyValuePair<string, string>> pairs)
        {
            return string.Join("&", pairs.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        }
        public static StringContent ToFormContent(this IEnumerable<KeyValuePair<string, string>> pairs)
        {
            return new StringContent(pairs.ToFormString(), System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
        }
        public static List<Wox.Plugin.Result> ToResultList(this IEnumerable<ResultItem> src, string iconPath, PluginInitContext pluginInitContext, bool copyOnlyFirstOption = false)
        {
            return src.Select((item, idx) =>
            {
                return new Wox.Plugin.Result
                {
                    Title = item.Title,
                    SubTitle = item.SubTitle,
                    DisableUsageBasedScoring = true,
                    Action = item.Action != null ? ((e) =>
                    {
                        return item.Action!(new CustomActionContext
                        {
                            actionContext = e,
                            pluginInitContext = pluginInitContext
                        });
                    }) :
                    ((e) =>
                    {
                        var textToCopy = item.CopyTgt ?? item.Title;
                        // If copyOnlyFirstOption is true and output contains semicolon, copy only the first part
                        if (copyOnlyFirstOption && (textToCopy.Contains(';') || textToCopy.Contains('；')))
                        {
                            // Split by both ASCII and Chinese semicolons
                            var parts = textToCopy.Split(new[] { ';', '；' }, StringSplitOptions.None);
                            textToCopy = parts[0].Trim();
                        }
                        UtilsFun.SetClipboardText(textToCopy);
                        return true;
                    }),
                    IcoPath = item.iconPath ?? iconPath,
                    ContextData = item,
                    ToolTipData = new Wox.Plugin.ToolTipData(item.Title, $"{item.Description ?? item.SubTitle}\n\n{item.transType}-{item.fromApiName}")
                };
            }).ToList();
        }

        public static T? FirstNotNoneCast<S, T>(this IEnumerable<S> src, Func<S, T?> func)
        {
            foreach (var item in src)
            {
                var t = func(item);
                if (t != null)
                    return t;
            }
            return default(T);
        }

        public static IEnumerable<(int idx, S item)> Enumerate<S>(this IEnumerable<S> src)
        {
            int idx = 0;
            foreach (var item in src)
            {
                yield return (idx, item);
                idx++;
            }
        }

        public static string removeSpecialCharacter(this string str)
        {
            return SpecialCharacterRegex().Replace(str, " ");
        }

        [GeneratedRegex(@"[^\w\s\u4e00-\u9fa5]")]
        private static partial Regex SpecialCharacterRegex();

        private static HttpClientHandler CreateHandler(bool useSystemProxy)
        {
            return new HttpClientHandler
            {
                UseProxy = useSystemProxy,
                Proxy = useSystemProxy ? WebRequest.GetSystemWebProxy() : null,
                // API responses and dictionary pages compress well; without this every response is downloaded raw.
                AutomaticDecompression = DecompressionMethods.All,
            };
        }

        public static void ChangeDefaultHttpHandlerProxy(bool useSystemProxy, bool callEvent = true)
        {
            UtilsFun.httpClientDefaultHandler = CreateHandler(useSystemProxy);
            if (callEvent)
                onHttpDefaultHandlerChange?.Invoke();
        }

        public static string ConvertSnakeCaseOrCamelCaseToNormalSpace(string src)
        {
            if (src.Contains(' '))
            {
                return src;
            }
            if (src.Contains('_'))
            {
                return src.Replace("_", " ");
            }
            return CamelRegex().Replace(src, " $1").TrimStart();
        }

        [GeneratedRegex(@"([A-Z])", RegexOptions.Compiled)]
        private static partial Regex CamelRegex();
    }
}