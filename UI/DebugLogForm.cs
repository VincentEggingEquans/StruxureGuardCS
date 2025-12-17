#nullable enable
using System.Text;
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

    private readonly ToolStripButton _fTrace;
    private readonly ToolStripButton _fDebug;
    private readonly ToolStripButton _fInfo;
    private readonly ToolStripButton _fWarn;
    private readonly ToolStripButton _fError;
    private readonly ToolStripButton _fFatal;

    private readonly ToolStripLabel _lblFind;
    private readonly ToolStripTextBox _txtFind;
    private readonly ToolStripButton _btnFindNext;

    private readonly RichTextBox _box;
    private readonly System.Windows.Forms.Timer _timer;

    private readonly Color _neutralBack;
    private readonly Color _neutralFore;

    private int _lastCount;
    private int _lastFindIndex;

    public DebugLogForm(InMemoryLogSink sink)
    {
        _sink = sink;

        Text = "Debug Log";
        Width = 1100;
        Height = 720;
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;

        _strip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };

        _btnClear = new ToolStripButton("Clear");
        _btnClear.Click += (_, __) =>
        {
            _sink.Clear();
            _box.Clear();
            _lastCount = 0;
            _lastFindIndex = 0;
        };

        _btnCopyAll = new ToolStripButton("Copy All");
        _btnCopyAll.Click += (_, __) =>
        {
            var t = _box.Text;
            if (!string.IsNullOrWhiteSpace(t))
            {
                // Clipboard api is not annotated for nullability; safe-guard anyway
                Clipboard.SetText(t);
            }
        };

        _btnCopySel = new ToolStripButton("Copy Selection");
        _btnCopySel.Click += (_, __) =>
        {
            var t = _box.SelectedText;
            if (!string.IsNullOrWhiteSpace(t))
            {
                Clipboard.SetText(t);
            }
        };

        _btnAutoScroll = new ToolStripButton("AutoScroll") { CheckOnClick = true, Checked = true };

        _fTrace = MakeFilter("Trace", true);
        _fDebug = MakeFilter("Debug", true);
        _fInfo  = MakeFilter("Info",  true);
        _fWarn  = MakeFilter("Warn",  true);
        _fError = MakeFilter("Error", true);
        _fFatal = MakeFilter("Fatal", true);

        _fTrace.CheckedChanged += (_, __) => Rebuild();
        _fDebug.CheckedChanged += (_, __) => Rebuild();
        _fInfo.CheckedChanged  += (_, __) => Rebuild();
        _fWarn.CheckedChanged  += (_, __) => Rebuild();
        _fError.CheckedChanged += (_, __) => Rebuild();
        _fFatal.CheckedChanged += (_, __) => Rebuild();

        _lblFind = new ToolStripLabel("Find:");
        _txtFind = new ToolStripTextBox { Width = 220 };
        _btnFindNext = new ToolStripButton("Next");
        _btnFindNext.Click += (_, __) => FindNext();

        _strip.Items.AddRange(new ToolStripItem[]
        {
            _btnClear,
            new ToolStripSeparator(),
            _btnCopyAll,
            _btnCopySel,
            new ToolStripSeparator(),
            _btnAutoScroll,
            new ToolStripSeparator(),
            _fTrace, _fDebug, _fInfo, _fWarn, _fError, _fFatal,
            new ToolStripSeparator(),
            _lblFind, _txtFind, _btnFindNext
        });

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

        Controls.Add(_box);
        Controls.Add(_strip);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
                return;
            }

            if (e.Control && e.KeyCode == Keys.F)
            {
                _txtFind.Focus();
                _txtFind.SelectAll();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Enter && _txtFind.Focused)
            {
                FindNext();
                e.Handled = true;
            }
        };

        _timer = new System.Windows.Forms.Timer { Interval = 150 };
        _timer.Tick += (_, __) => Pump();
        _timer.Start();

        FormClosed += (_, __) => _timer.Stop();

        Rebuild();
    }

    private static ToolStripButton MakeFilter(string text, bool initial)
        => new ToolStripButton(text) { CheckOnClick = true, Checked = initial, DisplayStyle = ToolStripItemDisplayStyle.Text };

    private bool PassFilter(LogLevelEx lvl) => lvl switch
    {
        LogLevelEx.Trace => _fTrace.Checked,
        LogLevelEx.Debug => _fDebug.Checked,
        LogLevelEx.Info  => _fInfo.Checked,
        LogLevelEx.Warn  => _fWarn.Checked,
        LogLevelEx.Error => _fError.Checked,
        LogLevelEx.Fatal => _fFatal.Checked,
        _ => true
    };

    private void Rebuild()
    {
        _box.Clear();
        _lastCount = 0;
        _lastFindIndex = 0;
        Pump(force: true);
    }

    private void Pump(bool force = false)
    {
        var count = _sink.Count;
        if (!force && count == _lastCount) return;

        var newEvents = _sink.SnapshotEventsFrom(_lastCount);
        _lastCount = count;

        if (newEvents.Count == 0) return;

        foreach (var ev in newEvents)
        {
            if (!PassFilter(ev.Level)) continue;
            AppendColored(ev);
        }

        if (_btnAutoScroll.Checked)
        {
            _box.SelectionStart = _box.TextLength;
            _box.ScrollToCaret();
        }
    }

    private void AppendColored(LogEvent e)
    {
        var line = FormatLine(e);
        var (fore, back, bold) = StyleFor(e.Level);

        _box.SelectionStart = _box.TextLength;
        _box.SelectionLength = 0;

        _box.SelectionColor = fore;
        _box.SelectionBackColor = back;

        // WinForms nullability: compiler may think Font can be null (it won't here)
        var baseFont = _box.Font ?? Control.DefaultFont;
        _box.SelectionFont = new Font(baseFont, bold ? FontStyle.Bold : FontStyle.Regular);

        _box.AppendText(line + Environment.NewLine);

        _box.SelectionColor = _neutralFore;
        _box.SelectionBackColor = _neutralBack;
        _box.SelectionFont = baseFont;
    }

    private static string FormatLine(LogEvent e)
    {
        var ex = e.Exception is null
            ? ""
            : $" | EX: {e.Exception.GetType().Name}: {e.Exception.Message}";

        return $"{e.Timestamp:HH:mm:ss.fff} [{e.Level}] ({e.Category}) {e.Message}{ex}";
    }

    private (Color Fore, Color Back, bool Bold) StyleFor(LogLevelEx lvl) => lvl switch
    {
        LogLevelEx.Fatal => (Color.White, Color.FromArgb(180, 30, 30), true),
        LogLevelEx.Error => (Color.FromArgb(255, 230, 230), Color.FromArgb(120, 25, 25), true),
        LogLevelEx.Warn  => (Color.FromArgb(255, 245, 220), Color.FromArgb(120, 90, 0), true),
        LogLevelEx.Info  => (Color.Gainsboro, _neutralBack, false),
        LogLevelEx.Debug => (Color.FromArgb(170, 170, 170), _neutralBack, false),
        LogLevelEx.Trace => (Color.FromArgb(140, 140, 140), _neutralBack, false),
        _ => (_neutralFore, _neutralBack, false)
    };

    private void FindNext()
    {
        var q = (_txtFind.Text ?? string.Empty).Trim();
        if (q.Length == 0) return;

        var text = _box.Text ?? string.Empty;
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
