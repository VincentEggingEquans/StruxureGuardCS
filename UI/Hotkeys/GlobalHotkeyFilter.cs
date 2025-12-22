using System;
using System.Windows.Forms;
using StruxureGuard.Core.Logging;

namespace StruxureGuard.UI.Hotkeys;

public sealed class GlobalHotkeyFilter : IMessageFilter
{
    private readonly Func<bool> _openDebugLog;

    public GlobalHotkeyFilter(Func<bool> openDebugLog)
    {
        _openDebugLog = openDebugLog ?? throw new ArgumentNullException(nameof(openDebugLog));
    }

    public bool PreFilterMessage(ref Message m)
    {
        const int WM_KEYDOWN = 0x0100;
        const int WM_SYSKEYDOWN = 0x0104; // Alt-combos often come in as SYSKEYDOWN
        const int WM_SYSCHAR = 0x0106;    // sometimes controls generate SYSCHAR instead

        if (m.Msg != WM_KEYDOWN && m.Msg != WM_SYSKEYDOWN && m.Msg != WM_SYSCHAR)
            return false;

        var key = (Keys)m.WParam.ToInt32();
        var mods = Control.ModifierKeys;

        // Alt+L
        if (key == Keys.L && mods.HasFlag(Keys.Alt))
        {
            try
            {
                Log.Info("hotkey", "Alt+L pressed => open debug log");
                _openDebugLog();
            }
            catch (Exception ex)
            {
                Log.Error("hotkey", $"Alt+L handler failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
            }

            // consume the key so it doesn't trigger menus/system beeps
            return true;
        }

        return false;
    }
}
