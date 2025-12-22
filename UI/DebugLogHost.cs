using System;
using StruxureGuard.Core.Logging;

namespace StruxureGuard.UI;

public sealed class DebugLogHost
{
    private DebugLogForm? _form;

    public bool OpenOrActivate()
    {
        try
        {
            if (_form == null || _form.IsDisposed)
            {
                _form = new DebugLogForm(Log.Memory);
                _form.FormClosed += (_, __) => _form = null;

                _form.Show();
                _form.Activate();

                Log.Info("ui", "Debug log opened (Alt+L)");
                return true;
            }

            if (!_form.Visible) _form.Show();
            _form.Activate();

            Log.Info("ui", "Debug log activated (Alt+L)");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("ui", $"OpenOrActivate DebugLog failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
            return false;
        }
    }
}
