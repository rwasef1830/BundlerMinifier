using NUglify;
using NUglify.Css;

namespace BundlerMinifier
{
    static class CssOptions
    {
        public static CssSettings GetSettings(Bundle bundle)
        {
            var settings = new CssSettings
            {
                TermSemicolons = GetValue(bundle, "termSemicolons") == "True",
                DecodeEscapes = GetValue(bundle, "decodeEscapes", "True") == "True"
            };

            string cssComment = GetValue(bundle, "commentMode");

            settings.CommentMode = cssComment switch
            {
                "hacks" => CssComment.Hacks,
                "important" => CssComment.Important,
                "none" => CssComment.None,
                "all" => CssComment.All,
                _ => settings.CommentMode
            };

            string colorNames = GetValue(bundle, "colorNames");

            settings.ColorNames = colorNames switch
            {
                "hex" => CssColor.Hex,
                "major" => CssColor.Major,
                "noSwap" => CssColor.NoSwap,
                "strict" => CssColor.Strict,
                _ => settings.ColorNames
            };

            string outputMode = GetValue(bundle, "outputMode", "singleLine");

            settings.OutputMode = outputMode switch
            {
                "multipleLines" => OutputMode.MultipleLines,
                "singleLine" => OutputMode.SingleLine,
                "none" => OutputMode.None,
                _ => settings.OutputMode
            };

            string indentSize = GetValue(bundle, "indentSize", 2);
            if (int.TryParse(indentSize, out var size))
            {
                settings.Indent = new string(' ', size);
            }

            settings.IgnoreErrorList = GetValue(bundle, "ignoreErrorList", "");

            if (long.TryParse(GetValue(bundle, "killSwitch", "0"), out var killSwitch))
            {
                settings.KillSwitch = killSwitch;
            }

            return settings;
        }

        internal static string GetValue(Bundle bundle, string key, object defaultValue = null)
        {
            if (bundle.Minify.TryGetValue(key, out var value))
            {
                return value.ToString();
            }

            if (defaultValue != null)
            {
                return defaultValue.ToString();
            }

            return string.Empty;
        }
    }
}
