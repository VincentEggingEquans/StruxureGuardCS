using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using StruxureGuard.Core.Logging;
using StruxureGuard.Core.Tools.Infrastructure;
using StruxureGuard.Core.Tools.ModbusGroupAdvisor;

namespace StruxureGuard.UI.Tools;

public sealed class ModbusGroupAdvisorToolForm : ToolBaseForm
{
    private readonly TextBox _txtInput;

    private readonly Button _btnAnalyse;
    private readonly Button _btnCancel;
    private readonly Button _btnExportXml;

    private readonly Label _lblStatus;

    private readonly DataGridView _gridPreview;
    private readonly ListView _lvAdvice;

    private ModbusAnalysisResultDto? _last;
    private string _lastXml = "";

    public ModbusGroupAdvisorToolForm()
    {
        Text = "Modbus Group Advisor";
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.CenterParent;

        // --- Create controls FIRST (so readonly assignment is valid) ---
        _txtInput = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Dock = DockStyle.Fill,
            Font = new System.Drawing.Font("Consolas", 10f),
            BorderStyle = BorderStyle.FixedSingle
        };

        _gridPreview = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BorderStyle = BorderStyle.FixedSingle
        };
        _gridPreview.Columns.Add("name", "Name");
        _gridPreview.Columns.Add("address", "Adres");
        _gridPreview.Columns.Add("length", "Lengte");
        _gridPreview.Columns.Add("fc", "FC");
        _gridPreview.Columns.Add("type", "Type");
        _gridPreview.Columns.Add("raw", "Register type (raw)");

        _lvAdvice = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HideSelection = false,
            BorderStyle = BorderStyle.FixedSingle
        };
        _lvAdvice.Columns.Add("Item", 420, HorizontalAlignment.Left);
        _lvAdvice.Columns.Add("FC", 50, HorizontalAlignment.Center);
        _lvAdvice.Columns.Add("Type", 80, HorizontalAlignment.Center);
        _lvAdvice.Columns.Add("Start", 70, HorizontalAlignment.Right);
        _lvAdvice.Columns.Add("End", 70, HorizontalAlignment.Right);
        _lvAdvice.Columns.Add("Regs/Bits", 90, HorizontalAlignment.Right);
        _lvAdvice.Columns.Add("#", 45, HorizontalAlignment.Right);
        _lvAdvice.Columns.Add("Gaps", 55, HorizontalAlignment.Center);
        _lvAdvice.Columns.Add("Opmerking", 320, HorizontalAlignment.Left);
        _lvAdvice.Columns.Add("Point adres", 85, HorizontalAlignment.Right);
        _lvAdvice.Columns.Add("Point lengte", 95, HorizontalAlignment.Right);

        _btnAnalyse = new Button { Text = "Analyseer", Width = 120, Anchor = AnchorStyles.Right };
        _btnCancel = new Button { Text = "Cancel", Width = 120, Anchor = AnchorStyles.Right };
        _btnExportXml = new Button { Text = "Export XML", Width = 120, Anchor = AnchorStyles.Right, Enabled = false };

        _lblStatus = new Label
        {
            Text = "Ready.",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Padding = new Padding(10, 4, 10, 4),
            BorderStyle = BorderStyle.FixedSingle
        };

        // --- Build UI ---
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var header = BuildHeaderBar();
        root.Controls.Add(header, 0, 0);

        var splitA = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 8
        };
        root.Controls.Add(splitA, 0, 1);

        var splitB = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 8
        };
        splitA.Panel2.Controls.Add(splitB);

        // Input card
        splitA.Panel1.Controls.Add(BuildCard(
            title: "Input (plak hier je EBO/Excel export):",
            rightButtons: new[]
            {
                MakeButton("Plak", 90, (_, __) => PasteFromClipboard()),
                MakeButton("Clear", 90, (_, __) =>
                {
                    _txtInput.Clear();
                    Log.Info("modbus", "CLICK Clear input");
                })
            },
            content: _txtInput));

        // Preview card
        splitB.Panel1.Controls.Add(BuildCard(
            title: "Preview (wat er uit de input wordt ingelezen):",
            rightButtons: new[]
            {
                MakeButton("Copy row", 110, (_, __) => CopySelectedPreviewRow())
            },
            content: _gridPreview));

        // Advice card
        splitB.Panel2.Controls.Add(BuildCard(
            title: "Advies (groepen + onderliggende registers):",
            rightButtons: new[]
            {
                MakeButton("Copy selected", 130, (_, __) => CopySelectedAdviceRow())
            },
            content: _lvAdvice));

        // Events
        _btnAnalyse.Click += async (_, __) => await AnalyseAsync();
        _btnCancel.Click += (_, __) => CancelRun();
        _btnExportXml.Click += (_, __) => ExportXml();

        Shown += (_, __) =>
        {
            try
            {
                splitA.SplitterDistance = (int)(Height * 0.48);
                splitB.SplitterDistance = (int)(Height * 0.42);
            }
            catch { }

            Log.Info("modbus", "ModbusGroupAdvisor tool opened");
        };

        UpdateUiRunningState(false);
    }

    private Control BuildHeaderBar()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(12, 12, 12, 8),
            ColumnCount = 6
        };

        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // title
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // spacer
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // analyse
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // cancel
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // export
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // status

        var lblTitle = new Label
        {
            Text = "Modbus Group Advisor",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont, System.Drawing.FontStyle.Bold)
        };
        header.Controls.Add(lblTitle, 0, 0);

        header.Controls.Add(_btnAnalyse, 2, 0);
        header.Controls.Add(_btnCancel, 3, 0);
        header.Controls.Add(_btnExportXml, 4, 0);
        header.Controls.Add(_lblStatus, 5, 0);

        return header;
    }

    private static Control BuildCard(string title, Control[] rightButtons, Control content)
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 8, 12, 12)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(layout);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(0, 0, 0, 6)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        header.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Anchor = AnchorStyles.Left
        }, 0, 0);

        var btnHost = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.Right
        };

        foreach (var b in rightButtons)
        {
            b.Margin = new Padding(6, 0, 0, 0);
            btnHost.Controls.Add(b);
        }

        header.Controls.Add(btnHost, 1, 0);

        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(content, 0, 1);

        return outer;
    }

    private static Button MakeButton(string text, int width, EventHandler onClick)
    {
        var b = new Button { Text = text, Width = width };
        b.Click += onClick;
        return b;
    }

    protected override void UpdateUiRunningState(bool isRunning)
    {
        _btnAnalyse.Enabled = !isRunning;
        _btnCancel.Enabled = isRunning;

        _txtInput.ReadOnly = isRunning;

        _btnExportXml.Enabled = !isRunning && _last is not null && _last.Groups.Count > 0;
    }

    protected override void OnProgress(ToolProgressInfo p)
    {
        _lblStatus.Text = $"{p.Phase}: {p.Message}";
    }

    private void PasteFromClipboard()
    {
        try
        {
            var txt = Clipboard.GetText();
            if (!string.IsNullOrEmpty(txt))
            {
                _txtInput.SelectedText = txt;
                Log.Info("modbus", $"Paste from clipboard len={txt.Length}");
            }
        }
        catch (Exception ex)
        {
            Log.Warn("modbus", $"Clipboard paste failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
            MessageBox.Show(this, "Plakken uit klembord mislukt. Zie log (Alt+L).", "Clipboard",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async System.Threading.Tasks.Task AnalyseAsync()
    {
        Log.Info("modbus", "CLICK Analyseer");

        _btnExportXml.Enabled = false;
        _last = null;
        _lastXml = "";

        ClearPreview();
        ClearAdvice();

        var tool = new ModbusGroupAdvisorTool();

        var parameters = ToolParameters.Empty
            .With(ModbusGroupAdvisorParameterKeys.RawText, _txtInput.Text ?? "");

        var result = await RunToolAsync(
            tool: tool,
            parameters: parameters,
            toolLogTag: "modbus",
            onCompleted: r =>
            {
                if (r.Success && !r.Canceled)
                {
                    var json = r.TryGetOutput(ModbusGroupAdvisorTool.OutputKeyAnalysisJson) ?? "";
                    _lastXml = r.TryGetOutput(ModbusGroupAdvisorTool.OutputKeyEboXml) ?? "";

                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        _last = JsonSerializer.Deserialize<ModbusAnalysisResultDto>(json);
                        if (_last != null)
                        {
                            PopulatePreview(_last.PreviewRows);
                            PopulateAdvice(_last.Groups);
                        }
                    }

                    _btnExportXml.Enabled = _last is not null && _last.Groups.Count > 0;
                    _lblStatus.Text = $"Done. Groups={_last?.Groups.Count ?? 0} Preview={_last?.PreviewRows.Count ?? 0}";

                    if (r.Warnings.Count > 0)
                    {
                        var msg = string.Join(Environment.NewLine, r.Warnings);
                        MessageBox.Show(this, msg, "Let op", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }

                return System.Threading.Tasks.Task.CompletedTask;
            },
            showWarningsOnSuccess: false,
            successMessageFactory: _ => "Analyse klaar.");

        if (!result.Success)
            _lblStatus.Text = "Failed/Blocked.";
    }

    private void ExportXml()
    {
        if (_last is null || _last.Groups.Count == 0)
        {
            MessageBox.Show(this, "Er zijn nog geen registergroepen om te exporteren.", "Geen groepen",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_lastXml))
        {
            MessageBox.Show(this, "XML is leeg. Analyseer opnieuw.", "Geen XML",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Title = "Opslaan als",
            DefaultExt = "xml",
            FileName = "modbus_register_groups.xml",
            Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*"
        };

        if (sfd.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            System.IO.File.WriteAllText(sfd.FileName, _lastXml, System.Text.Encoding.UTF8);
            Log.Info("modbus", $"Export XML OK: '{sfd.FileName}' len={_lastXml.Length}");
            MessageBox.Show(this, $"XML opgeslagen:\n{sfd.FileName}", "Export geslaagd",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log.Error("modbus", $"Export failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
            MessageBox.Show(this, ex.Message, "Export mislukt", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearPreview()
    {
        _gridPreview.Rows.Clear();
    }

    private void ClearAdvice()
    {
        _lvAdvice.BeginUpdate();
        _lvAdvice.Items.Clear();
        _lvAdvice.EndUpdate();
    }

    private void PopulatePreview(List<ModbusPreviewRowDto> rows)
    {
        _gridPreview.SuspendLayout();
        _gridPreview.Rows.Clear();

        foreach (var r in rows)
            _gridPreview.Rows.Add(r.Name, r.Address, r.Length, r.FunctionCode, r.RegType, r.RawType);

        _gridPreview.ResumeLayout();
        Log.Info("modbus", $"Preview populated rows={rows.Count}");
    }

    private void PopulateAdvice(List<ModbusGroupDto> groups)
    {
        _lvAdvice.BeginUpdate();
        _lvAdvice.Items.Clear();

        foreach (var g in groups)
        {
            var unitLabel = g.RegType == "register" ? $"{g.TotalUnits} regs" : $"{g.TotalUnits} bits";
            var gapsLabel = g.HasGaps ? "Ja" : "Nee";
            var comment = g.HasGaps
                ? "Bevat lege adressen binnen range – controleer device."
                : "Compacte groep.";

            var liGroup = new ListViewItem($"GROUP {g.GroupId}");
            liGroup.SubItems.Add($"FC{g.FunctionCode}");
            liGroup.SubItems.Add(g.RegType);
            liGroup.SubItems.Add(g.StartAddress.ToString());
            liGroup.SubItems.Add(g.EndAddress.ToString());
            liGroup.SubItems.Add(unitLabel);
            liGroup.SubItems.Add(g.NumPoints.ToString());
            liGroup.SubItems.Add(gapsLabel);
            liGroup.SubItems.Add(comment);
            liGroup.SubItems.Add("");
            liGroup.SubItems.Add("");

            liGroup.BackColor = System.Drawing.Color.FromArgb(242, 242, 242);
            liGroup.Font = new System.Drawing.Font(_lvAdvice.Font, System.Drawing.FontStyle.Bold);

            _lvAdvice.Items.Add(liGroup);

            foreach (var e in g.Entries.OrderBy(x => x.Address))
            {
                var liPoint = new ListViewItem($"   ↳ {e.Name}");
                liPoint.SubItems.Add($"FC{e.FunctionCode}");
                liPoint.SubItems.Add(e.RegType);
                liPoint.SubItems.Add("");
                liPoint.SubItems.Add("");
                liPoint.SubItems.Add("");
                liPoint.SubItems.Add("");
                liPoint.SubItems.Add("");
                liPoint.SubItems.Add("");
                liPoint.SubItems.Add(e.Address.ToString());
                liPoint.SubItems.Add(e.Length.ToString());

                _lvAdvice.Items.Add(liPoint);
            }
        }

        _lvAdvice.EndUpdate();
        Log.Info("modbus", $"Advice populated groups={groups.Count} items={_lvAdvice.Items.Count}");

        AutoSizeAdviceColumns();
    }

    private void AutoSizeAdviceColumns()
    {
        try
        {
            for (int i = 0; i < _lvAdvice.Columns.Count; i++)
                _lvAdvice.Columns[i].Width = -2;

            _lvAdvice.Columns[0].Width = Math.Max(_lvAdvice.Columns[0].Width, 320);
            _lvAdvice.Columns[8].Width = Math.Max(_lvAdvice.Columns[8].Width, 260);
        }
        catch (Exception ex)
        {
            Log.Warn("modbus", $"AutoSizeAdviceColumns failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void CopySelectedPreviewRow()
    {
        try
        {
            if (_gridPreview.SelectedRows.Count == 0) return;
            var r = _gridPreview.SelectedRows[0];
            var vals = new[]
            {
                r.Cells[0].Value?.ToString() ?? "",
                r.Cells[1].Value?.ToString() ?? "",
                r.Cells[2].Value?.ToString() ?? "",
                r.Cells[3].Value?.ToString() ?? "",
                r.Cells[4].Value?.ToString() ?? "",
                r.Cells[5].Value?.ToString() ?? ""
            };
            var text = string.Join("\t", vals);
            Clipboard.SetText(text);
            Log.Info("modbus", $"Copy preview row ok len={text.Length}");
            _lblStatus.Text = "Copied preview row.";
        }
        catch (Exception ex)
        {
            Log.Warn("modbus", $"Copy preview row failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
        }
    }

    private void CopySelectedAdviceRow()
    {
        try
        {
            if (_lvAdvice.SelectedItems.Count == 0) return;
            var it = _lvAdvice.SelectedItems[0];

            var parts = new List<string> { it.Text };
            for (int i = 1; i < it.SubItems.Count; i++)
                parts.Add(it.SubItems[i].Text);

            var text = string.Join("\t", parts);
            Clipboard.SetText(text);
            Log.Info("modbus", $"Copy advice row ok len={text.Length}");
            _lblStatus.Text = "Copied advice row.";
        }
        catch (Exception ex)
        {
            Log.Warn("modbus", $"Copy advice row failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
        }
    }
}
