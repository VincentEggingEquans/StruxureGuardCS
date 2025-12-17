using System.Text.Json;
using StruxureGuard.Core.Logging;

namespace StruxureGuard.Styling;

public static class ThemeManager
{
    private static readonly Dictionary<string, ThemeSettings> _presets = new(StringComparer.OrdinalIgnoreCase);

    public static ThemeSettings? Current { get; private set; }

    private static string ThemeFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StruxureGuard",
            "theme.json"
        );

    public static void RegisterPreset(ThemeSettings t)
    {
        _presets[t.Name] = t;
        Log.Info("theme", $"Registered preset '{t.Name}'");
    }

    public static IEnumerable<string> GetPresetNames() =>
        _presets.Keys.OrderBy(x => x);

    public static void ApplyPreset(string name)
    {
        Log.Info("theme", $"ApplyPreset called with '{name}'");

        if (!_presets.TryGetValue(name, out var preset))
        {
            Log.Warn("theme", $"ApplyPreset: preset '{name}' NOT FOUND");
            return;
        }

        Current = preset;
        Log.Info("theme", $"ApplyPreset: Current set to '{Current.Name}'");

        SaveCurrent();
        ApplyThemeToAllOpenForms();
    }

    public static void ApplyTheme(Control root)
    {
        if (Current is null) return;
        ThemeApplier.Apply(root, Current);
    }

    public static void ApplyThemeToAllOpenForms()
    {
        if (Current is null) return;

        Log.Info("theme", $"ApplyThemeToAllOpenForms: applying '{Current.Name}' to {Application.OpenForms.Count} forms");

        foreach (Form f in Application.OpenForms)
        {
            Log.Info("theme", $"Applying theme to form '{f.Name}' ({f.GetType().Name})");
            ThemeApplier.Apply(f, Current);
        }
    }

    public static void LoadAtStartup()
    {
        if (Current is null && _presets.Count > 0)
        {
            Current = _presets.Values.First();
            Log.Info("theme", $"LoadAtStartup: fallback Current='{Current.Name}'");
        }

        try
        {
            if (!File.Exists(ThemeFilePath))
            {
                Log.Info("theme", "LoadAtStartup: no saved theme file");
                return;
            }

            var json = File.ReadAllText(ThemeFilePath);
            var loaded = JsonSerializer.Deserialize<ThemeSettings>(json, JsonOptions);
            if (loaded is null)
            {
                Log.Warn("theme", "LoadAtStartup: theme.json deserialize returned null");
                return;
            }

            Current = _presets.TryGetValue(loaded.Name, out var preset) ? preset : loaded;
            Log.Info("theme", $"LoadAtStartup: loaded Current='{Current.Name}' (preset={_presets.ContainsKey(Current.Name)})");
        }
        catch (Exception ex)
        {
            Log.Warn("theme", $"LoadAtStartup failed: {ex.Message}");
        }
    }

    public static void SaveCurrent()
    {
        if (Current is null) return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ThemeFilePath)!);
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(ThemeFilePath, json);
            Log.Info("theme", $"Saved theme '{Current.Name}' to {ThemeFilePath}");
        }
        catch (Exception ex)
        {
            Log.Warn("theme", $"SaveCurrent failed: {ex.Message}");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new ColorJsonConverter() }
    };
}
