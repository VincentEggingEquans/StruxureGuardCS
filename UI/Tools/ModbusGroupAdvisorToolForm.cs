using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using StruxureGuard.Core.Logging;
using StruxureGuard.Core.Tools.Infrastructure;
using StruxureGuard.Core.Tools.ModbusGroupAdvisor;

namespace StruxureGuard.UI.Tools;

public sealed class ModbusGroupAdvisorToolForm : ToolBaseForm
{
    // Input is now a real grid (true columns)
    private readonly DataGridView _gridInput;

    private readonly Button _btnAnalyse;
    private readonly Button _btnExportXml;

    private readonly ListView _lvPreview;
    private readonly ListView _lvAdvice;

    private readonly ImageList _adviceImages;
    private const string AdviceIconGroupKey = "group";
    private const string AdviceRowKind_Remark = "remark";

    private ModbusAnalysisResultDto? _last;
    private string _lastXml = "";

    private readonly SplitContainer _splitMain;
    private readonly SplitContainer _splitLeft;
    private bool _initialSplitApplied;

    public ModbusGroupAdvisorToolForm()
    {
        Text = "Modbus Group Advisor";
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.CenterParent;

        // ---------- Input Grid ----------
        _gridInput = new DataGridView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackgroundColor = SystemColors.Window,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            MultiSelect = true,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText,
            AutoGenerateColumns = false,
            Font = new Font("Segoe UI", 9f)
        };

        _gridInput.ColumnHeadersDefaultCellStyle.Font = new Font(_gridInput.Font, FontStyle.Bold);

        // Ctrl+V = paste-replace
        _gridInput.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.V)
            {
                e.SuppressKeyPress = true;
                PasteReplaceGridFromClipboard();
            }
        };

        // ---------- Preview ----------
        _lvPreview = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            BorderStyle = BorderStyle.FixedSingle
        };

        _lvPreview.Columns.Add("Name", 340, HorizontalAlignment.Left);
        _lvPreview.Columns.Add("Adres", 80, HorizontalAlignment.Right);
        _lvPreview.Columns.Add("Len", 55, HorizontalAlignment.Right);
        _lvPreview.Columns.Add("FC", 45, HorizontalAlignment.Center);
        _lvPreview.Columns.Add("Type", 80, HorizontalAlignment.Left);
        _lvPreview.Columns.Add("Raw type", 240, HorizontalAlignment.Left);
        _lvPreview.Columns.Add("Status", 260, HorizontalAlignment.Left);

        // ---------- Advice ----------
        _lvAdvice = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            BorderStyle = BorderStyle.FixedSingle,
            OwnerDraw = true
        };

        _lvAdvice.DrawColumnHeader += (_, e) => e.DrawDefault = true;

        _lvAdvice.DrawItem += (_, e) =>
        {
            var item = e.Item;
            if (item is null) { e.DrawDefault = true; return; }

            if (item.Tag is string s && s == AdviceRowKind_Remark)
            {
                e.DrawBackground();
                if (item.Selected) e.DrawFocusRectangle();
                return;
            }

            e.DrawDefault = true;
        };

        _lvAdvice.DrawSubItem += (_, e) =>
        {
            var item = e.Item;
            if (item is null) { e.DrawDefault = true; return; }

            if (item.Tag is string s && s == AdviceRowKind_Remark)
            {
                DrawRemarkRowSubItem(e);
                return;
            }

            e.DrawDefault = true;
        };

        _adviceImages = new ImageList
        {
            ImageSize = new Size(16, 16),
            ColorDepth = ColorDepth.Depth32Bit
        };

        // Koppel de imagelist aan de ListView (dan is SmallImageList nooit null)
        _lvAdvice.SmallImageList = _adviceImages;

        // Laad icoon (non-fatal, logt zelf)
        TryLoadAdviceIcon();


        _lvAdvice.Columns.Add("Item", 360, HorizontalAlignment.Left);
        _lvAdvice.Columns.Add("FC", 40, HorizontalAlignment.Center);
        _lvAdvice.Columns.Add("Type", 70, HorizontalAlignment.Left);
        _lvAdvice.Columns.Add("Start", 60, HorizontalAlignment.Right);
        _lvAdvice.Columns.Add("End", 60, HorizontalAlignment.Right);
        _lvAdvice.Columns.Add("Regs/Bits", 80, HorizontalAlignment.Left);
        _lvAdvice.Columns.Add("#", 40, HorizontalAlignment.Right);
        _lvAdvice.Columns.Add("Gaps", 60, HorizontalAlignment.Left);
        _lvAdvice.Columns.Add("Point lengte", 110, HorizontalAlignment.Right);

        // ---------- Buttons ----------
        _btnAnalyse = MakeButton("Analyseer", 95, async (_, __) => await AnalyseAsync());
        _btnExportXml = MakeButton("Export XML", 95, (_, __) => ExportXml());
        _btnExportXml.Enabled = false;

        // ---------- Cards ----------
        var inputCard = BuildCard("Input (Ctrl+V = replace)", _gridInput);
        var previewCard = BuildCard("Preview", _lvPreview);
        var adviceCard = BuildCard("Advice", _lvAdvice);

        // ---------- SplitContainers ----------
        _splitLeft = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            Panel1MinSize = 0,
            Panel2MinSize = 0,
            SplitterWidth = 6
        };
        _splitLeft.Panel1.Controls.Add(inputCard);
        _splitLeft.Panel2.Controls.Add(previewCard);

        _splitMain = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            Panel1MinSize = 0,
            Panel2MinSize = 0,
            SplitterWidth = 6
        };
        _splitMain.Panel1.Controls.Add(_splitLeft);
        _splitMain.Panel2.Controls.Add(adviceCard);

        var topBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(4)
        };
        topBar.Controls.Add(_btnAnalyse);
        topBar.Controls.Add(_btnExportXml);

        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        root.Controls.Add(_splitMain);
        root.Controls.Add(topBar);
        Controls.Add(root);

        Shown += (_, __) => BeginInvoke((Action)ApplyInitialSplitLayoutSafe);
    }

    protected override void UpdateUiRunningState(bool isRunning)
    {
        _btnAnalyse.Enabled = !isRunning;
        _gridInput.Enabled = !isRunning;
        _btnExportXml.Enabled = !isRunning && _last is not null && _last.Groups.Count > 0;
    }

    protected override void OnProgress(ToolProgressInfo p)
    {
        // no UI progress header (requested)
    }

    private void ApplyInitialSplitLayoutSafe()
    {
        if (_initialSplitApplied) return;
        _initialSplitApplied = true;

        try
        {
            _splitMain.Panel1MinSize = 260;
            _splitMain.Panel2MinSize = 420;
            _splitLeft.Panel1MinSize = 240;
            _splitLeft.Panel2MinSize = 220;

            var desiredMain = (int)(ClientSize.Width * 0.52);
            var desiredLeft = (int)(ClientSize.Height * 0.48);

            SafeSetSplitterDistance(_splitMain, desiredMain);
            SafeSetSplitterDistance(_splitLeft, desiredLeft);

            Log.Info("modbus",
                $"Split init ok: clientW={ClientSize.Width} clientH={ClientSize.Height} " +
                $"splitMain={_splitMain.SplitterDistance} splitLeft={_splitLeft.SplitterDistance}");
        }
        catch (Exception ex)
        {
            Log.Warn("modbus", $"Split init failed (non-fatal): {ex.GetType().Name}: {ex.Message}\n{ex}");
        }
    }

    private static void SafeSetSplitterDistance(SplitContainer sc, int desired)
    {
        var total = sc.Orientation == Orientation.Vertical ? sc.Width : sc.Height;
        if (total <= 0) return;

        var min = sc.Panel1MinSize;
        var max = total - sc.Panel2MinSize - sc.SplitterWidth;
        if (max < min) { min = 0; max = Math.Max(0, total - sc.SplitterWidth); }

        var clamped = Math.Max(min, Math.Min(max, desired));
        if (sc.SplitterDistance != clamped) sc.SplitterDistance = clamped;
    }

    // ---------------- Input grid paste / parse ----------------

    private void PasteReplaceGridFromClipboard()
    {
        try
        {
            var txt = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(txt))
            {
                Log.Info("modbus", "PasteReplaceGrid: clipboard empty");
                return;
            }

            txt = txt.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = txt.Split('\n');

            // remove trailing empty lines
            while (lines.Length > 0 && string.IsNullOrWhiteSpace(lines[^1]))
                lines = lines.Take(lines.Length - 1).ToArray();

            if (lines.Length == 0)
                return;

            var rows = new List<string[]>();
            int maxCols = 0;

            foreach (var line in lines)
            {
                var cols = line.Split('\t');
                rows.Add(cols);
                if (cols.Length > maxCols) maxCols = cols.Length;
            }

            if (maxCols <= 0)
                return;

            // First row is header
            var header = rows[0];
            BuildGridColumns(header, maxCols);

            _gridInput.Rows.Clear();

            // Data rows start at 1
            for (int i = 1; i < rows.Count; i++)
            {
                var cols = rows[i];
                var arr = new object[maxCols];
                for (int c = 0; c < maxCols; c++)
                    arr[c] = c < cols.Length ? cols[c] : "";

                _gridInput.Rows.Add(arr);
            }

            AutoSizeGridColumns(sampleRows: Math.Min(80, _gridInput.Rows.Count));
            Log.Info("modbus", $"PasteReplaceGrid ok rows={_gridInput.Rows.Count} cols={_gridInput.Columns.Count} rawLen={txt.Length}");
        }
        catch (Exception ex)
        {
            Log.Warn("modbus", $"PasteReplaceGrid failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
            MessageBox.Show(this, "Plakken naar grid mislukt. Zie log (Alt+L).", "Clipboard",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void BuildGridColumns(string[] header, int maxCols)
    {
        _gridInput.SuspendLayout();

        _gridInput.Columns.Clear();

        for (int c = 0; c < maxCols; c++)
        {
            var name = c < header.Length && !string.IsNullOrWhiteSpace(header[c]) ? header[c] : $"Col {c + 1}";
            var col = new DataGridViewTextBoxColumn
            {
                HeaderText = name,
                Name = $"col_{c}",
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                MinimumWidth = 80
            };
            _gridInput.Columns.Add(col);
        }

        _gridInput.ResumeLayout();
    }

    private void AutoSizeGridColumns(int sampleRows)
    {
        try
        {
            if (_gridInput.Columns.Count == 0) return;

            using var g = _gridInput.CreateGraphics();

            for (int c = 0; c < _gridInput.Columns.Count; c++)
            {
                var col = _gridInput.Columns[c];

                int w = TextRenderer.MeasureText(col.HeaderText, _gridInput.ColumnHeadersDefaultCellStyle.Font ?? _gridInput.Font).Width + 30;

                for (int r = 0; r < sampleRows; r++)
                {
                    var v = _gridInput.Rows[r].Cells[c].Value?.ToString() ?? "";
                    if (v.Length > 120) v = v.Substring(0, 120);
                    var vw = TextRenderer.MeasureText(v, _gridInput.Font).Width + 26;
                    if (vw > w) w = vw;
                    if (w > 520) { w = 520; break; }
                }

                if (w < 90) w = 90;
                if (w > 520) w = 520;

                col.Width = w;
            }
        }
        catch (Exception ex)
        {
            Log.Warn("modbus", $"AutoSizeGridColumns failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
        }
    }

    private string BuildRawFromGrid()
    {
        if (_gridInput.Columns.Count == 0)
            return "";

        var sb = new StringBuilder();

        // header
        sb.Append(string.Join("\t", _gridInput.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText)));
        sb.AppendLine();

        // rows
        foreach (DataGridViewRow row in _gridInput.Rows)
        {
            if (row.IsNewRow) continue;

            var cells = new string[_gridInput.Columns.Count];
            for (int i = 0; i < cells.Length; i++)
                cells[i] = row.Cells[i].Value?.ToString() ?? "";

            // trim trailing empty columns to keep text nicer
            int end = cells.Length;
            while (end > 1 && string.IsNullOrWhiteSpace(cells[end - 1]))
                end--;

            sb.Append(string.Join("\t", cells.Take(end)));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ---------------- Icon loading ----------------

    private void TryLoadAdviceIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "group_icon.png");
            if (!File.Exists(iconPath))
            {
                Log.Warn("modbus", $"Advice icon missing: '{iconPath}'");
                return;
            }

            using var bmp = new Bitmap(iconPath);

            _adviceImages.Images.RemoveByKey(AdviceIconGroupKey);
            _adviceImages.Images.Add(AdviceIconGroupKey, new Bitmap(bmp));

            Log.Info("modbus", $"Advice icon loaded: '{iconPath}' size={bmp.Width}x{bmp.Height}");
        }
        catch (Exception ex)
        {
            Log.Warn("modbus", $"Advice icon load failed (non-fatal): {ex.GetType().Name}: {ex.Message}\n{ex}");
        }
    }

    // ---------------- Run / Export ----------------

    private async System.Threading.Tasks.Task AnalyseAsync()
    {
        Log.Info("modbus", "CLICK Analyseer");

        _btnExportXml.Enabled = false;
        _last = null;
        _lastXml = "";

        ClearPreview();
        ClearAdvice();

        var raw = BuildRawFromGrid();

        var tool = new ModbusGroupAdvisorTool();
        var parameters = ToolParameters.Empty
            .With(ModbusGroupAdvisorParameterKeys.RawText, raw);

        _ = await RunToolAsync(
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
                            PopulatePreview(_last.PreviewRows, _last.RejectedRows);
                            PopulateAdviceLikePython(_last.Groups);
                            Log.Info("modbus", $"UI populated: previewOk={_last.PreviewRows.Count} rejected={_last.RejectedRows.Count} groups={_last.Groups.Count}");
                        }
                    }

                    _btnExportXml.Enabled = _last is not null && _last.Groups.Count > 0;

                    if (r.Warnings.Count > 0)
                    {
                        var msg = string.Join(Environment.NewLine, r.Warnings);
                        MessageBox.Show(this, msg, "Let op", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }

                return System.Threading.Tasks.Task.CompletedTask;
            },
            showWarningsOnSuccess: false,
            successMessageFactory: _ => ""
        );
    }

    private void ExportXml()
    {
        Log.Info("modbus", "CLICK Export XML");

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
        {
            Log.Info("modbus", "Export canceled by user");
            return;
        }

        try
        {
            File.WriteAllText(sfd.FileName, _lastXml, Encoding.UTF8);
            Log.Info("modbus", $"Export XML OK: '{sfd.FileName}' len={_lastXml.Length}");
            MessageBox.Show(this, $"XML opgeslagen:\n{sfd.FileName}", "Export geslaagd", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log.Error("modbus", $"Export failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
            MessageBox.Show(this, ex.Message, "Export mislukt", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ---------------- Preview / Advice populate ----------------

    private void ClearPreview()
    {
        _lvPreview.BeginUpdate();
        _lvPreview.Items.Clear();
        _lvPreview.EndUpdate();
    }

    private void ClearAdvice()
    {
        _lvAdvice.BeginUpdate();
        _lvAdvice.Items.Clear();
        _lvAdvice.EndUpdate();
    }

    private void PopulatePreview(List<ModbusPreviewRowDto> rows, List<ModbusRejectedRowDto> rejected)
    {
        _lvPreview.BeginUpdate();
        _lvPreview.Items.Clear();

        foreach (var r in rows)
        {
            var li = new ListViewItem(r.Name);
            li.SubItems.Add(r.Address.ToString());
            li.SubItems.Add(r.Length.ToString());
            li.SubItems.Add(r.FunctionCode.ToString());
            li.SubItems.Add(r.RegType);
            li.SubItems.Add(r.RawType ?? "");
            li.SubItems.Add("OK");
            _lvPreview.Items.Add(li);
        }

        foreach (var bad in rejected)
        {
            var name = bad.Name ?? $"Row {bad.RowNumber}";
            var li = new ListViewItem(name);

            li.SubItems.Add("");
            li.SubItems.Add("");
            li.SubItems.Add("");
            li.SubItems.Add("");
            li.SubItems.Add("");
            li.SubItems.Add($"ERROR: {bad.Reason}");

            li.ForeColor = Color.Red;
            li.ToolTipText = bad.RawLine;

            _lvPreview.Items.Add(li);
        }

        _lvPreview.EndUpdate();
        Log.Info("modbus", $"Preview populated: ok={rows.Count} rejected={rejected.Count}");
    }

    private void PopulateAdviceLikePython(List<ModbusGroupDto> groups)
    {
        _lvAdvice.BeginUpdate();
        _lvAdvice.Items.Clear();

        foreach (var g in groups.OrderBy(x => x.GroupId))
        {
            var isCoil = string.Equals(g.RegType, "coil", StringComparison.OrdinalIgnoreCase);
            var regsBits = isCoil ? $"{g.TotalUnits} bits" : $"{g.TotalUnits} regs";
            var gaps = g.HasGaps ? "Ja" : "Nee";

            var opmerking = g.HasGaps
                ? "Bevat lege adressen binnen range – controleer dev"
                : "Compacte groep.";

            var groupTitle = $"GROUP {g.GroupId}  |  FC{g.FunctionCode}  {g.StartAddress}-{g.EndAddress}";

            var groupItem = new ListViewItem(groupTitle)
            {
                ImageKey = AdviceIconGroupKey,
                Font = new Font(_lvAdvice.Font, FontStyle.Bold),
                BackColor = SystemColors.ControlLight
            };

            groupItem.SubItems.Add(g.FunctionCode.ToString());
            groupItem.SubItems.Add(g.RegType);
            groupItem.SubItems.Add(g.StartAddress.ToString());
            groupItem.SubItems.Add(g.EndAddress.ToString());
            groupItem.SubItems.Add(regsBits);
            groupItem.SubItems.Add(g.NumPoints.ToString());
            groupItem.SubItems.Add(gaps);
            groupItem.SubItems.Add("");
            _lvAdvice.Items.Add(groupItem);

            if (!string.IsNullOrWhiteSpace(opmerking))
            {
                var noteItem = new ListViewItem($"Opmerking: {opmerking}")
                {
                    IndentCount = 1,
                    ForeColor = SystemColors.GrayText,
                    Tag = AdviceRowKind_Remark,
                    ToolTipText = opmerking
                };
                while (noteItem.SubItems.Count < _lvAdvice.Columns.Count)
                    noteItem.SubItems.Add("");
                _lvAdvice.Items.Add(noteItem);
            }

            foreach (var e in g.Entries.OrderBy(x => x.Address))
            {
                var start = e.Address;
                var end = e.Address + Math.Max(1, e.Length) - 1;

                var title = $"{e.Name} ({start})";
                var pointItem = new ListViewItem(title) { IndentCount = 1 };

                pointItem.SubItems.Add(e.FunctionCode.ToString());
                pointItem.SubItems.Add(e.RegType);
                pointItem.SubItems.Add(start.ToString());
                pointItem.SubItems.Add(end.ToString());
                pointItem.SubItems.Add("");
                pointItem.SubItems.Add("");
                pointItem.SubItems.Add("");
                pointItem.SubItems.Add(e.Length.ToString());

                _lvAdvice.Items.Add(pointItem);
            }
        }

        _lvAdvice.EndUpdate();
        Log.Info("modbus", $"Advice populated: groups={groups.Count} items={_lvAdvice.Items.Count}");
    }

    private void DrawRemarkRowSubItem(DrawListViewSubItemEventArgs e)
    {
        var item = e.Item;
        if (item is null) return;

        if (e.ColumnIndex != 0) return;

        var lv = item.ListView;
        if (lv is null) return;

        var rowRect = lv.GetItemRect(e.ItemIndex);
        var full = new Rectangle(rowRect.Left, rowRect.Top, lv.ClientSize.Width, rowRect.Height);

        using var bg = new SolidBrush(item.Selected ? SystemColors.Highlight : lv.BackColor);
        e.Graphics.FillRectangle(bg, full);

        var textColor = item.Selected ? SystemColors.HighlightText : item.ForeColor;

        var font = item.Font ?? lv.Font; // <-- fix: fallback
        TextRenderer.DrawText(
            e.Graphics,
            item.Text ?? "",
            font,
            Rectangle.Inflate(full, -6, 0),
            textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix
        );

        if (item.Selected)
            ControlPaint.DrawFocusRectangle(e.Graphics, full);
    }


    private static Control BuildCard(string title, Control content)
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(6, 4, 6, 6),
            BorderStyle = BorderStyle.FixedSingle
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new Label
        {
            Text = title,
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 4)
        };

        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(content, 0, 1);

        outer.Controls.Add(layout);
        return outer;
    }

    private static Button MakeButton(string text, int width, EventHandler onClick)
    {
        var b = new Button { Text = text, Width = width };
        b.Click += onClick;
        return b;
    }
}
