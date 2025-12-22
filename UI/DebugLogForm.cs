#nullable disable
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using StruxureGuard.Core.Logging;

namespace StruxureGuard.UI;

public sealed class DebugLogForm : Form
{
    private readonly InMemoryLogSink _sink;

    private readonly ToolStrip _strip;
    private readonly ToolStripButton _btnClear;
    private readonly ToolStripButton _btnCopyAll;
    private readonly ToolStripButton _btnCopySel;
    private readonly ToolStripButton _btnAutoScroll;
    private readonly ToolStripButton _btnPause;

    private readonly ToolStripButton _btnOpenFolder;
    private readonly ToolStripButton _btnOpenToday;
    private readonly ToolStripButton _btnExportView;
    private readonly ToolStripButton _btnClearFilters;

    private readonly ToolStripLabel _lblMin;
    private readonly ToolStripComboBox _cmbMinLevel;

    private readonly ToolStripLabel _lblCat;
    private readonly ToolStripTextBox _txtCat;

    private readonly ToolStripLabel _lblText;
    private readonly ToolStripTextBox _txtText;

    private readonly ToolStripSeparator _sepFind;
    private readonly ToolStripLabel _lblFind;
    private readonly ToolStripTextBox _txtFind;
    private readonly ToolStripButton _btnFindNext;

    private readonly StatusStrip _status;
    private readonly ToolStripStatusLabel _stDisplayed;
    private readonly ToolStripStatusLabel _stCached;
    private readonly ToolStripStatusLabel _stDropped;
    private readonly ToolStripStatusLabel _stSeq;

    private readonly RichTextBox _box;

    private readonly System.Windows.Forms.Timer _timer;
    private readonly System.Windows.Forms.Timer _filterTimer;

    private long _lastSeq;
    private int _lastFindIndex;

    private readonly List<LogEvent> _cacheAll = new();
    private int _displayedCount;

    private bool _filterDirty;

    private readonly Color _neutralBack;
    private readonly Color _neutralFore;

    public DebugLogForm(InMemoryLogSink sink)
    {
        _sink = sink;

        Text = "Debug Log";
        Width = 1300;
        Height = 500;
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;

        // ===== TOOLSTRIP =====
        _strip = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            Dock = DockStyle.Top
        };

        _btnClear = new ToolStripButton("Clear");
        _btnClear.Click += (_, __) =>
        {
            Log.Info("ui.logviewer", "Clear clicked");
            _sink.Clear();
            _cacheAll.Clear();
            _box.Clear();
            _lastSeq = 0;
            _displayedCount = 0;
            _lastFindIndex = 0;
            UpdateStatus();
        };

        _btnCopyAll = new ToolStripButton("Copy all");
        _btnCopyAll.Click += (_, __) =>
        {
            if (!string.IsNullOrWhiteSpace(_box.Text))
                Clipboard.SetText(_box.Text);

            Log.Info("ui.logviewer", "Copy all clicked");
        };

        _btnCopySel = new ToolStripButton("Copy selection");
        _btnCopySel.Click += (_, __) =>
        {
            var sel = _box.SelectedText;
            if (!string.IsNullOrWhiteSpace(sel))
                Clipboard.SetText(sel);

            Log.Info("ui.logviewer", "Copy selection clicked");
        };

        _btnOpenFolder = new ToolStripButton("Open log folder");
        _btnOpenFolder.Click += (_, __) =>
        {
            try
            {
                var folder = Log.LogFolder;
                Directory.CreateDirectory(folder);

                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });

                Log.Info("ui.logviewer", $"Open log folder clicked: {folder}");
            }
            catch (Exception ex)
            {
                Log.Error("ui.logviewer", "Open log folder failed", ex);
            }
        };

        _btnOpenToday = new ToolStripButton("Open today log");
        _btnOpenToday.Click += (_, __) =>
        {
            try
            {
                var path = Log.CurrentLogFilePath;
                if (!File.Exists(path))
                {
                    MessageBox.Show(this, "Today log file does not exist yet.", "Open today log",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Log.Warn("ui.logviewer", $"Open today log: file not found: {path}");
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });

                Log.Info("ui.logviewer", $"Open today log clicked: {path}");
            }
            catch (Exception ex)
            {
                Log.Error("ui.logviewer", "Open today log failed", ex);
            }
        };

        _btnExportView = new ToolStripButton("Export view");
        _btnExportView.Click += (_, __) =>
        {
            try
            {
                using var sfd = new SaveFileDialog
                {
                    Title = "Export log view",
                    Filter = "Text (*.txt)|*.txt|Log (*.log)|*.log|All files (*.*)|*.*",
                    FileName = $"logview_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                File.WriteAllText(sfd.FileName, _box.Text ?? "", Encoding.UTF8);
                Log.Info("ui.logviewer", $"Export view saved: {sfd.FileName}");
            }
            catch (Exception ex)
            {
                Log.Error("ui.logviewer", "Export view failed", ex);
            }
        };

        _btnAutoScroll = new ToolStripButton("AutoScroll")
        {
            CheckOnClick = true,
            Checked = true
        };
        _btnAutoScroll.CheckedChanged += (_, __) =>
        {
            Log.Info("ui.logviewer", $"AutoScroll toggled => {(_btnAutoScroll.Checked ? "ON" : "OFF")}");

            if (_btnAutoScroll.Checked && !_btnPause.Checked)
            {
                _box.SelectionStart = _box.TextLength;
                _box.SelectionLength = 0;
                _box.ScrollToCaret();
            }
        };

        _btnPause = new ToolStripButton("Pause")
        {
            CheckOnClick = true,
            Checked = false
        };
        _btnPause.CheckedChanged += (_, __) =>
        {
            Text = _btnPause.Checked ? "Debug Log (Paused)" : "Debug Log";
            Log.Info("ui.logviewer", $"Pause toggled => {(_btnPause.Checked ? "ON" : "OFF")}");

            if (!_btnPause.Checked)
                Pump(force: true);
        };

        _lblMin = new ToolStripLabel("Min:");
        _cmbMinLevel = new ToolStripComboBox { Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbMinLevel.Items.AddRange(new object[] { "Trace", "Debug", "Info", "Warn", "Error", "Fatal" });
        _cmbMinLevel.SelectedIndex = 0;
        _cmbMinLevel.SelectedIndexChanged += (_, __) => MarkFilterDirty();

        _lblCat = new ToolStripLabel("Cat:");
        _txtCat = new ToolStripTextBox { Width = 140 };
        _txtCat.TextChanged += (_, __) => MarkFilterDirty();

        _lblText = new ToolStripLabel("Text:");
        _txtText = new ToolStripTextBox { Width = 180 };
        _txtText.TextChanged += (_, __) => MarkFilterDirty();

        _btnClearFilters = new ToolStripButton("Clear filters");
        _btnClearFilters.Click += (_, __) =>
        {
            _cmbMinLevel.SelectedIndex = 0;
            _txtCat.Text = "";
            _txtText.Text = "";
            Log.Info("ui.logviewer", "Clear filters clicked");
            MarkFilterDirty(force: true);
        };

        _sepFind = new ToolStripSeparator();
        _lblFind = new ToolStripLabel("Find:");
        _txtFind = new ToolStripTextBox { Width = 220 };
        _txtFind.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                FindNext();
                e.SuppressKeyPress = true;
            }
        };

        _btnFindNext = new ToolStripButton("Next");
        _btnFindNext.Click += (_, __) => FindNext();

        _strip.Items.AddRange(new ToolStripItem[]
        {
            _btnClear,
            new ToolStripSeparator(),
            _btnCopyAll,
            _btnCopySel,
            new ToolStripSeparator(),
            _btnOpenFolder,
            _btnOpenToday,
            _btnExportView,
            new ToolStripSeparator(),
            _btnAutoScroll,
            _btnPause,
            new ToolStripSeparator(),
            _lblMin, _cmbMinLevel,
            _lblCat, _txtCat,
            _lblText, _txtText,
            _btnClearFilters,
            _sepFind,
            _lblFind, _txtFind, _btnFindNext
        });

        // ===== STATUS =====
        _status = new StatusStrip { Dock = DockStyle.Bottom };
        _stDisplayed = new ToolStripStatusLabel("Displayed: 0");
        _stCached = new ToolStripStatusLabel("Cached: 0");
        _stDropped = new ToolStripStatusLabel("Dropped: 0");
        _stSeq = new ToolStripStatusLabel("LastSeq: 0") { Spring = true };

        _status.Items.Add(_stDisplayed);
        _status.Items.Add(new ToolStripStatusLabel("|"));
        _status.Items.Add(_stCached);
        _status.Items.Add(new ToolStripStatusLabel("|"));
        _status.Items.Add(_stDropped);
        _status.Items.Add(new ToolStripStatusLabel("|"));
        _status.Items.Add(_stSeq);

        // ===== TEXTBOX =====
        _box = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both,
            DetectUrls = false,
            HideSelection = false,
            Font = new Font("Consolas", 9f),
            BorderStyle = BorderStyle.FixedSingle
        };

        _neutralBack = _box.BackColor;
        _neutralFore = _box.ForeColor;

        // ===== IMPORTANT FIX =====
        // Fill eerst toevoegen, daarna top/bottom; zo begint regel 1 niet "onder" de ToolStrip.
        Controls.Add(_box);
        Controls.Add(_status);
        Controls.Add(_strip);

        // ESC sluit, Ctrl+A select all
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }

            if (e.Control && e.KeyCode == Keys.A)
            {
                _box.SelectAll();
                e.Handled = true;
            }
        };

        // Context menu
        var ctx = new ContextMenuStrip();
        ctx.Items.Add("Copy", null, (_, __) =>
        {
            var sel = _box.SelectedText;
            if (!string.IsNullOrWhiteSpace(sel))
                Clipboard.SetText(sel);
        });
        ctx.Items.Add("Copy all", null, (_, __) =>
        {
            if (!string.IsNullOrWhiteSpace(_box.Text))
                Clipboard.SetText(_box.Text);
        });
        ctx.Items.Add("Select all", null, (_, __) => _box.SelectAll());
        _box.ContextMenuStrip = ctx;

        // Filter debounce timer
        _filterTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _filterTimer.Tick += (_, __) =>
        {
            _filterTimer.Stop();
            if (_filterDirty) ApplyFiltersAndRebuild();
        };

        // Pump timer
        _timer = new System.Windows.Forms.Timer { Interval = 150 };
        _timer.Tick += (_, __) => Pump();
        _timer.Start();

        FormClosed += (_, __) =>
        {
            _timer.Stop();
            _filterTimer.Stop();
        };

        // initial load
        Pump(force: true);
    }

    private void MarkFilterDirty(bool force = false)
    {
        _filterDirty = true;
        _lastFindIndex = 0;

        if (force)
        {
            _filterTimer.Stop();
            ApplyFiltersAndRebuild();
            return;
        }

        _filterTimer.Stop();
        _filterTimer.Start();
    }

    private void Pump(bool force = false)
    {
        if (!force && _btnPause.Checked) return;

        // als filters net gewijzigd zijn: eerst rebuild (via debounce). Geen “append” met oude filter.
        if (_filterDirty && !force) return;

        // init snapshot (eerste keer)
        if (force && _cacheAll.Count == 0)
        {
            var snap = _sink.SnapshotEvents();
            if (snap.Count > 0)
            {
                _cacheAll.AddRange(snap);
                _lastSeq = snap[^1].Sequence;
            }

            ApplyFiltersAndRebuild();
            return;
        }

        var newEvents = _sink.SnapshotEventsSince(_lastSeq);
        if (newEvents.Count == 0)
        {
            if (force) UpdateStatus();
            return;
        }

        _cacheAll.AddRange(newEvents);
        _lastSeq = newEvents[^1].Sequence;

        bool wasAtBottom = _btnAutoScroll.Checked && IsAtBottom();

        _box.SuspendLayout();

        foreach (var e in newEvents)
        {
            if (PassFilter(e))
            {
                AppendColored(e);
                _displayedCount++;
            }
        }

        if (_btnAutoScroll.Checked && wasAtBottom)
        {
            _box.SelectionStart = _box.TextLength;
            _box.SelectionLength = 0;
            _box.ScrollToCaret();
        }

        _box.ResumeLayout();

        UpdateStatus();
    }

    private void ApplyFiltersAndRebuild()
    {
        _filterDirty = false;

        bool scrollToEnd = _btnAutoScroll.Checked;

        _box.SuspendLayout();
        _box.Clear();

        _displayedCount = 0;

        foreach (var e in _cacheAll)
        {
            if (!PassFilter(e)) continue;
            AppendColored(e);
            _displayedCount++;
        }

        if (scrollToEnd)
        {
            _box.SelectionStart = _box.TextLength;
            _box.SelectionLength = 0;
            _box.ScrollToCaret();
        }

        _box.ResumeLayout();

        Log.Info("ui.logviewer", $"Filters applied: min={_cmbMinLevel.SelectedItem} cat='{_txtCat.Text}' text='{_txtText.Text}' displayed={_displayedCount} cached={_cacheAll.Count}");
        UpdateStatus();
    }

    private bool PassFilter(LogEvent e)
    {
        var min = SelectedMinLevel();
        if (e.Level < min) return false;

        var cat = (_txtCat.Text ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(cat))
        {
            if (e.Category is null) return false;
            if (e.Category.IndexOf(cat, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        var txt = (_txtText.Text ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(txt))
        {
            var hay = $"{e.Category} {e.Message}";
            if (e.Exception is not null)
                hay += " " + e.Exception.GetType().Name + " " + e.Exception.Message;

            if (hay.IndexOf(txt, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        return true;
    }

    private LogLevelEx SelectedMinLevel()
    {
        var s = _cmbMinLevel.SelectedItem as string ?? "Trace";
        return s switch
        {
            "Debug" => LogLevelEx.Debug,
            "Info" => LogLevelEx.Info,
            "Warn" => LogLevelEx.Warn,
            "Error" => LogLevelEx.Error,
            "Fatal" => LogLevelEx.Fatal,
            _ => LogLevelEx.Trace
        };
    }

    private void UpdateStatus()
    {
        _stDisplayed.Text = $"Displayed: {_displayedCount}";
        _stCached.Text = $"Cached: {_cacheAll.Count}";
        _stDropped.Text = $"Dropped: {_sink.DroppedCount}";
        _stSeq.Text = $"LastSeq: {_lastSeq}";
    }

    private bool IsAtBottom()
    {
        if (_box.TextLength == 0) return true;
        int lastVisible = _box.GetCharIndexFromPosition(new Point(1, _box.ClientSize.Height - 1));
        return lastVisible >= _box.TextLength - 2;
    }

    private void AppendColored(LogEvent e)
    {
        var line = FormatLine(e);

        (Color back, Color fore) = e.Level switch
        {
            LogLevelEx.Fatal => (Color.FromArgb(60, 20, 20), Color.FromArgb(255, 220, 220)),
            LogLevelEx.Error => (Color.FromArgb(45, 18, 18), Color.FromArgb(255, 210, 210)),
            LogLevelEx.Warn  => (Color.FromArgb(45, 35, 15), Color.FromArgb(255, 240, 200)),
            LogLevelEx.Info  => (_neutralBack, _neutralFore),
            LogLevelEx.Debug => (_neutralBack, Color.FromArgb(180, 180, 180)),
            LogLevelEx.Trace => (_neutralBack, Color.FromArgb(150, 150, 150)),
            _ => (_neutralBack, _neutralFore)
        };

        _box.SelectionStart = _box.TextLength;
        _box.SelectionLength = 0;

        _box.SelectionBackColor = back;
        _box.SelectionColor = fore;

        _box.AppendText(line + Environment.NewLine);

        _box.SelectionBackColor = _neutralBack;
        _box.SelectionColor = _neutralFore;
    }

    private static string FormatLine(LogEvent e)
    {
        var ex = e.Exception is null ? "" : $" | EX: {e.Exception.GetType().Name}: {e.Exception.Message}";
        return $"{e.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{e.Level}] ({e.Category}) {e.Message}{ex} [seq={e.Sequence} tid={e.ThreadId}]";
    }

    private void FindNext()
    {
        var q = (_txtFind.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(q)) return;

        var text = _box.Text ?? "";
        if (text.Length == 0) return;

        var idx = text.IndexOf(q, _lastFindIndex, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            idx = text.IndexOf(q, 0, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return;
        }

        _box.SelectionStart = idx;
        _box.SelectionLength = q.Length;
        _box.ScrollToCaret();
        _box.Focus();

        _lastFindIndex = idx + q.Length;
    }
}
