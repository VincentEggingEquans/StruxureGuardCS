using System.Drawing;
using System.Text;

namespace StruxureGuard.Styling;

public static class ThemePresetExporter
{
    /// <summary>
    /// Generate a C# preset class (StruxureGuard.Styling.Presets.*) from a ThemeSettings instance.
    /// </summary>
    /// <param name="theme">Theme to export</param>
    /// <param name="className">C# class name (sanitized)</param>
    /// <param name="presetNameOverride">Override for ThemeSettings.Name in the generated class</param>
    /// <param name="includeOverrides">If true: include ThemeSettings.Overrides dictionary entries</param>
    public static string ToPresetClass(
        ThemeSettings theme,
        string className,
        string? presetNameOverride = null,
        bool includeOverrides = false)
    {
        if (theme is null) throw new ArgumentNullException(nameof(theme));
        className = SanitizeIdentifier(className);

        var presetName = string.IsNullOrWhiteSpace(presetNameOverride) ? theme.Name : presetNameOverride;

        var sb = new StringBuilder();
        sb.AppendLine("using System.Drawing;");
        sb.AppendLine();
        sb.AppendLine("namespace StruxureGuard.Styling.Presets");
        sb.AppendLine("{");
        sb.AppendLine($"    public static class {className}");
        sb.AppendLine("    {");
        sb.AppendLine("        public static ThemeSettings Create()");
        sb.AppendLine("        {");
        sb.AppendLine("            var t = new ThemeSettings");
        sb.AppendLine("            {");
        sb.AppendLine($"                Name = {ToStringLiteral(presetName)},");
        sb.AppendLine($"                AppBack = {ColorExpr(theme.AppBack)},");
        sb.AppendLine($"                Surface = {ColorExpr(theme.Surface)},");
        sb.AppendLine($"                Text = {ColorExpr(theme.Text)},");
        sb.AppendLine($"                MutedText = {ColorExpr(theme.MutedText)},");
        sb.AppendLine();
        sb.AppendLine($"                Accent = {ColorExpr(theme.Accent)},");
        sb.AppendLine($"                Border = {ColorExpr(theme.Border)},");
        sb.AppendLine();
        sb.AppendLine($"                InputBack = {ColorExpr(theme.InputBack)},");
        sb.AppendLine($"                InputText = {ColorExpr(theme.InputText)},");
        sb.AppendLine();
        sb.AppendLine($"                ButtonBack = {ColorExpr(theme.ButtonBack)},");
        sb.AppendLine($"                ButtonText = {ColorExpr(theme.ButtonText)},");
        sb.AppendLine($"                ButtonDisabledBack = {ColorExpr(theme.ButtonDisabledBack)},");
        sb.AppendLine($"                ButtonDisabledText = {ColorExpr(theme.ButtonDisabledText)},");
        sb.AppendLine();
        sb.AppendLine($"                ListBack = {ColorExpr(theme.ListBack)},");
        sb.AppendLine($"                ListText = {ColorExpr(theme.ListText)},");
        sb.AppendLine($"                SelectionBack = {ColorExpr(theme.SelectionBack)},");
        sb.AppendLine($"                SelectionText = {ColorExpr(theme.SelectionText)},");
        sb.AppendLine();
        sb.AppendLine($"                StripBack = {ColorExpr(theme.StripBack)},");
        sb.AppendLine($"                StripText = {ColorExpr(theme.StripText)},");
        sb.AppendLine($"                StripBorder = {ColorExpr(theme.StripBorder)},");
        sb.AppendLine($"                StripHover = {ColorExpr(theme.StripHover)},");
        sb.AppendLine($"                StripPressed = {ColorExpr(theme.StripPressed)},");
        sb.AppendLine();
        sb.AppendLine($"                ProgressTrack = {ColorExpr(theme.ProgressTrack)},");
        sb.AppendLine($"                ProgressText = {ColorExpr(theme.ProgressText)},");
        sb.AppendLine();
        sb.AppendLine($"                FontFamily = {ToStringLiteral(theme.FontFamily)},");
        sb.AppendLine($"                FontSize = {FloatExpr(theme.FontSize)}");
        sb.AppendLine("            };");
        sb.AppendLine();

        if (includeOverrides && theme.Overrides.Count > 0)
        {
            sb.AppendLine("            // Per-control overrides (ThemeKey / Control.Name)");
            foreach (var kv in theme.Overrides.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                var key = kv.Key;
                var ov = kv.Value;

                sb.AppendLine($"            t.Overrides[{ToStringLiteral(key)}] = new ThemeSettings.ControlOverride");
                sb.AppendLine("            {");

                AppendNullableColor(sb, "BackColor", ov.BackColor);
                AppendNullableColor(sb, "ForeColor", ov.ForeColor);

                if (!string.IsNullOrWhiteSpace(ov.FontFamily))
                    sb.AppendLine($"                FontFamily = {ToStringLiteral(ov.FontFamily!)},");
                if (ov.FontSize.HasValue)
                    sb.AppendLine($"                FontSize = {FloatExpr(ov.FontSize.Value)},");

                AppendNullableColor(sb, "BorderColor", ov.BorderColor);
                AppendNullableColor(sb, "ButtonBack", ov.ButtonBack);
                AppendNullableColor(sb, "ButtonText", ov.ButtonText);

                AppendNullableColor(sb, "ProgressTrack", ov.ProgressTrack);
                AppendNullableColor(sb, "ProgressBar", ov.ProgressBar);
                AppendNullableColor(sb, "ProgressText", ov.ProgressText);

                // Trim trailing comma: simplest approach is to always leave commas and accept it? C# doesn't allow trailing
                // comma on last property assignment inside object initializer for older versions; we will ensure we end clean.
                // We do that by always writing commas and then add a dummy comment? Better: post-process block.
                sb = TrimTrailingCommaInCurrentObject(sb);

                sb.AppendLine("            };");
                sb.AppendLine();
            }
        }

        sb.AppendLine("            return t;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void AppendNullableColor(StringBuilder sb, string prop, Color? c)
    {
        if (!c.HasValue) return;
        sb.AppendLine($"                {prop} = {ColorExpr(c.Value)},");
    }

    private static StringBuilder TrimTrailingCommaInCurrentObject(StringBuilder sb)
    {
        // Remove the last ",\n" in the current object initializer block, if present.
        // We search backwards for a line that ends with ","
        var text = sb.ToString();
        var idx = text.LastIndexOf(",\n", StringComparison.Ordinal);
        if (idx < 0) return sb;

        // Ensure this comma is within the most recent object initializer braces we just wrote.
        // Very small safety: only remove if after the last '{' and before the last '}' that we haven't written yet.
        // At this point we haven't written the closing "};" yet, so it's safe enough to just remove the last comma.
        text = text.Remove(idx, 1); // remove the comma, keep newline
        return new StringBuilder(text);
    }

    private static string ColorExpr(Color c)
    {
        // Preserve alpha always
        return $"Color.FromArgb({c.A}, {c.R}, {c.G}, {c.B})";
    }

    private static string FloatExpr(float f)
    {
        // invariant-ish formatting for C# source
        if (Math.Abs(f - (int)f) < 0.0001f) return ((int)f).ToString() + "f";
        return f.ToString(System.Globalization.CultureInfo.InvariantCulture) + "f";
    }

    private static string ToStringLiteral(string s)
    {
        s ??= "";
        return "@\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private static string SanitizeIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Preset";

        var sb = new StringBuilder();
        bool first = true;

        foreach (var ch in name.Trim())
        {
            if (first)
            {
                if (char.IsLetter(ch) || ch == '_') sb.Append(ch);
                else if (char.IsDigit(ch)) { sb.Append('_'); sb.Append(ch); }
                else sb.Append('_');
                first = false;
                continue;
            }

            if (char.IsLetterOrDigit(ch) || ch == '_') sb.Append(ch);
            else sb.Append('_');
        }

        var id = sb.ToString();
        if (id.Length == 0) return "Preset";
        return id;
    }
}
