using System;
using System.Windows.Forms;
using StruxureGuard.Core.Logging;
using StruxureGuard.Core.Tools.Infrastructure;
using StruxureGuard.Core.Tools.UrlDecoder;

namespace StruxureGuard.UI.Tools;

public sealed class UrlDecoderToolForm : ToolBaseForm
{
    private readonly TextBox _txtUrl;
    private readonly TextBox _txtDecoded;

    private readonly Button _btnDecode;
    private readonly Button _btnCancel;
    private readonly Button _btnCopy;
    private readonly Button _btnClose;

    private readonly CheckBox _chkLeadingSlash;
    private readonly CheckBox _chkAutoCopy;

    private readonly Label _lblStatus;

    // Live preview debounce (optional, but nice)
    private readonly System.Windows.Forms.Timer _previewTimer;
    private const int PreviewDebounceMs = 250;

    public UrlDecoderToolForm()
    {
        Text = "URL Fragment Decoder";
        Width = 780;
        Height = 260;
        StartPosition = FormStartPosition.CenterParent;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 6
        };

        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // url
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // options
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // buttons
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // decoded
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // filler
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); // status

        Controls.Add(root);

        root.Controls.Add(new Label { Text = "URL:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);

        _txtUrl = new TextBox { Dock = DockStyle.Fill };
        root.Controls.Add(_txtUrl, 1, 0);

        // Options row
        var optRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        _chkLeadingSlash = new CheckBox { Text = "Zet / ervoor", AutoSize = true, Checked = true };
        _chkAutoCopy = new CheckBox { Text = "Auto-copy", AutoSize = true };

        optRow.Controls.Add(_chkLeadingSlash);
        optRow.Controls.Add(_chkAutoCopy);

        root.Controls.Add(optRow, 1, 1);
        root.Controls.Add(new Label { Text = "", AutoSize = true }, 0, 1);

        // Buttons row
        var btnRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        _btnDecode = new Button { Text = "Decode", Width = 120 };
        _btnCancel = new Button { Text = "Cancel", Width = 120 };
        _btnCopy = new Button { Text = "Kopieer", Width = 120 };
        _btnClose = new Button { Text = "Sluiten", Width = 120 };

        _btnDecode.Click += async (_, __) => await DecodeAsync();
        _btnCancel.Click += (_, __) => CancelRun();
        _btnCopy.Click += (_, __) => CopyDecoded();
        _btnClose.Click += (_, __) => Close();

        btnRow.Controls.Add(_btnDecode);
        btnRow.Controls.Add(_btnCancel);
        btnRow.Controls.Add(_btnCopy);
        btnRow.Controls.Add(_btnClose);

        root.Controls.Add(btnRow, 1, 2);
        root.Controls.Add(new Label { Text = "", AutoSize = true }, 0, 2);

        root.Controls.Add(new Label { Text = "Decoded path:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);

        _txtDecoded = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
        root.Controls.Add(_txtDecoded, 1, 3);

        _lblStatus = new Label { Dock = DockStyle.Fill, Text = "Ready." };
        root.Controls.Add(_lblStatus, 0, 5);
        root.SetColumnSpan(_lblStatus, 2);

        UpdateUiRunningState(false);

        // Live preview debounce timer
        _previewTimer = new System.Windows.Forms.Timer { Interval = PreviewDebounceMs };
        _previewTimer.Tick += (_, __) =>
        {
            _previewTimer.Stop();
            UpdatePreview();
        };

        _txtUrl.TextChanged += (_, __) => SchedulePreview("url.changed");
        _chkLeadingSlash.CheckedChanged += (_, __) => SchedulePreview("leadingSlash.changed");

        Load += (_, __) =>
        {
            Log.Info("urldecoder", "UrlDecoder tool opened");
            UpdatePreview();
        };

        FormClosed += (_, __) =>
        {
            try { _previewTimer.Stop(); } catch { }
            _previewTimer.Dispose();
        };
    }

    protected override void UpdateUiRunningState(bool isRunning)
    {
        _btnDecode.Enabled = !isRunning;
        _btnCancel.Enabled = isRunning;

        _txtUrl.ReadOnly = isRunning;
        _chkLeadingSlash.Enabled = !isRunning;
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
        Log.Info("urldecoder", $"Preview scheduled ({reason})");
    }

    private void UpdatePreview()
    {
        if (IsRunning) return;

        var opt = new UrlDecoderOptions
        {
            Url = _txtUrl.Text ?? "",
            EnsureLeadingSlash = _chkLeadingSlash.Checked,
            UseFragmentOnly = true
        };

        var decoded = UrlDecoderEngine.Decode(opt);
        _txtDecoded.Text = decoded;

        _lblStatus.Text = decoded.Length == 0 ? "Preview: leeg." : $"Preview: len={decoded.Length}";
        Log.Info("urldecoder", $"Preview updated: len={decoded.Length}");
    }

    private async System.Threading.Tasks.Task DecodeAsync()
    {
        Log.Info("urldecoder", "CLICK Decode");

        var tool = new UrlDecoderTool();

        var parameters = ToolParameters.Empty
            .With(UrlDecoderParameterKeys.Url, _txtUrl.Text ?? "")
            .With(UrlDecoderParameterKeys.EnsureLeadingSlash, _chkLeadingSlash.Checked.ToString())
            .With(UrlDecoderParameterKeys.UseFragmentOnly, "true");

        var result = await RunToolAsync(
            tool: tool,
            parameters: parameters,
            toolLogTag: "urldecoder",
            onCompleted: r =>
            {
                if (r.Success && !r.Canceled)
                {
                    var decoded = r.TryGetOutput(UrlDecoderTool.OutputKeyDecodedPath) ?? "";
                    _txtDecoded.Text = decoded;
                    _lblStatus.Text = "Done.";

                    if (_chkAutoCopy.Checked)
                        TryCopy(decoded, auto: true);
                }
                return System.Threading.Tasks.Task.CompletedTask;
            },
            showWarningsOnSuccess: true,
            successMessageFactory: _ => "Decoded path is bijgewerkt.");

        if (!result.Success)
            _lblStatus.Text = "Failed/Blocked.";
    }

    private void CopyDecoded()
    {
        TryCopy(_txtDecoded.Text ?? "", auto: false);
    }

    private void TryCopy(string text, bool auto)
    {
        try
        {
            Clipboard.SetText(text ?? "");
            Log.Info("urldecoder", $"{(auto ? "AutoCopy" : "Copy")} ok len={(text ?? "").Length}");
            _lblStatus.Text = auto ? "Auto-copied." : "Copied.";
        }
        catch (Exception ex)
        {
            Log.Warn("urldecoder", $"{(auto ? "AutoCopy" : "Copy")} failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
            MessageBox.Show(this, "Kopiëren naar clipboard mislukt. Zie log (Alt+L).",
                "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
