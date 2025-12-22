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

        ThemePresets.RegisterAll();
        ThemeManager.LoadAtStartup();

        // ✅ NEW: auto theme new forms/controls/toolstrips
        ThemeManager.EnableAutoTheming();

        Application.Run(new MainForm());
    }
}
