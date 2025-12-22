using System.Diagnostics;
using System.Runtime.InteropServices;
using StruxureGuard.Core.Logging;
using StruxureGuard.Styling;
using StruxureGuard.Core.Tools.Mkdir;
using StruxureGuard.UI.Controls;
using StruxureGuard.Core.Tools.Infrastructure;

namespace StruxureGuard.UI.Tools;

public sealed class MkdirToolForm : ToolBaseForm
{
    private const int PreviewColExistsWidth = 70;
    private const int MidGapWidth = 16;

    // Zet op false als je minder spam wil (nu: HEEL veel logging)
    private const bool VerboseProgressLogging = true;

    private TextBox _txtBase = null!;
    private Button _btnBrowse = null!;

    private Button _btnPasteSplit = null!;
    private Button _btnClean = null!;
    private Button _btnUnique = null!;

    private CheckBox _chkNumber = null!;
    private ComboBox _cmbNumberFormat = null!;
    private NumericUpDown _nudStart = null!;
    private NumericUpDown _nudPad = null!;

    private CheckBox _chkCopy = null!;
    private Button _btnPickFile = null!;
    private Label _lblFile = null!;
    private ComboBox _cmbCopyNaming = null!;
    private CheckBox _chkOpenAfter = null!;

    private ListView _preview = null!;

    private ColumnHeader _colPath = null!;
    private ColumnHeader _colExists = null!;
    private Label _lblPreviewInfo = null!;

    private ThemedProgressBar _progress = null!;
    private Label _lblProgress = null!;
    private Button _btnStart = null!;
    private Button _btnCancel = null!;

    private NamesTextBox _txtNames = null!;

    private string? _selectedFile;
    private DebugLogForm? _logForm;
    private readonly ToolTip _tt = new();

    public MkdirToolForm()
    {
        Text = "StruxureGuard MKDIR";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1200, 780);
        MinimumSize = new Size(980, 640);

        _tt.ShowAlways = true;
        _tt.InitialDelay = 250;
        _tt.ReshowDelay = 100;
        _tt.AutoPopDelay = 12000;

        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; }
            if (e.Alt && e.KeyCode == Keys.L) { OpenLogViewer(); e.Handled = true; }
        };

        BuildLayout();

        SizeChanged += (_, __) =>
        {
            if (!_preview.IsHandleCreated) return;
            ResizePreviewColumns();
        };
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ThemeManager.ApplyTheme(this);

        _txtBase.Text = Environment.CurrentDirectory;

        _cmbNumberFormat.SelectedIndex = 0;
        _cmbCopyNaming.SelectedIndex = 0;

        HookPreviewRefresh();
        RefreshPreview();

        UpdateUiState(false);

        ResizePreviewColumns();

        Log.Info("mkdir", "MKDIR tool opened");
        LogSnapshot("OnLoad");
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ResizePreviewColumns();
        Log.Info("mkdir", "Form shown");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        try
        {
            if (IsRunning)
            {
                Log.Warn("mkdir", "Form closing while running -> cancel requested");
                CancelRun();
            }
        }
        catch (Exception ex)
        {
            Log.Warn("mkdir", $"OnFormClosing cancel failed: {ex.GetType().Name}: {ex.Message}");
        }

        base.OnFormClosing(e);
    }


    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 4,
            AutoScroll = true
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Controls.Add(root);

        // Base row
        var baseRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        baseRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        baseRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        baseRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        baseRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));

        baseRow.Controls.Add(new Label
        {
            Text = "Doelmap:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 8, 4)
        }, 0, 0);

        _txtBase = new TextBox
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0),
            Height = 24
        };

        var baseBoxHost = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0) };
        baseBoxHost.Controls.Add(_txtBase);
        baseBoxHost.Resize += (_, __) =>
        {
            _txtBase.Width = baseBoxHost.ClientSize.Width;
            _txtBase.Left = 0;
            _txtBase.Top = Math.Max(0, (baseBoxHost.ClientSize.Height - _txtBase.Height) / 2);
        };
        baseRow.Controls.Add(baseBoxHost, 1, 0);

        _btnBrowse = new Button
        {
            Text = "Bladeren…",
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0, 0, 0, 3),
            Height = 28
        };
        _btnBrowse.Click += (_, __) => BrowseBase();

        var browseHost = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
        browseHost.Controls.Add(_btnBrowse);
        browseHost.Resize += (_, __) =>
        {
            _btnBrowse.Width = browseHost.ClientSize.Width;
            _btnBrowse.Left = 0;
            _btnBrowse.Top = Math.Max(0, (browseHost.ClientSize.Height - _btnBrowse.Height) / 2);
        };
        baseRow.Controls.Add(browseHost, 2, 0);

        root.Controls.Add(baseRow, 0, 0);

        // Content
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, MidGapWidth));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(content, 0, 1);

        // Left top
        var leftTop = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        leftTop.RowStyles.Clear();
        leftTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        leftTop.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        leftTop.Controls.Add(new Label
        {
            Text = "Mapnamen (1 per regel):",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 0, 2)
        }, 0, 0);

        _txtNames = new NamesTextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };

        leftTop.Controls.Add(_txtNames, 0, 1);
        content.Controls.Add(leftTop, 0, 0);

        // Right top
        var rightTop = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        rightTop.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        rightTop.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _lblPreviewInfo = new Label
        {
            Text = "Preview:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 0, 2)
        };
        rightTop.Controls.Add(_lblPreviewInfo, 0, 0);

        _preview = new ListView
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            Scrollable = true
        };

        _preview.HandleCreated += (_, __) => ResizePreviewColumns();
        _preview.OwnerDraw = true;
        _preview.DrawColumnHeader += Preview_DrawColumnHeader;
        _preview.DrawSubItem += Preview_DrawSubItem;

        _colPath = new ColumnHeader { Text = "Pad", TextAlign = HorizontalAlignment.Left };
        _colExists = new ColumnHeader { Text = "Bestaat", Width = PreviewColExistsWidth, TextAlign = HorizontalAlignment.Center };
        _preview.Columns.AddRange(new[] { _colPath, _colExists });

        _preview.SizeChanged += (_, __) =>
        {
            if (_preview.IsDisposed || !_preview.IsHandleCreated) return;
            _preview.BeginInvoke(new Action(ResizePreviewColumns));
        };

        var previewHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(2)
        };

        previewHost.Controls.Add(_preview);
        rightTop.Controls.Add(previewHost, 0, 1);

        content.Controls.Add(rightTop, 2, 0);

        // Options
        var options = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 4,
            Margin = new Padding(0, 10, 0, 0)
        };
        options.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        options.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        options.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        options.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        content.Controls.Add(options, 0, 1);
        content.SetColumnSpan(options, 3);

        var quickRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0)
        };

        _btnPasteSplit = new Button { Text = "Plak & splits (; / regels)", Width = 190, Height = 26 };
        _btnPasteSplit.Click += (_, __) => PasteAndSplitSemicolon();
        _tt.SetToolTip(_btnPasteSplit, "Plakt uit het clipboard en splitst op ';' en nieuwe regels.");

        _btnClean = new Button { Text = "Opschonen (trim/leeg)", Width = 180, Height = 26 };
        _btnClean.Click += (_, __) => CleanLines();

        _btnUnique = new Button { Text = "Dubbele verwijderen", Width = 170, Height = 26 };
        _btnUnique.Click += (_, __) => MakeUnique();

        quickRow.Controls.Add(_btnPasteSplit);
        quickRow.Controls.Add(_btnClean);
        quickRow.Controls.Add(_btnUnique);

        options.Controls.Add(quickRow, 0, 0);

        var numRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0)
        };

        _chkNumber = new CheckBox { Text = "Auto-nummering", AutoSize = true };

        var info = new Label
        {
            Text = "ⓘ",
            AutoSize = true,
            Cursor = Cursors.Hand,
            Padding = new Padding(4, 1, 6, 0)
        };
        _tt.SetToolTip(info,
            "Voegt automatisch een nummer toe aan elke mapnaam.\r\n" +
            "Voorbeeld: '01 - Naam' of '1. Naam' of 'Naam 01'.\r\n" +
            "Pad geeft het aantal cijfers aan (bijv. 2 = 01, 03, 15; 4 = 0001, 0002, 0010).");

        _cmbNumberFormat = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        _cmbNumberFormat.Items.AddRange(new object[] { "None", "01 - Name", "1. Name", "Name 01" });

        _nudStart = new NumericUpDown { Minimum = 1, Maximum = 9999, Value = 1, Width = 80 };
        _nudPad = new NumericUpDown { Minimum = 1, Maximum = 6, Value = 1, Width = 70 };

        numRow.Controls.Add(_chkNumber);
        numRow.Controls.Add(info);
        numRow.Controls.Add(new Label { Text = "Format:", AutoSize = true, Padding = new Padding(6, 5, 0, 0) });
        numRow.Controls.Add(_cmbNumberFormat);
        numRow.Controls.Add(new Label { Text = "Start:", AutoSize = true, Padding = new Padding(12, 5, 0, 0) });
        numRow.Controls.Add(_nudStart);
        numRow.Controls.Add(new Label { Text = "Pad:", AutoSize = true, Padding = new Padding(12, 5, 0, 0) });
        numRow.Controls.Add(_nudPad);

        options.Controls.Add(numRow, 0, 1);

        var copyRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 8, 0, 0)
        };
        copyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        copyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        copyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _chkCopy = new CheckBox { Text = "Kopieer bestand naar elke map", AutoSize = true, Dock = DockStyle.Fill };
        _chkCopy.CheckedChanged += (_, __) =>
        {
            Log.Info("mkdir", $"Copy toggled -> {_chkCopy.Checked}");
            if (!_chkCopy.Checked)
            {
                _selectedFile = null;
                _lblFile.Text = "Geen bestand geselecteerd";
                Log.Info("mkdir", "Copy disabled -> selected file cleared");
            }
            UpdateUiState(IsRunning);
        };
        copyRow.Controls.Add(_chkCopy, 0, 0);

        _btnPickFile = new Button
        {
            Text = "Kies bestand",
            Height = 26,
            Dock = DockStyle.Fill,
            Enabled = false,
            UseVisualStyleBackColor = true
        };
        _btnPickFile.Click += (_, __) => PickFile();
        copyRow.Controls.Add(_btnPickFile, 1, 0);

        _lblFile = new Label
        {
            Text = "Geen bestand geselecteerd",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        copyRow.Controls.Add(_lblFile, 2, 0);

        options.Controls.Add(copyRow, 0, 2);

        var namingRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 2,
            Margin = new Padding(0, 8, 0, 0)
        };
        namingRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        namingRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        namingRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        namingRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        namingRow.Controls.Add(new Label { Text = "", AutoSize = true }, 0, 0);

        namingRow.Controls.Add(new Label
        {
            Text = "Bestandsnaam:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 1, 0);

        _cmbCopyNaming = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled = false,
            Width = 260,
            Anchor = AnchorStyles.Left
        };
        _cmbCopyNaming.Items.AddRange(new object[]
        {
            "Behouden (originele naam)",
            "Hernoemen naar mapnaam"
        });
        _cmbCopyNaming.SelectedIndexChanged += (_, __) =>
        {
            Log.Info("mkdir", $"CopyNaming changed -> idx={_cmbCopyNaming.SelectedIndex}");
            RefreshPreview();
        };
        namingRow.Controls.Add(_cmbCopyNaming, 2, 0);

        _chkOpenAfter = new CheckBox
        {
            Text = "Open doelmap na afloop",
            AutoSize = true,
            Dock = DockStyle.Left
        };
        _chkOpenAfter.CheckedChanged += (_, __) => Log.Info("mkdir", $"OpenAfter toggled -> {_chkOpenAfter.Checked}");
        namingRow.Controls.Add(_chkOpenAfter, 0, 1);
        namingRow.SetColumnSpan(_chkOpenAfter, 4);

        options.Controls.Add(namingRow, 0, 3);

        // Progress row (jouw layout)
        var prog = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(0, 6, 0, 0),
            Margin = new Padding(0)
        };

        prog.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        prog.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));

        var hdr = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0)
        };

        hdr.Controls.Add(new Label
        {
            Text = "Voortgang:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 3, 8, 0)
        });

        _lblProgress = new Label
        {
            Text = "0 / 0",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 3, 0, 0)
        };

        hdr.Controls.Add(_lblProgress);
        prog.Controls.Add(hdr, 0, 0);

        _progress = new ThemedProgressBar
        {
            Dock = DockStyle.Fill,
            Height = 24,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Margin = new Padding(0, 2, 0, 0)
        };

        prog.Controls.Add(_progress, 0, 1);
        prog.SetColumnSpan(_progress, prog.ColumnCount);

        root.Controls.Add(prog, 0, 2);

        // Buttons row
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 4)
        };

        _btnStart = new Button { Text = "Start", Width = 160, Height = 30 };
        _btnStart.Click += async (_, __) => await StartAsync();

        _btnCancel = new Button { Text = "Cancel", Width = 160, Height = 30, Enabled = false };
        _btnCancel.Click += (_, __) =>
        {
            Log.Info("mkdir", "Cancel clicked");
            CancelRun();
        };

        buttons.Controls.Add(_btnStart);
        buttons.Controls.Add(_btnCancel);

        root.Controls.Add(buttons, 0, 3);
    }

    private void UpdateUiState(bool isRunning)
    {
        _txtBase.ReadOnly = isRunning;
        _btnBrowse.Enabled = !isRunning;

        _txtNames.ReadOnly = isRunning;

        _btnPasteSplit.Enabled = !isRunning;
        _btnClean.Enabled = !isRunning;
        _btnUnique.Enabled = !isRunning;

        _chkNumber.Enabled = !isRunning;
        _cmbNumberFormat.Enabled = !isRunning;
        _nudStart.Enabled = !isRunning;
        _nudPad.Enabled = !isRunning;

        _chkCopy.Enabled = !isRunning;
        _chkOpenAfter.Enabled = !isRunning;

        var copyEnabled = !isRunning && _chkCopy.Checked;
        _btnPickFile.Enabled = copyEnabled;
        _cmbCopyNaming.Enabled = copyEnabled;

        _btnStart.Enabled = !isRunning;
        _btnCancel.Enabled = isRunning;

        Log.Info("mkdir", $"UI state -> running={isRunning}, copyEnabled={copyEnabled}");
        RefreshPreview();
    }

    private void HookPreviewRefresh()
    {
        _txtBase.TextChanged += (_, __) => RefreshPreview();
        _txtNames.TextChanged += (_, __) => RefreshPreview();

        _chkNumber.CheckedChanged += (_, __) => RefreshPreview();
        _cmbNumberFormat.SelectedIndexChanged += (_, __) => RefreshPreview();
        _nudStart.ValueChanged += (_, __) => RefreshPreview();
        _nudPad.ValueChanged += (_, __) => RefreshPreview();

        _txtBase.TextChanged += (_, __) => Log.Info("mkdir", $"Base changed -> '{_txtBase.Text}'");
        _chkNumber.CheckedChanged += (_, __) => Log.Info("mkdir", $"Numbering toggled -> {_chkNumber.Checked}");
        _cmbNumberFormat.SelectedIndexChanged += (_, __) => Log.Info("mkdir", $"NumberFormat changed -> idx={_cmbNumberFormat.SelectedIndex}");
        _nudStart.ValueChanged += (_, __) => Log.Info("mkdir", $"StartNr changed -> {_nudStart.Value}");
        _nudPad.ValueChanged += (_, __) => Log.Info("mkdir", $"Pad changed -> {_nudPad.Value}");

        _chkCopy.CheckedChanged += (_, __) => Log.Info("mkdir", $"Copy toggled (Hook) -> {_chkCopy.Checked}");
        _chkOpenAfter.CheckedChanged += (_, __) => Log.Info("mkdir", $"OpenAfter toggled (Hook) -> {_chkOpenAfter.Checked}");
        _cmbCopyNaming.SelectedIndexChanged += (_, __) => Log.Info("mkdir", $"CopyNaming changed (Hook) -> idx={_cmbCopyNaming.SelectedIndex}");
    }

    private void ResizePreviewColumns()
    {
        if (_preview.IsDisposed || !_preview.IsHandleCreated || _preview.Columns.Count < 2)
            return;

        int w = _preview.ClientSize.Width;
        if (w <= 0) return;

        bool hasVScroll = false;
        int perPage = GetListViewCountPerPage(_preview);
        if (perPage > 0)
            hasVScroll = _preview.Items.Count > perPage;

        if (hasVScroll)
            w -= SystemInformation.VerticalScrollBarWidth;

        w -= 4;
        if (w < 0) w = 0;

        _colExists.Width = PreviewColExistsWidth;

        int pathWidth = w - _colExists.Width;
        if (pathWidth < 0) pathWidth = 0;

        _colPath.Width = pathWidth;
    }

    private void Preview_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        e.DrawBackground();
        var text = e.Header?.Text ?? string.Empty;

        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        flags |= (e.ColumnIndex == 1) ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left;

        TextRenderer.DrawText(e.Graphics, text, _preview.Font, e.Bounds, _preview.ForeColor, flags);
    }

    private void Preview_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        var selected = e.Item?.Selected ?? false;

        var back = selected ? SystemColors.Highlight : _preview.BackColor;
        var fore = selected ? SystemColors.HighlightText : _preview.ForeColor;

        using (var b = new SolidBrush(back))
            e.Graphics.FillRectangle(b, e.Bounds);

        var text = e.SubItem?.Text ?? string.Empty;

        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        flags |= (e.ColumnIndex == 1) ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left;

        TextRenderer.DrawText(e.Graphics, text, _preview.Font, e.Bounds, fore, flags);
    }

    private void BrowseBase()
    {
        Log.Info("mkdir", "BrowseBase opened");

        using var dlg = new FolderBrowserDialog { Description = "Selecteer doelmap", UseDescriptionForTitle = true };
        if (Directory.Exists(_txtBase.Text))
            dlg.SelectedPath = _txtBase.Text;

        if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
        {
            Log.Info("mkdir", $"BrowseBase selected: '{dlg.SelectedPath}'");
            _txtBase.Text = dlg.SelectedPath;
        }
        else
        {
            Log.Info("mkdir", "BrowseBase canceled/no selection");
        }
    }

    private void PickFile()
    {
        Log.Info("mkdir", "PickFile opened");

        using var dlg = new OpenFileDialog
        {
            Title = "Kies bestand om te kopiëren",
            CheckFileExists = true,
            CheckPathExists = true
        };

        if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.FileName))
        {
            _selectedFile = dlg.FileName;
            _lblFile.Text = Path.GetFileName(dlg.FileName);
            Log.Info("mkdir", $"PickFile selected: '{_selectedFile}'");
        }
        else
        {
            Log.Info("mkdir", "PickFile canceled/no selection");
        }

        RefreshPreview();
    }

    private void PasteAndSplitSemicolon()
    {
        Log.Info("mkdir", "PasteAndSplitSemicolon invoked");

        try
        {
            var text = Clipboard.GetText() ?? "";
            Log.Info("mkdir", $"Clipboard length={text.Length}");

            if (string.IsNullOrWhiteSpace(text))
            {
                Log.Warn("mkdir", "Clipboard empty/whitespace");
                return;
            }

            var parts = text
                .Replace("\r\n", "\n")
                .Split(new[] { '\n', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();

            Log.Info("mkdir", $"Parsed clipboard parts={parts.Count}");

            var existing = (_txtNames.Text ?? "").TrimEnd();
            var merged = (existing.Length == 0 ? "" : existing + Environment.NewLine) + string.Join(Environment.NewLine, parts);

            _txtNames.Text = merged;
            Log.Info("mkdir", $"Names updated, length={_txtNames.Text.Length}");
        }
        catch (Exception ex)
        {
            Log.Warn("mkdir", $"Clipboard paste failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
            MessageBox.Show(this, "Kon niet plakken uit clipboard.", "MKDIR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CleanLines()
    {
        Log.Info("mkdir", "CleanLines invoked");
        var before = _txtNames.Text?.Length ?? 0;

        var lines = ParseRawNames(_txtNames.Text);
        _txtNames.Text = string.Join(Environment.NewLine, lines);

        var after = _txtNames.Text.Length;
        Log.Info("mkdir", $"CleanLines done: beforeLen={before}, afterLen={after}, lines={lines.Count}");
    }

    private void MakeUnique()
    {
        Log.Info("mkdir", "MakeUnique invoked");
        var lines = ParseRawNames(_txtNames.Text);
        var beforeCount = lines.Count;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = new List<string>();
        foreach (var l in lines)
            if (seen.Add(l)) unique.Add(l);

        _txtNames.Text = string.Join(Environment.NewLine, unique);
        Log.Info("mkdir", $"MakeUnique done: before={beforeCount}, after={unique.Count}");
    }

    private void RefreshPreview()
    {
        var sw = Stopwatch.StartNew();

        var basePath = (_txtBase.Text ?? "").Trim();
        var names = BuildFinalFolderNames();

        _preview.BeginUpdate();
        _preview.Items.Clear();

        int existsCount = 0;

        foreach (var name in names)
        {
            var full = string.IsNullOrWhiteSpace(basePath) ? name : Path.Combine(basePath, name);
            var exists = !string.IsNullOrWhiteSpace(basePath) && Directory.Exists(full);
            if (exists) existsCount++;

            var it = new ListViewItem(full);
            it.SubItems.Add(exists ? "Ja" : "Nee");
            _preview.Items.Add(it);
        }

        _preview.EndUpdate();

        _lblPreviewInfo.Text = $"Preview: {names.Count} items ({existsCount} bestaan al)";

        ResizePreviewColumns();

        sw.Stop();
        Log.Info("mkdir", $"RefreshPreview: baseLen={basePath.Length}, items={names.Count}, exists={existsCount}, ms={sw.ElapsedMilliseconds}");
    }

    private async Task StartAsync()
    {
        var swTotal = Stopwatch.StartNew();

        Log.Info("mkdir", "============================================================");
        Log.Info("mkdir", "StartAsync invoked");
        LogSnapshot("StartAsync");

        if (IsRunning)
        {
            Log.Warn("mkdir", "Start ignored: already running");
            return;
        }

        var basePath = (_txtBase.Text ?? "").Trim();

        if (string.IsNullOrWhiteSpace(basePath))
        {
            Log.Warn("mkdir", "Start blocked: base path empty");
            MessageBox.Show(this, "Kies een basispad.", "MKDIR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!Directory.Exists(basePath))
        {
            Log.Warn("mkdir", $"Start blocked: base path not found: '{basePath}'");
            MessageBox.Show(this, "Basispad bestaat niet.", "MKDIR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var names = BuildFinalFolderNames();
        Log.Info("mkdir", $"BuildFinalFolderNames -> {names.Count} items");

        if (VerboseProgressLogging)
        {
            for (int i = 0; i < names.Count; i++)
                Log.Info("mkdir", $"  Name[{i + 1}]='{names[i]}'");
        }

        if (names.Count == 0)
        {
            Log.Warn("mkdir", "Start blocked: no names");
            MessageBox.Show(this, "Geen mapnamen opgegeven.", "MKDIR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_chkCopy.Checked)
        {
            if (string.IsNullOrWhiteSpace(_selectedFile))
            {
                Log.Warn("mkdir", "Start blocked: copy enabled but no file selected");
                MessageBox.Show(this, "Kopieer-optie staat aan maar er is geen bestand gekozen.", "MKDIR",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!File.Exists(_selectedFile))
            {
                Log.Warn("mkdir", $"Start blocked: selected file does not exist: '{_selectedFile}'");
                MessageBox.Show(this, "Gekozen bestand bestaat niet.", "MKDIR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        SetProgress(0, names.Count);

        // Tool is now stateless; it will build options from ctx.Parameters
        var tool = new MkdirTool();

        // Canonical run parameters (this is now the real input)
        var runParams = new Dictionary<string, string>
        {
            [MkdirParameterKeys.BasePath] = basePath,
            [MkdirParameterKeys.FolderNames] = string.Join("\n", names),

            [MkdirParameterKeys.CopyEnabled] = _chkCopy.Checked.ToString(),
            [MkdirParameterKeys.SourceFilePath] = _chkCopy.Checked ? (_selectedFile ?? "") : "",
            [MkdirParameterKeys.CopyNamingMode] =
                (_cmbCopyNaming.SelectedIndex == 1 ? CopyNamingMode.RenameToFolderName : CopyNamingMode.KeepOriginalName).ToString(),

            // Preserve current behavior
            [MkdirParameterKeys.OverwriteExistingCopiedFiles] = _chkCopy.Checked.ToString(),
            [MkdirParameterKeys.SkipCopyIfTargetExists] = "false",

            // Keep your current verbosity behavior
            [MkdirParameterKeys.VerboseLogging] = VerboseProgressLogging.ToString(),

            // Snapshot only
            [MkdirParameterKeys.TotalNames] = names.Count.ToString()
        };

        try
        {
            await RunToolAsync(
                tool: tool,
                parameters: runParams,
                toolLogTag: "mkdir",
                onCompleted: async result =>
                {
                    if (result.Success && !result.Canceled && _chkOpenAfter.Checked)
                    {
                        try
                        {
                            Log.Info("mkdir", $"Open-after: '{basePath}'");
                            Process.Start(new ProcessStartInfo { FileName = basePath, UseShellExecute = true });
                        }
                        catch (Exception ex)
                        {
                            Log.Warn("mkdir", $"Open-after failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
                        }
                    }

                    await Task.CompletedTask;
                },
                showWarningsOnSuccess: true,
                successMessageFactory: _ => "Klaar.");
        }
        catch (Exception ex)
        {
            Log.Error("mkdir", $"UI execution failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
            MessageBox.Show(this, "Er ging iets mis. Kijk in de log (Alt+L).", "MKDIR",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

    }


    private void SetProgress(int done, int total)
    {
        if (total <= 0)
        {
            _progress.Value = 0;
            _lblProgress.Text = "0 / 0";
            if (VerboseProgressLogging) Log.Info("mkdir", "Progress: 0/0 (total<=0)");
            return;
        }

        int pct = (int)Math.Round(done * 100.0 / total);
        if (pct < 0) pct = 0;
        if (pct > 100) pct = 100;

        _progress.Value = pct;
        _lblProgress.Text = $"{done} / {total}";

        if (VerboseProgressLogging)
            Log.Info("mkdir", $"Progress update: done={done}, total={total}, pct={pct}");
    }

    private void SafeUi(Action action)
    {
        if (IsDisposed) return;

        try
        {
            if (InvokeRequired)
            {
                if (!IsHandleCreated) return;
                BeginInvoke(action);
            }
            else
            {
                action();
            }
        }
        catch (ObjectDisposedException)
        {
            // ignore
        }
        catch (InvalidOperationException)
        {
            // handle not created / shutting down
        }
        catch (Exception ex)
        {
            Log.Warn("mkdir", $"SafeUi failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void LogSnapshot(string where)
    {
        try
        {
            Log.Info("mkdir", $"Snapshot[{where}]: Base='{_txtBase.Text}', NamesLen={_txtNames.Text?.Length ?? 0}, Running={IsRunning}");
            Log.Info("mkdir", $"Snapshot[{where}]: Numbering={_chkNumber.Checked}, FormatIdx={_cmbNumberFormat.SelectedIndex}, Start={_nudStart.Value}, Pad={_nudPad.Value}");
            Log.Info("mkdir", $"Snapshot[{where}]: Copy={_chkCopy.Checked}, SelectedFile='{_selectedFile ?? "<null>"}', CopyNamingIdx={_cmbCopyNaming.SelectedIndex}, OpenAfter={_chkOpenAfter.Checked}");
        }
        catch (Exception ex)
        {
            Log.Warn("mkdir", $"LogSnapshot failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void OpenLogViewer()
    {
        Log.Info("mkdir", "OpenLogViewer requested");

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

    private List<string> BuildFinalFolderNames()
    {
        var sw = Stopwatch.StartNew();

        var raw = ParseRawNames(_txtNames.Text);
        Log.Info("mkdir", $"BuildFinalFolderNames: rawLines={raw.Count}");

        if (raw.Count == 0)
            return raw;

        var cleaned = raw.Select(SanitizeFolderName).Where(x => x.Length > 0).ToList();
        Log.Info("mkdir", $"BuildFinalFolderNames: sanitized={cleaned.Count}");

        if (!_chkNumber.Checked)
        {
            sw.Stop();
            Log.Info("mkdir", $"BuildFinalFolderNames: numbering OFF, ms={sw.ElapsedMilliseconds}");
            return cleaned;
        }

        var fmt = ToNumberFormat();
        Log.Info("mkdir", $"BuildFinalFolderNames: numbering ON, fmt={fmt}");

        if (fmt == AutoNumberFormat.None)
        {
            sw.Stop();
            Log.Info("mkdir", $"BuildFinalFolderNames: fmt=None, ms={sw.ElapsedMilliseconds}");
            return cleaned;
        }

        var start = (int)_nudStart.Value;
        var pad = (int)_nudPad.Value;

        var numbered = new List<string>(cleaned.Count);
        for (int i = 0; i < cleaned.Count; i++)
        {
            var n = start + i;
            var num = n.ToString().PadLeft(pad, '0');

            numbered.Add(fmt switch
            {
                AutoNumberFormat.PrefixDash => $"{num} - {cleaned[i]}",
                AutoNumberFormat.PrefixDot => $"{n}. {cleaned[i]}",
                AutoNumberFormat.SuffixSpace => $"{cleaned[i]} {num}",
                _ => cleaned[i]
            });
        }

        var final = numbered.Select(SanitizeFolderName).Where(x => x.Length > 0).ToList();

        sw.Stop();
        Log.Info("mkdir", $"BuildFinalFolderNames: final={final.Count}, ms={sw.ElapsedMilliseconds}");

        return final;
    }

    private AutoNumberFormat ToNumberFormat()
        => _cmbNumberFormat.SelectedIndex switch
        {
            1 => AutoNumberFormat.PrefixDash,
            2 => AutoNumberFormat.PrefixDot,
            3 => AutoNumberFormat.SuffixSpace,
            _ => AutoNumberFormat.None
        };

    private static List<string> ParseRawNames(string? raw)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return list;

        var lines = raw.Replace("\r\n", "\n").Split('\n');
        foreach (var line in lines)
        {
            var s = (line ?? "").Trim();
            if (s.Length == 0) continue;
            list.Add(s);
        }
        return list;
    }

    private static string SanitizeFolderName(string name)
    {
        var s = (name ?? "").Trim();
        if (s.Length == 0) return "";

        foreach (var ch in Path.GetInvalidFileNameChars())
            s = s.Replace(ch, '_');

        if (s == "." || s == "..") return "";
        return s;
    }

    private const int LVM_FIRST = 0x1000;
    private const int LVM_GETCOUNTPERPAGE = LVM_FIRST + 40;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private static int GetListViewCountPerPage(ListView lv)
    {
        if (!lv.IsHandleCreated) return 0;
        var r = SendMessage(lv.Handle, LVM_GETCOUNTPERPAGE, IntPtr.Zero, IntPtr.Zero);
        return r.ToInt32();
    }

    private sealed class NamesTextBox : Panel
    {
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
        private const int EM_LINESCROLL = 0x00B6;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private readonly TextBox _tb;
        private readonly VScrollBar _vs;
        private readonly TableLayoutPanel _layout;
        private bool _syncing;

        public NamesTextBox()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);

            BorderStyle = BorderStyle.None;
            BackColor = SystemColors.Window;
            Padding = new Padding(0);

            _layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(2)
            };
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));

            _tb = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                AcceptsReturn = true,
                WordWrap = false,
                ScrollBars = ScrollBars.None,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0),
                BackColor = SystemColors.Window
            };
            _tb.HandleCreated += (_, __) => UpdateScrollbar();

            _vs = new VScrollBar
            {
                Dock = DockStyle.Fill,
                Visible = false,
                Enabled = false
            };

            _layout.Controls.Add(_tb, 0, 0);
            _layout.Controls.Add(_vs, 1, 0);
            Controls.Add(_layout);

            _tb.TextChanged += (_, __) =>
            {
                base.OnTextChanged(EventArgs.Empty);
                UpdateScrollbar();
            };
            _tb.KeyDown += (_, __) => UpdateScrollbar();
            _tb.KeyUp += (_, __) => UpdateScrollbar();
            _tb.MouseWheel += (_, __) => UpdateScrollbar();
            _tb.MouseUp += (_, __) => UpdateScrollbar();
            _tb.Resize += (_, __) => UpdateScrollbar();

            HandleCreated += (_, __) => UpdateScrollbar();
            SizeChanged += (_, __) => UpdateScrollbar();

            _vs.Scroll += (_, __) =>
            {
                if (_syncing || !_tb.IsHandleCreated) return;

                _syncing = true;
                try
                {
                    int first = GetFirstVisibleLine();
                    int delta = _vs.Value - first;
                    if (delta != 0)
                        SendMessage(_tb.Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)delta);
                }
                finally
                {
                    _syncing = false;
                }

                UpdateScrollbar();
            };
        }

        // ✅ Nullability match met WinForms (voorkomt CS8765)
#pragma warning disable CS8764
        public override string? Text
        {
            get => _tb.Text;
            set
            {
                _tb.Text = value ?? string.Empty;
                UpdateScrollbar();
            }
        }
#pragma warning restore CS8764
        public bool ReadOnly
        {
            get => _tb.ReadOnly;
            set => _tb.ReadOnly = value;
        }

        public new event EventHandler? TextChanged
        {
            add => base.TextChanged += value;
            remove => base.TextChanged -= value;
        }

        private int GetFirstVisibleLine()
        {
            if (!_tb.IsHandleCreated) return 0;
            return (int)SendMessage(_tb.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
        }

        private int GetLastVisibleLine(int first)
        {
            if (_tb.ClientSize.Height < 4) return first;

            var pt = new Point(2, Math.Max(0, _tb.ClientSize.Height - 2));
            int idx = _tb.GetCharIndexFromPosition(pt);
            int line = _tb.GetLineFromCharIndex(idx);
            if (line < first) line = first;
            return line;
        }

        private void UpdateScrollbar()
        {
            if (_syncing || !_tb.IsHandleCreated) return;
            if (_tb.ClientSize.Height < 4 || _tb.ClientSize.Width < 4) return;

            int total = GetLogicalLineCount();
            int first = GetFirstVisibleLine();
            int last = GetLastVisibleLine(first);

            int visibleCount = Math.Max(1, (last - first) + 1);
            bool needScroll = total > visibleCount;

            _layout.SuspendLayout();

            _layout.ColumnStyles[1].SizeType = SizeType.Absolute;
            _layout.ColumnStyles[1].Width = needScroll ? SystemInformation.VerticalScrollBarWidth : 0;

            _vs.Visible = needScroll;
            _vs.Enabled = needScroll;

            _vs.BringToFront();
            _layout.ResumeLayout(true);
            _layout.PerformLayout();

            if (!needScroll)
            {
                _vs.Value = 0;
                _vs.Minimum = 0;
                _vs.Maximum = 0;
                return;
            }

            _vs.Minimum = 0;
            _vs.LargeChange = Math.Max(1, visibleCount);
            _vs.SmallChange = 1;

            int maxValue = Math.Max(0, total - visibleCount);
            _vs.Maximum = maxValue + _vs.LargeChange - 1;

            int newVal = Math.Max(0, Math.Min(maxValue, first));
            if (_vs.Value != newVal)
                _vs.Value = newVal;
        }

        private int GetLogicalLineCount()
        {
            var lines = _tb.Lines;
            int count = lines.Length;
            if (count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
                count--;
            return Math.Max(0, count);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var r = ClientRectangle;
            r.Width -= 1;
            r.Height -= 1;

            ControlPaint.DrawBorder(e.Graphics, r,
                SystemColors.WindowFrame, ButtonBorderStyle.Solid);
        }
    }

    private MkdirOptions BuildEngineOptions(string basePath, List<string> names)
    {
        return new MkdirOptions
        {
            BasePath = basePath,
            FolderNames = names,

            CopyFileToEachFolder = _chkCopy.Checked,
            SourceFilePath = _chkCopy.Checked ? _selectedFile : null,

            CopyNamingMode = _cmbCopyNaming.SelectedIndex == 1
                ? CopyNamingMode.RenameToFolderName
                : CopyNamingMode.KeepOriginalName,

            // Preserve current UX: UI previously used overwrite:true
            OverwriteExistingCopiedFiles = _chkCopy.Checked,
            SkipCopyIfTargetExists = false,

            // Keep current verbosity behavior
            VerboseLogging = VerboseProgressLogging
        };
    }

    protected override void UpdateUiRunningState(bool isRunning)
    {
        // hergebruik je bestaande methode
        UpdateUiState(isRunning);

    }

    protected override void OnProgress(ToolProgressInfo p)
    {
        // Prefer precomputed percent if tool supplies it; fallback to existing logic
        if (p.Percent is int pct)
        {
            _progress.Value = pct;
            _lblProgress.Text = p.Total > 0 ? $"{p.Done} / {p.Total}" : "0 / 0";
        }
        else
        {
            SetProgress(p.Done, p.Total);
        }

        if (VerboseProgressLogging)
            Log.Info("mkdir",
                $"Progress (UI): done={p.Done}, total={p.Total}, pct={(p.Percent?.ToString() ?? "-")}, phase='{p.Phase}', item='{p.CurrentItem}', msg='{p.Message}'");
    }


}
