using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using StruxureGuard.Core.Logging;
using StruxureGuard.Core.Tools.AspPathChecker;
using StruxureGuard.Core.Tools.Infrastructure;

namespace StruxureGuard.UI.Tools;

public sealed class AspPathCheckerToolForm : ToolBaseForm
{
    private readonly DataGridView _gridAsp;
    private readonly DataGridView _gridPaths;

    private readonly ListView _lv;

    private readonly Button _btnBuild;
    private readonly Button _btnCheckSelected;
    private readonly Button _btnCheckAll;
    private readonly Label _lblStatus;

    private const int ColAsp = 0;
    private const int ColPath = 1;
    private const int ColStatus = 2;

    public AspPathCheckerToolForm()
    {
        Text = "ASP Path Checker";
        MinimumSize = new Size(1200, 720);

        Log.Info("asppath", "ui: form constructed");

        // ---------- MIDDLE (results fill) ----------
        var mid = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 6, 10, 6)
        };
        Controls.Add(mid);

        // ---------- TOP (inputs) ----------
        var top = new Panel
        {
            Dock = DockStyle.Top,
            Height = 280,
            Padding = new Padding(10, 10, 10, 6)
        };
        Controls.Add(top);

        // ---------- BOTTOM (buttons + status) ----------
        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            Padding = new Padding(10, 6, 10, 10)
        };
        Controls.Add(bottom);


        // Inputs layout (2 columns)
        var inputs = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3
        };
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        inputs.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // header line
        inputs.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // labels
        inputs.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // grids
        top.Controls.Add(inputs);

        // Compact header hint
        var hint = new Label
        {
            AutoSize = true,
            Text = "Ctrl+V plakt TSV en bouwt kolommen op basis van de eerste regel (header). Output: Ctrl+C kopieert geselecteerde regels.",
            Padding = new Padding(0, 0, 0, 6)
        };
        inputs.Controls.Add(hint, 0, 0);
        inputs.SetColumnSpan(hint, 2);

        inputs.Controls.Add(new Label { Text = "ASP input (plak TSV export: kolom 'Name'):", AutoSize = true }, 0, 1);
        inputs.Controls.Add(new Label { Text = "Path input (plak TSV export: kolom 'Path'):", AutoSize = true }, 1, 1);

        _gridAsp = CreateInputGrid();
        _gridPaths = CreateInputGrid();

        _gridAsp.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.V)
            {
                e.SuppressKeyPress = true;
                PasteReplaceGridFromClipboard(_gridAsp, gridTag: "asp", preferredHeaderKey: "Name");
            }
        };

        _gridPaths.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.V)
            {
                e.SuppressKeyPress = true;
                PasteReplaceGridFromClipboard(_gridPaths, gridTag: "path", preferredHeaderKey: "Path");
            }
        };

        inputs.Controls.Add(_gridAsp, 0, 2);
        inputs.Controls.Add(_gridPaths, 1, 2);

        // Results list
        _lv = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            BorderStyle = BorderStyle.FixedSingle
        };
        _lv.Columns.Add("ASP", 200, HorizontalAlignment.Left);
        _lv.Columns.Add("Path", 200, HorizontalAlignment.Left);
        _lv.Columns.Add("Status", 200, HorizontalAlignment.Left);

        _lv.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                e.SuppressKeyPress = true;
                CopySelectedRowsAsTsv();
            }
        };

        _lv.Resize += (_, _) => AutoFitOutputColumns();
        mid.Controls.Add(_lv);

        // Bottom content: buttons left, status right
        var btnRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight
        };
        bottom.Controls.Add(btnRow);

        _lblStatus = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = false,
            Text = ""
        };
        bottom.Controls.Add(_lblStatus);

        _btnBuild = new Button { Text = "Build list", Width = 90 };
        _btnBuild.Click += (_, _) => RunBuild();

        _btnCheckSelected = new Button { Text = "Check", Width = 90 };
        _btnCheckSelected.Click += (_, _) => RunCheckSelected();

        _btnCheckAll = new Button { Text = "Check all", Width = 90 };
        _btnCheckAll.Click += (_, _) => RunCheckAll();

        btnRow.Controls.Add(_btnBuild);
        btnRow.Controls.Add(_btnCheckSelected);
        btnRow.Controls.Add(_btnCheckAll);

        // Initial fit (prevents horizontal bar at startup)
        Shown += (_, _) => BeginInvoke(new Action(AutoFitOutputColumns));
    }

    protected override void UpdateUiRunningState(bool isRunning)
    {
        _btnBuild.Enabled = !isRunning;
        _btnCheckSelected.Enabled = !isRunning;
        _btnCheckAll.Enabled = !isRunning;

        _gridAsp.Enabled = !isRunning;
        _gridPaths.Enabled = !isRunning;
        _lv.Enabled = !isRunning;

        if (isRunning)
            _lblStatus.Text = "Bezig...";
    }

    // ---------------- Tool run ----------------

    private void RunBuild()
    {
        Log.Info("asppath", "ui: CLICK Build list");

        var tool = new AspPathCheckerTool();
        var aspRaw = BuildRawFromGrid(_gridAsp);
        var pathRaw = BuildRawFromGrid(_gridPaths);

        var parameters = ToolParameters.Empty
            .With(AspPathCheckerParameterKeys.Mode, "build")
            .With(AspPathCheckerParameterKeys.AspText, aspRaw)
            .With(AspPathCheckerParameterKeys.PathText, pathRaw);

        _ = RunToolAsync(
            tool: tool,
            parameters: parameters,
            toolLogTag: "asppath",
            onCompleted: result =>
            {
                ApplyResultToList(result);
                return System.Threading.Tasks.Task.CompletedTask;
            },
            showWarningsOnSuccess: true,
            successMessageFactory: _ => "");
    }

    private void RunCheckSelected()
    {
        var sel = _lv.SelectedIndices.Cast<int>().Distinct().OrderBy(i => i).ToList();

        Log.Info("asppath", $"ui: CLICK Check selected selectedCount={sel.Count}");

        if (sel.Count == 0)
        {
            MessageBox.Show(this, "Selecteer eerst 1 of meer regels.", "Check selected",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var tool = new AspPathCheckerTool();
        var aspRaw = BuildRawFromGrid(_gridAsp);
        var pathRaw = BuildRawFromGrid(_gridPaths);

        var parameters = ToolParameters.Empty
            .With(AspPathCheckerParameterKeys.Mode, "check")
            .With(AspPathCheckerParameterKeys.CheckAll, "false")
            .With(AspPathCheckerParameterKeys.SelectedIndices, string.Join(",", sel))
            .With(AspPathCheckerParameterKeys.AspText, aspRaw)
            .With(AspPathCheckerParameterKeys.PathText, pathRaw);

        _ = RunToolAsync(
            tool: tool,
            parameters: parameters,
            toolLogTag: "asppath",
            onCompleted: result =>
            {
                ApplyResultToList(result);
                return System.Threading.Tasks.Task.CompletedTask;
            },
            showWarningsOnSuccess: true,
            successMessageFactory: _ => "");
    }

    private void RunCheckAll()
    {
        Log.Info("asppath", "ui: CLICK Check all");

        var tool = new AspPathCheckerTool();
        var aspRaw = BuildRawFromGrid(_gridAsp);
        var pathRaw = BuildRawFromGrid(_gridPaths);

        var parameters = ToolParameters.Empty
            .With(AspPathCheckerParameterKeys.Mode, "check")
            .With(AspPathCheckerParameterKeys.CheckAll, "true")
            .With(AspPathCheckerParameterKeys.SelectedIndices, "")
            .With(AspPathCheckerParameterKeys.AspText, aspRaw)
            .With(AspPathCheckerParameterKeys.PathText, pathRaw);

        _ = RunToolAsync(
            tool: tool,
            parameters: parameters,
            toolLogTag: "asppath",
            onCompleted: result =>
            {
                ApplyResultToList(result);
                return System.Threading.Tasks.Task.CompletedTask;
            },
            showWarningsOnSuccess: true,
            successMessageFactory: _ => "");
    }

    // ---------------- Output apply ----------------

    private void ApplyResultToList(ToolResult result)
    {
        var json = result.TryGetOutput(AspPathCheckerTool.OutputKeyResultJson);
        if (string.IsNullOrWhiteSpace(json))
        {
            Log.Warn("asppath", "ui: No ResultJson output found.");
            _lblStatus.Text = result.Summary;
            return;
        }

        AspPathCheckerResultDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<AspPathCheckerResultDto>(json);
        }
        catch (Exception ex)
        {
            Log.Warn("asppath", $"ui: Deserialize ResultJson failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
            _lblStatus.Text = result.Summary;
            return;
        }

        if (dto == null)
        {
            Log.Warn("asppath", "ui: ResultJson deserialized to null.");
            _lblStatus.Text = result.Summary;
            return;
        }

        _lv.BeginUpdate();
        _lv.Items.Clear();

        foreach (var r in dto.Rows)
        {
            var item = new ListViewItem(r.AspName ?? "");
            item.SubItems.Add(r.Path ?? "");
            item.SubItems.Add(r.Status ?? "");

            ApplyItemStyle(item, r.Status ?? "");
            _lv.Items.Add(item);
        }

        _lv.EndUpdate();

        AutoFitOutputColumns();

        _lblStatus.Text =
            $"ASP={dto.AspCount} Paths={dto.PathCount} Matches={dto.MatchCount} | " +
            $"NoMatch={dto.AspWithoutMatchCount} | Checked={dto.CheckedCount} Missing={dto.MissingCount}";

        Log.Info("asppath", $"ui: List applied rows={dto.Rows.Count} checked={dto.CheckedCount} missing={dto.MissingCount}");
    }

    private static void ApplyItemStyle(ListViewItem item, string status)
    {
        if (status.Contains("Ontbreekt", StringComparison.OrdinalIgnoreCase))
        {
            item.ForeColor = Color.Firebrick;
            return;
        }

        if (status.Contains("Geen path gevonden", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("nog niet gecontroleerd", StringComparison.OrdinalIgnoreCase))
        {
            item.ForeColor = Color.DarkGoldenrod;
            return;
        }

        item.ForeColor = SystemColors.ControlText;
    }

    private void AutoFitOutputColumns()
    {
        try
        {
            if (_lv.Columns.Count < 3) return;

            var total = _lv.ClientSize.Width;
            if (total <= 0) return;

            const int pad = 50;
            var available = Math.Max(0, total - pad);

            const int aspMin = 140;
            const int statusMin = 160;
            const int pathMin = 240;

            int aspW = Math.Max(aspMin, Math.Min(260, available / 4));
            int statusW = Math.Max(statusMin, Math.Min(260, available / 4));

            int pathW = available - aspW - statusW;
            if (pathW < pathMin)
            {
                var shortage = pathMin - pathW;
                var takeAsp = shortage / 2;
                var takeStatus = shortage - takeAsp;

                aspW = Math.Max(aspMin, aspW - takeAsp);
                statusW = Math.Max(statusMin, statusW - takeStatus);

                pathW = Math.Max(pathMin, available - aspW - statusW);
            }

            _lv.Columns[ColAsp].Width = aspW;
            _lv.Columns[ColPath].Width = Math.Max(pathMin, pathW);
            _lv.Columns[ColStatus].Width = statusW;
        }
        catch (Exception ex)
        {
            Log.Warn("asppath", $"ui: AutoFitOutputColumns failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void CopySelectedRowsAsTsv()
    {
        try
        {
            if (_lv.SelectedItems.Count == 0) return;

            var sb = new StringBuilder();
            foreach (ListViewItem it in _lv.SelectedItems)
            {
                var asp = it.SubItems.Count > 0 ? it.SubItems[0].Text : "";
                var path = it.SubItems.Count > 1 ? it.SubItems[1].Text : "";
                var status = it.SubItems.Count > 2 ? it.SubItems[2].Text : "";
                sb.Append(asp).Append('\t').Append(path).Append('\t').Append(status).AppendLine();
            }

            Clipboard.SetText(sb.ToString());
            Log.Info("asppath", $"ui: Copy TSV selectedCount={_lv.SelectedItems.Count}");
        }
        catch (Exception ex)
        {
            Log.Warn("asppath", $"ui: Copy failed (non-fatal): {ex.GetType().Name}: {ex.Message}\n{ex}");
        }
    }

    // ---------------- Input grid helpers ----------------

    private static DataGridView CreateInputGrid()
    {
        var g = new DataGridView
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
            Font = new Font("Segoe UI", 9f),
            ReadOnly = true
        };

        g.ColumnHeadersDefaultCellStyle.Font = new Font(g.Font, FontStyle.Bold);
        return g;
    }

    private void PasteReplaceGridFromClipboard(DataGridView grid, string gridTag, string preferredHeaderKey)
    {
        try
        {
            var txt = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(txt))
            {
                Log.Info("asppath", $"ui: paste-replace grid[{gridTag}] clipboard empty");
                return;
            }

            txt = txt.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = txt.Split('\n');

            while (lines.Length > 0 && string.IsNullOrWhiteSpace(lines[^1]))
                lines = lines.Take(lines.Length - 1).ToArray();

            if (lines.Length == 0)
                return;

            var rows = lines.Select(l => (l ?? "").Split('\t')).ToList();
            var maxCols = rows.Max(r => r.Length);
            if (maxCols <= 0) return;

            var first = rows[0];
            bool tsvDetected = txt.Contains('\t');
            bool headerContainsKey = first.Any(c => string.Equals((c ?? "").Trim(), preferredHeaderKey, StringComparison.OrdinalIgnoreCase));
            bool treatFirstAsHeader = tsvDetected || headerContainsKey || maxCols > 1;

            string[] header;
            int startRow;

            if (treatFirstAsHeader)
            {
                header = first.Select(c => (c ?? "").Trim()).ToArray();
                startRow = 1;
            }
            else
            {
                header = new[] { preferredHeaderKey };
                startRow = 0;
            }

            BuildGridColumns(grid, header, maxCols);

            grid.Rows.Clear();

            for (int i = startRow; i < rows.Count; i++)
            {
                var cols = rows[i];
                var arr = new object[maxCols];
                for (int c = 0; c < maxCols; c++)
                    arr[c] = c < cols.Length ? (cols[c] ?? "").Trim() : "";

                grid.Rows.Add(arr);
            }

            AutoSizeGridColumns(grid, sampleRows: Math.Min(80, grid.Rows.Count));

            Log.Info("asppath",
                $"ui: paste-replace grid[{gridTag}] ok tsv={tsvDetected} headerUsed={treatFirstAsHeader} rows={grid.Rows.Count} cols={grid.Columns.Count} rawLen={txt.Length}");
        }
        catch (Exception ex)
        {
            Log.Warn("asppath", $"ui: paste-replace grid[{gridTag}] failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
            MessageBox.Show(this, "Plakken naar grid mislukt. Zie log (Alt+L).", "Clipboard",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void BuildGridColumns(DataGridView grid, string[] header, int maxCols)
    {
        grid.SuspendLayout();
        grid.Columns.Clear();

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
            grid.Columns.Add(col);
        }

        grid.ResumeLayout();
    }

    private static void AutoSizeGridColumns(DataGridView grid, int sampleRows)
    {
        try
        {
            if (grid.Columns.Count == 0) return;

            for (int c = 0; c < grid.Columns.Count; c++)
            {
                var col = grid.Columns[c];

                int w = TextRenderer.MeasureText(col.HeaderText, grid.ColumnHeadersDefaultCellStyle.Font ?? grid.Font).Width + 30;

                for (int r = 0; r < sampleRows; r++)
                {
                    var v = grid.Rows[r].Cells[c].Value?.ToString() ?? "";
                    if (v.Length > 120) v = v.Substring(0, 120);
                    var vw = TextRenderer.MeasureText(v, grid.Font).Width + 26;
                    if (vw > w) w = vw;
                    if (w > 420) { w = 420; break; }
                }

                if (w < 90) w = 90;
                if (w > 420) w = 420;

                col.Width = w;
            }
        }
        catch (Exception ex)
        {
            Log.Warn("asppath", $"ui: AutoSizeGridColumns failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string BuildRawFromGrid(DataGridView grid)
    {
        if (grid.Columns.Count == 0)
            return "";

        var sb = new StringBuilder();

        sb.Append(string.Join("\t", grid.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText)));
        sb.AppendLine();

        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;

            var cells = new string[grid.Columns.Count];
            for (int i = 0; i < cells.Length; i++)
                cells[i] = row.Cells[i].Value?.ToString() ?? "";

            int end = cells.Length;
            while (end > 1 && string.IsNullOrWhiteSpace(cells[end - 1]))
                end--;

            sb.Append(string.Join("\t", cells.Take(end)));
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
