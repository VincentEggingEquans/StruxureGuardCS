namespace StruxureGuard.Core.Tools.LineConverter;

public sealed class LineConverterOptions
{
    public required string InputText { get; init; }

    public string Conjunction { get; init; } = "en";
    public bool Deduplicate { get; init; }
    public bool Sort { get; init; }
    public bool OxfordComma { get; init; }
}
