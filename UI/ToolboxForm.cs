using StruxureGuard.Core.Logging;
using StruxureGuard.Core.Tools;
using StruxureGuard.Styling;
using StruxureGuard.UI.DevTools;
using StruxureGuard.UI.Tools;

namespace StruxureGuard.UI;

public sealed class ToolboxForm : Form
{
    private readonly Dictionary<string, Form> _openTools = new(StringComparer.OrdinalIgnoreCase);

    private readonly TableLayoutPanel _root;
    private readonly Label _title;
    private readonly Button _btnClose;

    private DebugLogForm? _logForm;
    private Form? _themeTool;

    public ToolboxForm()
    {
        Text = "StruxureGuard Toolbox";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 420;
        Height = 520;

        KeyPreview = true;

        var tools = ToolCatalog.Tools;

        _root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = tools.Count + 3 // title + tools + spacer + close
        };

        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); // title
        for (int i = 0; i < tools.Count; i++)
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // buttons
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // spacer
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); // close

        Controls.Add(_root);

        _title = new Label
        {
            Text = "Selecteer een tool:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(Font.FontFamily, 11f, FontStyle.Bold)
        };
        _root.Controls.Add(_title, 0, 0);

        // Build tool buttons from catalog
        for (int i = 0; i < tools.Count; i++)
        {
            var def = tools[i];
            var btn = MakeButton(def.ButtonText, () =>
            {
                Log.Info("toolbox", $"CLICK tool button: key='{def.Key}' label='{def.ButtonText}'");
                OpenSingleton(def.Key, def.ButtonText, () => ToolCatalog.CreateForm(def.Key));
            });

            _root.Controls.Add(btn, 0, i + 1);
        }

        _btnClose = new Button
        {
            Text = "Sluiten",
            Dock = DockStyle.Fill,
            Height = 36
        };
        _btnClose.Click += (_, __) => Close();
        _root.Controls.Add(_btnClose, 0, tools.Count + 2);

        // Keyboard shortcuts: ESC closes toolbox, Alt+L opens log
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                Close();
                return;
            }

            if (e.Alt && e.KeyCode == Keys.L)
            {
                e.Handled = true;
                OpenLogViewer();
                return;
            }
        };

        FormClosing += (_, __) => CloseAllTools();

        // Context menu
        var ctx = new ContextMenuStrip();
        ctx.Items.Add("UI Style Tool (Theme Manager)", null, (_, __) => OpenThemeTool());
        ctx.Items.Add("Debug Log", null, (_, __) => OpenLogViewer());
        ctx.Items.Add(new ToolStripSeparator());
        ctx.Items.Add("Close", null, (_, __) => Close());
        ContextMenuStrip = ctx;

        Log.Info("toolbox", "Toolbox opened");
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ThemeManager.ApplyTheme(this);
    }

    private static Button MakeButton(string text, Action onClick)
    {
        var b = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            Height = 34
        };
        b.Click += (_, __) => onClick();
        return b;
    }

    private void OpenSingleton(string key, string label, Func<Form> factory)
    {
        if (_openTools.TryGetValue(key, out var existing) && !existing.IsDisposed)
        {
            existing.WindowState = FormWindowState.Normal;
            existing.BringToFront();
            existing.Activate();

            Log.Info("toolbox",
                $"Activated tool: key='{key}' label='{label}' formType='{existing.GetType().FullName}'");
            return;
        }

        var form = factory();
        _openTools[key] = form;

        form.FormClosed += (_, __) =>
        {
            if (_openTools.TryGetValue(key, out var f) && ReferenceEquals(f, form))
                _openTools.Remove(key);
        };

        ThemeManager.ApplyTheme(form);

        form.StartPosition = FormStartPosition.CenterParent;
        form.Show(this);
        form.BringToFront();
        form.Activate();

        Log.Info("toolbox",
            $"Opened tool: key='{key}' label='{label}' formType='{form.GetType().FullName}'");
    }

    private void CloseAllTools()
    {
        foreach (var kv in _openTools.ToList())
        {
            try
            {
                var f = kv.Value;
                if (f is { IsDisposed: false })
                    f.Close();
            }
            catch { }
        }
        _openTools.Clear();

        try { _logForm?.Close(); } catch { }
        try { _themeTool?.Close(); } catch { }
    }

    private void OpenLogViewer()
    {
        if (_logForm is { IsDisposed: false })
        {
            _logForm.BringToFront();
            _logForm.Activate();
            return;
        }

        _logForm = new DebugLogForm(Log.Memory);
        ThemeManager.ApplyTheme(_logForm);
        _logForm.Show(this);
        _logForm.BringToFront();
        _logForm.Activate();
    }

    private void OpenThemeTool()
    {
        if (_themeTool is { IsDisposed: false })
        {
            _themeTool.BringToFront();
            _themeTool.Activate();
            return;
        }

        _themeTool = new UiStyleToolForm();
        ThemeManager.ApplyTheme(_themeTool);
        _themeTool.Show(this);
        _themeTool.BringToFront();
        _themeTool.Activate();
    }
}
