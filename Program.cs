using StruxureGuard.Core.Logging;
using StruxureGuard.Styling;
using StruxureGuard.UI;

namespace StruxureGuard;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        Log.Init(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StruxureGuard",
            "Logs"));

        // 🔹 THEMING BOOTSTRAP
        ThemePresets.RegisterAll();

        var asm = typeof(StruxureGuard.Styling.ThemeSettings).Assembly;
        Log.Info("theme", $"ThemeSettings assembly: {asm.GetName().Name}");
        Log.Info("theme", $"Contains NordPreset type? {asm.GetType("StruxureGuard.Styling.Presets.NordPreset") != null}");

        ThemeManager.LoadAtStartup();

        Application.Run(new MainForm());
    }
}
