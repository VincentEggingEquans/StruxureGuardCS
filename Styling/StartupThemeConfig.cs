namespace StruxureGuard.Styling;

public sealed class StartupThemeConfig
{
    // If true: ignore theme.json and always start with DefaultPresetName (if exists)
    public bool ForceDefaultPreset { get; set; } = false;

    // Name must match a registered preset (e.g. "Dark", "Nord", "SolarizedDark", ...)
    public string DefaultPresetName { get; set; } = "Dark";
}
