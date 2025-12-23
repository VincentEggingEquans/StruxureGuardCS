using StruxureGuard.Core.Logging;
using StruxureGuard.Styling;
using StruxureGuard.UI;
using StruxureGuard.UI.Hotkeys;

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

        var debugLogHost = new DebugLogHost();
        Application.AddMessageFilter(new GlobalHotkeyFilter(debugLogHost.OpenOrActivate));

        ThemePresets.RegisterAll();
        ThemeManager.LoadAtStartup();

        // ✅ NEW: auto theme new forms/controls/toolstrips
        ThemeManager.EnableAutoTheming();

#if DEBUG
        Log.Warn("auth", "DEBUG build: login bypass active (LoginForm skipped).");
        Application.Run(new MainForm());
#else
        using (var login = new LoginForm())
        {
            var dr = login.ShowDialog();
            if (dr != DialogResult.OK)
            {
                Log.Warn("auth", "Login failed/canceled => app exit.");
                return;
            }
        }

        Application.Run(new MainForm());
#endif
    }
}
