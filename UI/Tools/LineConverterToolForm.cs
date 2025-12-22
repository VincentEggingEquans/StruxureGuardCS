using System;
using System.Windows.Forms;
using StruxureGuard.Core.Logging;
using StruxureGuard.Core.Tools.Infrastructure;
using StruxureGuard.Core.Tools.LineConverter;

namespace StruxureGuard.UI.Tools;

public sealed class LineConverterToolForm : ToolBaseForm
{
    private readonly TextBox _txtInput;
    private readonly TextBox _txtResult;

    private readonly Button _btnConvert;
    private readonly Button _btnCancel;
    private readonly Button _btnCopy;
    private readonly Button _btnClose;

    private readonly CheckBox _chkDedup;
    private readonly CheckBox _chkSort;
    private readonly CheckBox _chkOxford;
    private readonly CheckBox _chkAutoCopy;

    private readonly ComboBox _cmbConjunction;
    private readonly ComboBox _cmbPreset;

    private readonly Label _lblStatus;

    // Live preview debounce
    private readonly System.Windows.Forms.Timer _previewTimer;
    private bool _applyingPreset;
    private const int PreviewDebounceMs = 250;

    public LineConverterToolForm()
    {
        Text = "Lineconverter";
        Width = 1100;
        Height = 700;
        StartPosition = FormStartPosition.CenterParent;

        // Root layout
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 6
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));  // label
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));   // input
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));  // options
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));  // buttons
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));   // result
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));  // status
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "Plak hier je regels (één per regel):",
            Dock = DockStyle.Fill
        }, 0, 0);

        _txtInput = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill
        };
        root.Controls.Add(_txtInput, 0, 1);

        // --- Create option controls ---
        _cmbPreset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbPreset.Items.AddRange(new object[]
        {
            "Standaard (NL)",
            "Oxford comma",
            "Uniek (deduplicate)",
            "Uniek + Sort",
            "Custom"
        });
        _cmbPreset.SelectedIndex = 0;

        _chkDedup = new CheckBox { Text = "Deduplicate", AutoSize = true };
        _chkSort = new CheckBox { Text = "Sort", AutoSize = true };
        _chkOxford = new CheckBox { Text = "Oxford comma", AutoSize = true };
        _chkAutoCopy = new CheckBox { Text = "Auto-copy na omzetten", AutoSize = true };

        _cmbConjunction = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbConjunction.Items.AddRange(new object[] { "en", "of" });
        _cmbConjunction.SelectedIndex = 0;

        // --- Options row (TableLayoutPanel for vertical centering) ---
        var optRow = BuildOptionsRow();
        root.Controls.Add(optRow, 0, 2);

        // Buttons row
        var btnRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        _btnConvert = new Button { Text = "Omzetten", Width = 140 };
        _btnCancel = new Button { Text = "Cancel", Width = 120 };
        _btnCopy = new Button { Text = "Kopieer resultaat", Width = 160 };
        _btnClose = new Button { Text = "Sluiten", Width = 120 };

        _btnConvert.Click += async (_, __) => await ConvertAsync();
        _btnCancel.Click += (_, __) => CancelRun();
        _btnCopy.Click += (_, __) => CopyResult();
        _btnClose.Click += (_, __) => Close();

        btnRow.Controls.Add(_btnConvert);
        btnRow.Controls.Add(_btnCancel);
        btnRow.Controls.Add(_btnCopy);
        btnRow.Controls.Add(_btnClose);
        root.Controls.Add(btnRow, 0, 3);

        _txtResult = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill
        };
        root.Controls.Add(_txtResult, 0, 4);

        _lblStatus = new Label { Dock = DockStyle.Fill, Text = "Ready." };
        root.Controls.Add(_lblStatus, 0, 5);

        UpdateUiRunningState(false);

        // Debounce timer for live preview
        _previewTimer = new System.Windows.Forms.Timer { Interval = PreviewDebounceMs };
        _previewTimer.Tick += (_, __) =>
        {
            _previewTimer.Stop();
            UpdatePreview();
        };

        // Hook changes to schedule preview
        _txtInput.TextChanged += (_, __) => SchedulePreview("input.changed");

        _chkDedup.CheckedChanged += (_, __) => OnOptionChanged("dedup.changed");
        _chkSort.CheckedChanged += (_, __) => OnOptionChanged("sort.changed");
        _chkOxford.CheckedChanged += (_, __) => OnOptionChanged("oxford.changed");
        _chkAutoCopy.CheckedChanged += (_, __) => Log.Info("lineconv", $"AutoCopy changed -> {_chkAutoCopy.Checked}");

        _cmbConjunction.SelectedIndexChanged += (_, __) => OnOptionChanged("conjunction.changed");
        _cmbPreset.SelectedIndexChanged += (_, __) => ApplyPresetFromDropdown();

        Load += (_, __) =>
        {
            Log.Info("lineconv", "LineConverter tool opened");
            ApplyPresetFromDropdown(); // init option state
            UpdatePreview();           // immediate preview
        };

        FormClosed += (_, __) =>
        {
            try { _previewTimer.Stop(); } catch { }
            _previewTimer.Dispose();
        };
    }

    private TableLayoutPanel BuildOptionsRow()
    {
        var optRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 10,
            RowCount = 1
        };
        optRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        optRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // "Preset:"
        optRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240)); // preset
        optRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // dedup
        optRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // sort
        optRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // oxford
        optRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // "Conjunction:"
        optRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); // conjunction
        optRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // auto-copy
        optRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // filler
        optRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1));   // tiny spacer

        static Label MakeLabel(string text, Padding? pad = null) => new()
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Padding = pad ?? new Padding(0, 2, 0, 0)
        };

        // Vertical centering
        _cmbPreset.Anchor = AnchorStyles.Left;
        _cmbConjunction.Anchor = AnchorStyles.Left;
        _chkDedup.Anchor = AnchorStyles.Left;
        _chkSort.Anchor = AnchorStyles.Left;
        _chkOxford.Anchor = AnchorStyles.Left;
        _chkAutoCopy.Anchor = AnchorStyles.Left;

        _cmbPreset.Width = 240;
        _cmbConjunction.Width = 140;

        // Spacing
        var m = new Padding(10, 0, 0, 0);
        _chkDedup.Margin = m;
        _chkSort.Margin = m;
        _chkOxford.Margin = m;
        _chkAutoCopy.Margin = new Padding(14, 0, 0, 0);

        optRow.Controls.Add(MakeLabel("Preset:"), 0, 0);
        optRow.Controls.Add(_cmbPreset, 1, 0);
        optRow.Controls.Add(_chkDedup, 2, 0);
        optRow.Controls.Add(_chkSort, 3, 0);
        optRow.Controls.Add(_chkOxford, 4, 0);
        optRow.Controls.Add(MakeLabel("Conjunction:", new Padding(12, 2, 0, 0)), 5, 0);
        optRow.Controls.Add(_cmbConjunction, 6, 0);
        optRow.Controls.Add(_chkAutoCopy, 7, 0);

        return optRow;
    }

    protected override void UpdateUiRunningState(bool isRunning)
    {
        _btnConvert.Enabled = !isRunning;
        _btnCancel.Enabled = isRunning;

        _txtInput.ReadOnly = isRunning;
        _cmbPreset.Enabled = !isRunning;
        _chkDedup.Enabled = !isRunning;
        _chkSort.Enabled = !isRunning;
        _chkOxford.Enabled = !isRunning;
        _cmbConjunction.Enabled = !isRunning;
        _chkAutoCopy.Enabled = !isRunning;
    }

    protected override void OnProgress(ToolProgressInfo p)
    {
        _lblStatus.Text = $"{p.Phase} - {p.Percent}% - {p.Message}";
    }

    private void SchedulePreview(string reason)
    {
        if (IsRunning) return;

        _previewTimer.Stop();
        _previewTimer.Start();

        Log.Info("lineconv", $"Preview scheduled ({reason})");
    }

    private void OnOptionChanged(string reason)
    {
        if (_applyingPreset) return;

        // Manual change => preset becomes Custom
        if ((_cmbPreset.SelectedItem?.ToString() ?? "") != "Custom")
        {
            _applyingPreset = true;
            _cmbPreset.SelectedItem = "Custom";
            _applyingPreset = false;
        }

        SchedulePreview(reason);
    }

    private void ApplyPresetFromDropdown()
    {
        if (_applyingPreset) return;
        if (IsRunning) return;

        var preset = _cmbPreset.SelectedItem?.ToString() ?? "Standaard (NL)";
        _applyingPreset = true;

        switch (preset)
        {
            case "Standaard (NL)":
                _chkDedup.Checked = false;
                _chkSort.Checked = false;
                _chkOxford.Checked = false;
                _cmbConjunction.SelectedItem = "en";
                break;

            case "Oxford comma":
                _chkDedup.Checked = false;
                _chkSort.Checked = false;
                _chkOxford.Checked = true;
                _cmbConjunction.SelectedItem = "en";
                break;

            case "Uniek (deduplicate)":
                _chkDedup.Checked = true;
                _chkSort.Checked = false;
                _chkOxford.Checked = false;
                _cmbConjunction.SelectedItem = "en";
                break;

            case "Uniek + Sort":
                _chkDedup.Checked = true;
                _chkSort.Checked = true;
                _chkOxford.Checked = false;
                _cmbConjunction.SelectedItem = "en";
                break;

            case "Custom":
            default:
                // leave as-is
                break;
        }

        _applyingPreset = false;

        Log.Info("lineconv", $"Preset applied: '{preset}'");
        SchedulePreview("preset.changed");
    }

    private void UpdatePreview()
    {
        if (IsRunning) return;

        var input = _txtInput.Text ?? "";
        var opt = BuildOptionsFromUi(input);

        var (items, resultText) = LineConverterEngine.Execute(opt);

        _txtResult.Text = resultText;
        _lblStatus.Text = items.Count == 0 ? "Preview: leeg." : $"Preview: {items.Count} regels.";

        Log.Info("lineconv", $"Preview updated: items={items.Count} resultLen={resultText.Length}");
    }

    private async System.Threading.Tasks.Task ConvertAsync()
    {
        Log.Info("lineconv", "CLICK Convert");

        var tool = new LineConverterTool();

        var parameters = ToolParameters.Empty
            .With(LineConverterParameterKeys.InputText, _txtInput.Text ?? "")
            .With(LineConverterParameterKeys.Deduplicate, _chkDedup.Checked.ToString())
            .With(LineConverterParameterKeys.Sort, _chkSort.Checked.ToString())
            .With(LineConverterParameterKeys.OxfordComma, _chkOxford.Checked.ToString())
            .With(LineConverterParameterKeys.Conjunction, _cmbConjunction.SelectedItem?.ToString() ?? "en");

        var result = await RunToolAsync(
            tool: tool,
            parameters: parameters,
            toolLogTag: "lineconv",
            onCompleted: r =>
            {
                if (r.Success && !r.Canceled)
                {
                    var text = r.TryGetOutput(LineConverterTool.OutputKeyResultText) ?? "";
                    _txtResult.Text = text;
                    _lblStatus.Text = "Done.";

                    if (_chkAutoCopy.Checked)
                        TryCopy(text, auto: true);
                }
                return System.Threading.Tasks.Task.CompletedTask;
            },
            showWarningsOnSuccess: true,
            successMessageFactory: _ => "Resultaat is bijgewerkt.");

        if (!result.Success)
            _lblStatus.Text = "Failed/Blocked.";
    }

    private LineConverterOptions BuildOptionsFromUi(string inputText)
    {
        return new LineConverterOptions
        {
            InputText = inputText ?? "",
            Conjunction = _cmbConjunction.SelectedItem?.ToString() ?? "en",
            Deduplicate = _chkDedup.Checked,
            Sort = _chkSort.Checked,
            OxfordComma = _chkOxford.Checked
        };
    }

    private void CopyResult()
    {
        TryCopy(_txtResult.Text ?? "", auto: false);
    }

    private void TryCopy(string text, bool auto)
    {
        try
        {
            Clipboard.SetText(text ?? "");
            Log.Info("lineconv", $"{(auto ? "AutoCopy" : "Copy")} ok len={(text ?? "").Length}");
            _lblStatus.Text = auto ? "Auto-copied." : "Copied.";
        }
        catch (Exception ex)
        {
            Log.Warn("lineconv", $"{(auto ? "AutoCopy" : "Copy")} failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
            MessageBox.Show(this, "Kopiëren naar clipboard mislukt. Zie log (Alt+L).",
                "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
