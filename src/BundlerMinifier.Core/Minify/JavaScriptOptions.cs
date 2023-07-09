using NUglify;
using NUglify.JavaScript;

namespace BundlerMinifier;

static class JavaScriptOptions
{
    public static CodeSettings GetSettings(Bundle bundle)
    {
        var settings = new CodeSettings
        {
            AlwaysEscapeNonAscii = GetValue(bundle, "alwaysEscapeNonAscii", false) == "True",
            PreserveImportantComments = GetValue(bundle, "preserveImportantComments", true) == "True",
            TermSemicolons = GetValue(bundle, "termSemicolons", true) == "True"
        };

        if (GetValue(bundle, "renameLocals", true) == "False")
        {
            settings.LocalRenaming = LocalRenaming.KeepAll;
        }

        string evalTreatment = GetValue(bundle, "evalTreatment", "ignore");

        settings.EvalTreatment = evalTreatment switch
        {
            "ignore" => EvalTreatment.Ignore,
            "makeAllSafe" => EvalTreatment.MakeAllSafe,
            "makeImmediateSafe" => EvalTreatment.MakeImmediateSafe,
            _ => settings.EvalTreatment
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