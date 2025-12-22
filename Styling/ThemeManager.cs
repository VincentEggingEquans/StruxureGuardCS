using System.Text.Json;

namespace StruxureGuard.Styling;

public static class ThemeManager
{
    private static readonly Dictionary<string, ThemeSettings> _presets =
        new(StringComparer.OrdinalIgnoreCase);

    public static ThemeSettings? Current { get; private set; }

    private static string AppDataDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StruxureGuard"
        );

    private static string ThemeFilePath =>
        Path.Combine(AppDataDir, "theme.json");

    private static string CustomThemesDir =>
        Path.Combine(AppDataDir, "CustomThemes");

    private static string StartupConfigPath =>
        Path.Combine(AppDataDir, "startupTheme.json");

    public static StartupThemeConfig StartupConfig { get; private set; } = new();

    // =========================
    // JSON
    // =========================
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new ColorJsonConverter(),
            new NullableColorJsonConverter()
        }
    };

    // =========================
    // Auto-theming
    // =========================
    private static bool _autoEnabled;
    private static readonly HashSet<Form> _trackedForms = new();
    private static readonly HashSet<Control> _hookedControlAdded = new();

    public static void EnableAutoTheming()
    {
        if (_autoEnabled) return;
        _autoEnabled = true;

        Application.Idle += (_, __) => ScanAndHookOpenForms();
        ScanAndHookOpenForms();
    }

    private static void ScanAndHookOpenForms()
    {
        if (Application.OpenForms.Count == 0) return;

        foreach (Form f in Application.OpenForms)
        {
            if (_trackedForms.Contains(f)) continue;
            _trackedForms.Add(f);

            f.FormClosed += (_, __) =>
            {
                _trackedForms.Remove(f);
                _hookedControlAdded.Remove(f);
            };

            // Apply after handles exist + also when shown
            f.HandleCreated += (_, __) => ApplyTheme(f);
            f.Shown += (_, __) => ApplyTheme(f);

            HookControlTreeForAutoTheme(f);

            ApplyTheme(f);
        }
    }

    private static void HookControlTreeForAutoTheme(Control root)
    {
        if (root is null) return;
        if (!_hookedControlAdded.Add(root)) return;

        root.ControlAdded += (_, e) =>
        {
            if (e.Control is null) return;

            ApplyTheme(e.Control);
            HookControlTreeForAutoTheme(e.Control);

            // Ensure menu-like surfaces hanging off controls are themed too
            if (Current is not null)
                ThemeApplier.ApplyContextMenusIfAny(e.Control, Current);
        };

        foreach (Control c in root.Controls)
            HookControlTreeForAutoTheme(c);

        if (Current is not null)
            ThemeApplier.ApplyContextMenusIfAny(root, Current);
    }

    // =========================
    // Presets
    // =========================
    public static void RegisterPreset(ThemeSettings t)
        => _presets[t.Name] = t;

    public static IEnumerable<string> GetPresetNames()
        => _presets.Keys.OrderBy(x => x);

    public static ThemeSettings? GetPreset(string name)
        => _presets.TryGetValue(name, out var p) ? p : null;

    public static ThemeSettings? GetPresetCopy(string name)
        => _presets.TryGetValue(name, out var p) ? p.Clone() : null;

    // =========================
    // Apply
    // =========================
    public static void ApplyPreset(string name)
    {
        var copy = GetPresetCopy(name);
        if (copy is null) return;

        Current = copy;
        SaveCurrent();
        ApplyThemeToAllOpenForms();
    }

    public static void ApplyThemeSettings(ThemeSettings t, bool persist = true)
    {
        Current = t;
        if (persist) SaveCurrent();
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

        foreach (Form f in Application.OpenForms)
            ThemeApplier.Apply(f, Current);
    }

    // =========================
    // Startup config (stored in startupTheme.json)
    // =========================
    public static void LoadStartupConfig()
    {
        try
        {
            if (!File.Exists(StartupConfigPath)) return;
            var json = File.ReadAllText(StartupConfigPath);
            var cfg = JsonSerializer.Deserialize<StartupThemeConfig>(json, JsonOptions);
            if (cfg is not null) StartupConfig = cfg;
        }
        catch
        {
            // fail-soft
        }
    }

    public static void SaveStartupConfig()
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            var json = JsonSerializer.Serialize(StartupConfig, JsonOptions);
            File.WriteAllText(StartupConfigPath, json);
        }
        catch
        {
            // fail-soft
        }
    }

    // =========================
    // Load / Save active theme (theme.json)
    // =========================
    public static void LoadAtStartup()
    {
        // 0) Load startup policy
        LoadStartupConfig();

        // 1) Load custom presets into registry (so dropdown sees them)
        LoadCustomPresetsIntoRegistry();

        // 2) If policy says "force default preset", do it (and stop)
        if (StartupConfig.ForceDefaultPreset &&
            !string.IsNullOrWhiteSpace(StartupConfig.DefaultPresetName) &&
            _presets.TryGetValue(StartupConfig.DefaultPresetName, out var forced))
        {
            Current = forced.Clone();
            return;
        }

        // 3) fallback: first registered preset if nothing yet
        if (Current is null && _presets.Count > 0)
            Current = _presets.Values.First().Clone();

        // 4) load last active theme from theme.json (user choice)
        try
        {
            if (!File.Exists(ThemeFilePath)) return;

            var json = File.ReadAllText(ThemeFilePath);
            var loaded = JsonSerializer.Deserialize<ThemeSettings>(json, JsonOptions);
            if (loaded is null) return;

            // If name matches a known preset: use preset base + loaded overrides win
            if (_presets.TryGetValue(loaded.Name, out var preset))
            {
                var merged = preset.Clone();
                foreach (var kv in loaded.Overrides)
                    merged.Overrides[kv.Key] = kv.Value;

                Current = merged;
            }
            else
            {
                // pure custom theme
                Current = loaded;
            }
        }
        catch
        {
            // fail-soft
        }
    }

    public static void SaveCurrent()
    {
        if (Current is null) return;

        try
        {
            Directory.CreateDirectory(AppDataDir);
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(ThemeFilePath, json);
        }
        catch
        {
            // fail-soft
        }
    }

    // =========================
    // Custom themes as presets
    // =========================
    public static void SaveCurrentAsCustomPreset(string name)
    {
        if (Current is null) return;

        try
        {
            Directory.CreateDirectory(CustomThemesDir);

            var copy = Current.Clone();
            copy.Name = name;

            var path = Path.Combine(CustomThemesDir, $"{SanitizeFileName(name)}.json");
            var json = JsonSerializer.Serialize(copy, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch
        {
            // fail-soft
        }
    }

    private static void LoadCustomPresetsIntoRegistry()
    {
        try
        {
            if (!Directory.Exists(CustomThemesDir)) return;

            foreach (var file in Directory.EnumerateFiles(CustomThemesDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var loaded = JsonSerializer.Deserialize<ThemeSettings>(json, JsonOptions);
                    if (loaded is null) continue;

                    // Register custom as preset so UI can pick it
                    _presets[loaded.Name] = loaded;
                }
                catch
                {
                    // ignore individual broken custom
                }
            }
        }
        catch
        {
            // fail-soft
        }
    }

    private static string SanitizeFileName(string s)
    {
        foreach (var ch in Path.GetInvalidFileNameChars())
            s = s.Replace(ch, '_');
        return s.Trim();
    }
}
