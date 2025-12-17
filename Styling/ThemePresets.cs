using StruxureGuard.Core.Logging;
using StruxureGuard.Styling.Presets;

namespace StruxureGuard.Styling;

public static class ThemePresets
{
    public static void RegisterAll()
    {
        int ok = 0, fail = 0;

        void Register(string id, Func<ThemeSettings> factory)
        {
            try
            {
                var t = factory();
                if (t is null)
                {
                    Log.Warn("theme", $"Preset '{id}' returned null (skipped)");
                    fail++;
                    return;
                }

                if (string.IsNullOrWhiteSpace(t.Name))
                {
                    Log.Warn("theme", $"Preset '{id}' returned ThemeSettings with empty Name (skipped)");
                    fail++;
                    return;
                }

                ThemeManager.RegisterPreset(t);
                ok++;
                Log.Info("theme", $"Registered preset '{t.Name}' ({id})");
            }
            catch (Exception ex)
            {
                fail++;
                Log.Warn("theme", $"Register preset '{id}' failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // ✅ Register everything explicitly (no reflection)
        Register(nameof(Dark), Dark.Create);
        Register(nameof(FlatDarkPreset), FlatDarkPreset.Create);
        Register(nameof(FlatLightPreset), FlatLightPreset.Create);
        Register(nameof(BlueAccentPreset), BlueAccentPreset.Create);
        Register(nameof(EmeraldDarkPreset), EmeraldDarkPreset.Create);
        Register(nameof(HighContrastDarkPreset), HighContrastDarkPreset.Create);
        Register(nameof(NordPreset), NordPreset.Create);
        Register(nameof(SolarizedDarkPreset), SolarizedDarkPreset.Create);

        Log.Info("theme", $"ThemePresets.RegisterAll: ok={ok}, fail={fail}, presets=[{string.Join(", ", ThemeManager.GetPresetNames())}]");

        if (ok == 0)
            Log.Warn("theme", "No presets registered. Check preset classes + namespaces.");
    }
}
