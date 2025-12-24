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
    // INPUT grids (ModbusGroupAdvisor-style)
    private readonly DataGridView _gridAsp;
    private readonly DataGridView _gridPaths;

    private readonly Button _btnBuild;
    private readonly Button _btnCheckSelected;
    private readonly Button _btnCheckAll;

    // OUTPUT list (right)
    private readonly ListView _lv;
    private readonly Label _lblStatus;

    // Column indices in output list
    private const int ColAsp = 0;
    private const int ColPath = 1;
    private const int ColStatus = 2;

    public AspPathCheckerToolForm()
    {
        Text = "ASP Path Checker";
        MinimumSize = new Size(1470, 650);

        Log.Info("asppath", "ui: form constructed");

        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // main area
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // bottom buttons/status
        Controls.Add(outer);

        var root = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            FixedPanel = FixedPanel.Panel1,
            IsSplitterFixed = false
        };
        outer.Controls.Add(root, 0, 0);

        // Belangrijk: pas splitter pas na layout/Shown toe (anders pakt hij soms niet)
        Shown += (_, _) =>
        {
            root.SplitterDistance = 700; // LINKS 700px -> RECHTS smaller. Wil je rechts breder? lager maken.
            BeginInvoke(new Action(AutoFitOutputColumns));
        };

        // Als je de splitter sleept: kolommen opnieuw fitten
        root.SplitterMoved += (_, _) => AutoFitOutputColumns();


        // ---------------- LEFT (inputs) ----------------
        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10)
        };
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // ASP label
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 50));     // ASP grid
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // Paths label
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 50));     // Paths grid
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // buttons
        root.Panel1.Controls.Add(left);

        left.Controls.Add(new Label { Text = "ASP input (plak TSV export: kolom 'Name'):", AutoSize = true }, 0, 0);

        _gridAsp = CreateInputGrid();
        _gridAsp.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.V)
            {
                e.SuppressKeyPress = true;
                PasteReplaceGridFromClipboard(_gridAsp, gridTag: "asp", preferredHeaderKey: "Name");
            }
        };
        left.Controls.Add(_gridAsp, 0, 1);

        left.Controls.Add(new Label { Text = "Path input (plak TSV export: kolom 'Path'):", AutoSize = true }, 0, 2);

        _gridPaths = CreateInputGrid();
        _gridPaths.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.V)
            {
                e.SuppressKeyPress = true;
                PasteReplaceGridFromClipboard(_gridPaths, gridTag: "path", preferredHeaderKey: "Path");
            }
        };
        left.Controls.Add(_gridPaths, 0, 3);

        var btnRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false
        };

        _btnBuild = new Button { Text = "Build list" };
        _btnBuild.Click += (_, _) => RunBuild();

        _btnCheckSelected = new Button { Text = "Check selected" };
        _btnCheckSelected.Click += (_, _) => RunCheckSelected();

        _btnCheckAll = new Button { Text = "Check all" };
        _btnCheckAll.Click += (_, _) => RunCheckAll();

        btnRow.Controls.Add(_btnBuild);
        btnRow.Controls.Add(_btnCheckSelected);
        btnRow.Controls.Add(_btnCheckAll);

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(10, 0, 10, 10)
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outer.Controls.Add(bottom, 0, 1);

        bottom.Controls.Add(btnRow, 0, 0);

        // Status rechts onderin (zelfde rij als knoppen)
        _lblStatus.TextAlign = ContentAlignment.MiddleRight;
        _lblStatus.Dock = DockStyle.Fill;
        bottom.Controls.Add(_lblStatus, 1, 0);


        // ---------------- RIGHT (output) ----------------
        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10)
        };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Panel2.Controls.Add(right);

        _lv = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            BorderStyle = BorderStyle.FixedSingle
        };

        _lv.Columns.Add("ASP", 260, HorizontalAlignment.Left);
        _lv.Columns.Add("Path", 200, HorizontalAlignment.Left); // wordt toch direct autofit
        _lv.Columns.Add("Status", 260, HorizontalAlignment.Left);

        _lv.Resize += (_, _) => AutoFitOutputColumns();
        right.Controls.Add(_lv, 0, 0);

        _lblStatus = new Label { AutoSize = true, Text = "" };
        right.Controls.Add(_lblStatus, 0, 1);

        AutoFitOutputColumns();
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
        var sel = _lv.SelectedIndices.Cast<int>()
            .Distinct()
            .OrderBy(i => i)
            .ToList();

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

            // ruimte voor borders + verticale scrollbar
            const int pad = 50;

            // Doel “mooie” widths, maar nooit overflow
            var available = Math.Max(0, total - pad);

            // Basis max widths (zoals je mooi vindt)
            int aspMax = 260;
            int statusMax = 260;

            // Minimums (zodat het leesbaar blijft)
            const int aspMin = 140;
            const int statusMin = 160;
            const int pathMin = 150;

            // Als het krap wordt: laat ASP/Status mee krimpen
            int aspW = Math.Min(aspMax, available / 4);
            int statusW = Math.Min(statusMax, available / 4);

            aspW = Math.Max(aspMin, aspW);
            statusW = Math.Max(statusMin, statusW);

            // Path krijgt rest, maar minimaal pathMin
            int pathW = available - aspW - statusW;
            if (pathW < pathMin)
            {
                // Nog krapper: knijp ASP/Status extra zodat Path minimaal past
                int shortage = pathMin - pathW;

                int takeFromAsp = shortage / 2;
                int takeFromStatus = shortage - takeFromAsp;

                aspW = Math.Max(aspMin, aspW - takeFromAsp);
                statusW = Math.Max(statusMin, statusW - takeFromStatus);

                pathW = Math.Max(pathMin, available - aspW - statusW);
            }

            _lv.Columns[ColAsp].Width = aspW;
            _lv.Columns[ColStatus].Width = statusW;
            _lv.Columns[ColPath].Width = Math.Max(pathMin, pathW);
        }
        catch (Exception ex)
        {
            Log.Warn("asppath", $"ui: AutoFitOutputColumns failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
        }
    }


    // ---------------- Input grid helpers (ModbusGroupAdvisor pattern) ----------------

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
                Log.Info("asppath", $"PasteReplaceGrid[{gridTag}]: clipboard empty");
                return;
            }

            txt = txt.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = txt.Split('\n');

            // remove trailing empty lines
            while (lines.Length > 0 && string.IsNullOrWhiteSpace(lines[^1]))
                lines = lines.Take(lines.Length - 1).ToArray();

            if (lines.Length == 0)
                return;

            var rows = lines.Select(l => (l ?? "").Split('\t')).ToList();
            var maxCols = rows.Max(r => r.Length);
            if (maxCols <= 0) return;

            // Determine header strategy:
            // - If it looks like TSV (tabs present) -> first row = header
            // - Or if first row contains preferredHeaderKey -> header
            // - Else fallback: still treat first row as header if multi-col
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
                // single-column list with no clear header -> create header ourselves and include all lines
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
                $"PasteReplaceGrid[{gridTag}] ok tsv={tsvDetected} headerUsed={treatFirstAsHeader} rows={grid.Rows.Count} cols={grid.Columns.Count} rawLen={txt.Length}");
        }
        catch (Exception ex)
        {
            Log.Warn("asppath", $"PasteReplaceGrid[{gridTag}] failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
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
                    if (w > 520) { w = 520; break; }
                }

                if (w < 90) w = 90;
                if (w > 520) w = 520;

                col.Width = w;
            }
        }
        catch (Exception ex)
        {
            Log.Warn("asppath", $"AutoSizeGridColumns failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string BuildRawFromGrid(DataGridView grid)
    {
        if (grid.Columns.Count == 0)
            return "";

        var sb = new StringBuilder();

        // header
        sb.Append(string.Join("\t", grid.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText)));
        sb.AppendLine();

        // rows
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;

            var cells = new string[grid.Columns.Count];
            for (int i = 0; i < cells.Length; i++)
                cells[i] = row.Cells[i].Value?.ToString() ?? "";

            // trim trailing empty columns (nicer output)
            int end = cells.Length;
            while (end > 1 && string.IsNullOrWhiteSpace(cells[end - 1]))
                end--;

            sb.Append(string.Join("\t", cells.Take(end)));
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
