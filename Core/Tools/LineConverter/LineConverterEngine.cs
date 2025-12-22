namespace StruxureGuard.Core.Tools.LineConverter;

public static class LineConverterEngine
{
    public static List<string> ParseLines(string inputText)
    {
        inputText ??= "";

        var lines = inputText
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        return lines;
    }

    public static string ConvertToSentence(IReadOnlyList<string> items, string conjunction = "en", bool oxfordComma = false)
    {
        conjunction = string.IsNullOrWhiteSpace(conjunction) ? "en" : conjunction.Trim();

        if (items.Count == 0) return "";
        if (items.Count == 1) return items[0];
        if (items.Count == 2) return $"{items[0]} {conjunction} {items[1]}";

        // Dutch default: no comma before "en" (unless you opt-in to Oxford comma)
        var prefix = string.Join(", ", items.Take(items.Count - 1));
        var sep = oxfordComma ? ", " : " ";
        return prefix + sep + conjunction + " " + items[^1];
    }

    public static (List<string> Items, string Result) Execute(LineConverterOptions opt)
    {
        var items = ParseLines(opt.InputText);

        if (opt.Deduplicate)
            items = items.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (opt.Sort)
            items = items.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

        var result = ConvertToSentence(items, opt.Conjunction, opt.OxfordComma);
        return (items, result);
    }
}
