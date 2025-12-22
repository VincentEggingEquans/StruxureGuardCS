using System.Diagnostics;
using StruxureGuard.Core.Logging;
using StruxureGuard.UI.DevTools;
using StruxureGuard.Styling;

namespace StruxureGuard.UI;

public sealed class MainForm : Form
{
    private readonly Stopwatch _f8Hold = new();

    private readonly Stopwatch _f2Hold = new();

    private readonly System.Windows.Forms.Timer _f8Timer;

    private readonly System.Windows.Forms.Timer _f2Timer;

    private DebugLogForm? _logForm;

    private Form? _themeTool;

    private ToolboxForm? _toolbox;

    private Label _statusLabel = null!;
    private Button _btnRapportage = null!;
    private Button _btnOnderhoud = null!;

    public MainForm()
    {
        Text = "StruxureGuard";
        Width = 520;
        Height = 360;
        StartPosition = FormStartPosition.CenterScreen;

        KeyPreview = true;

        _f8Timer = new System.Windows.Forms.Timer { Interval = 50 };
        _f8Timer.Tick += (_, __) => CheckF8Hold();

        _f2Timer = new System.Windows.Forms.Timer { Interval = 50 };
        _f2Timer.Tick += (_, __) => CheckF2Hold();

        BuildLayout();

        Log.Info("ui", "MainForm constructed");
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ThemeManager.ApplyTheme(this);
        Log.Info("ui", "MainForm loaded");
        UpdateStatus("Ready.");
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            RowCount = 4,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var title = new Label
        {
            Text = "StruxureGuard",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 18, FontStyle.Bold),
            Dock = DockStyle.Fill
        };
        root.Controls.Add(title, 0, 0);

        _btnRapportage = new Button
        {
            Text = "Rapportage Generator",
            Dock = DockStyle.Fill,
            Height = 44
        };
        _btnRapportage.Click += (_, __) =>
        {
            Log.Info("tool.rapportage", "Rapportage Generator opened (button)");
            MessageBox.Show(this, "Placeholder: open Rapportage Generator form", "Rapportage");
        };
        root.Controls.Add(_btnRapportage, 0, 1);

        _btnOnderhoud = new Button
        {
            Text = "Onderhoud Rapportage",
            Dock = DockStyle.Fill,
            Height = 44
        };
        _btnOnderhoud.Click += (_, __) =>
        {
            Log.Info("tool.onderhoud", "Onderhoud Rapportage opened (button)");
            MessageBox.Show(this, "Placeholder: open Onderhoud Rapportage form", "Onderhoud");
        };
        root.Controls.Add(_btnOnderhoud, 0, 2);

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft
        };
        root.Controls.Add(_statusLabel, 0, 3);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Alt+L => debug log viewer
        if (e.Alt && e.KeyCode == Keys.L)
        {
            e.SuppressKeyPress = true;
            OpenLogViewer();
            return;
        }

        // F8 hold => toolbox
        if (e.KeyCode == Keys.F8)
        {
            if (!_f8Hold.IsRunning)
            {
                _f8Hold.Restart();
                _f8Timer.Start();
                Log.Info("hotkey", "F8 hold started");
            }
            return;
        }

        // F2 hold => Theme Manager
        if (e.KeyCode == Keys.F2)
        {
            if (!_f2Hold.IsRunning)
            {
                _f2Hold.Restart();
                _f2Timer.Start();
                Log.Info("hotkey", "F2 hold started");
            }
            return;
        }


    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        if (e.KeyCode == Keys.F8)
        {
            _f8Timer.Stop();
            _f8Hold.Reset();
            UpdateStatus("Ready.");
            Log.Info("hotkey", "F8 hold released/cancelled");
        }
    
        if (e.KeyCode == Keys.F2)
        {
            _f2Timer.Stop();
            _f2Hold.Reset();
            Log.Info("hotkey", "F2 hold released/cancelled");
        }
    
    }

    private void CheckF8Hold()
    {
        if (!_f8Hold.IsRunning) return;

        if (_f8Hold.ElapsedMilliseconds >= 2000)
        {
            _f8Timer.Stop();
            _f8Hold.Reset();

            Log.Info("hotkey", "F8 hold triggered => open toolbox");
            OpenToolbox();
        }
    }

    private void CheckF2Hold()
    {
        if (!_f2Hold.IsRunning) return;

        if (_f2Hold.ElapsedMilliseconds >= 2000)
        {
            _f2Timer.Stop();
            _f2Hold.Reset();

            Log.Info("hotkey", "F2 hold triggered => open theme manager");
            OpenThemeManager();
        }
    }



    private void OpenLogViewer()
    {
        if (_logForm is { IsDisposed: false })
        {
            _logForm.BringToFront();
            _logForm.Activate();
            return;
        }

        Log.Info("ui", "Debug log opened (Alt+L)");
        _logForm = new DebugLogForm(Log.Memory);
        _logForm.Show(this);
    }

    private void OpenToolbox()
    {
        if (_toolbox is { IsDisposed: false })
        {
            _toolbox.BringToFront();
            _toolbox.Activate();
            return;
        }

        _toolbox = new ToolboxForm();
        _toolbox.StartPosition = FormStartPosition.CenterParent;
        _toolbox.Show(this);
    }

    private void OpenThemeManager()
    {
        Log.Warn("theme", "Theme Manager opened via hidden shortcut");

        if (_themeTool is { IsDisposed: false })
        {
            _themeTool.BringToFront();
            _themeTool.Activate();
            return;
        }

        _themeTool = new UiStyleToolForm();
        _themeTool.StartPosition = FormStartPosition.CenterScreen;
        _themeTool.Show(this);
    }


    private void UpdateStatus(string text) => _statusLabel.Text = text;
}
