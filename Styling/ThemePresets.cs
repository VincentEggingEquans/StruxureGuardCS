using StruxureGuard.Styling.Presets;

namespace StruxureGuard.Styling;

public static class ThemePresets
{
    public static void RegisterAll()
    {
        // ✅ explicit (geen reflectie)
        ThemeManager.RegisterPreset(Dark.Create());
        ThemeManager.RegisterPreset(FlatDarkPreset.Create());
        ThemeManager.RegisterPreset(FlatLightPreset.Create());
        ThemeManager.RegisterPreset(BlueAccentPreset.Create());
        ThemeManager.RegisterPreset(EmeraldDarkPreset.Create());
        ThemeManager.RegisterPreset(HighContrastDarkPreset.Create());
        ThemeManager.RegisterPreset(NordPreset.Create());
        ThemeManager.RegisterPreset(SolarizedDarkPreset.Create());
    }
}
