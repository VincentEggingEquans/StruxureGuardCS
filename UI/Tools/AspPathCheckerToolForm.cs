using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using StruxureGuard.Core.Logging;
using StruxureGuard.Core.Tools.AspPathChecker;
using StruxureGuard.Core.Tools.Infrastructure;


namespace StruxureGuard.UI.Tools;

public sealed class AspPathCheckerToolForm : ToolBaseForm
{
    private readonly TextBox _txtAsp;
    private readonly TextBox _txtPaths;

    private readonly Button _btnBuild;
    private readonly Button _btnCheckSelected;
    private readonly Button _btnCheckAll;
    private readonly Button _btnClear;

    private readonly DataGridView _grid;

    private readonly Label _lblInfo;
    private readonly Label _lblOutput;

    private readonly Button _btnOpenLog;
    private readonly Button _btnOpenFolder;

    private readonly string _outputFile;

    public AspPathCheckerToolForm()
    {
        Text = "ASP Path Checker";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;

        _outputFile = GetDefaultOutputFile();

        _txtAsp = MakeMultilineBox();
        _txtPaths = MakeMultilineBox();

        _btnBuild = MakeButton("Lijst opbouwen", 140, async (_, __) => await BuildListAsync());
        _btnCheckSelected = MakeButton("Check geselecteerde", 160, async (_, __) => await CheckSelectedAsync());
        _btnCheckAll = MakeButton("Check alles", 120, async (_, __) => await CheckAllAsync());
        _btnClear = MakeButton("Lijst leegmaken", 130, (_, __) => ClearList());
        _btnOpenLog = MakeButton("Open log", 95, (_, __) => OpenLogFile());
        _btnOpenFolder = MakeButton("Open folder", 110, (_, __) => OpenLogFolder());

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackgroundColor = SystemColors.Window,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            MultiSelect = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ReadOnly = true,
            AutoGenerateColumns = false,
            Font = new Font("Segoe UI", 9f)
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "ASP-naam",
            DataPropertyName = nameof(AspPathRowDto.AspName),
            Width = 240,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Path",
            DataPropertyName = nameof(AspPathRowDto.Path),
            Width = 760,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Status",
            DataPropertyName = nameof(AspPathRowDto.Status),
            Width = 220,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        _grid.RowPrePaint += (_, e) =>
        {
            try
            {
                var row = _grid.Rows[e.RowIndex];
                var status = row.Cells[2].Value?.ToString() ?? "";

                // Rood: ontbreekt / gelogd
                if (status.Contains("Ontbreekt", StringComparison.OrdinalIgnoreCase))
                {
                    row.DefaultCellStyle.ForeColor = Color.Firebrick;
                    row.DefaultCellStyle.BackColor = SystemColors.Window;
                    return;
                }

                // Oranje/grijs: geen match
                if (status.Contains("Geen path gevonden", StringComparison.OrdinalIgnoreCase) ||
                    status.Contains("Geen ASP gevonden", StringComparison.OrdinalIgnoreCase))
                {
                    row.DefaultCellStyle.ForeColor = Color.DarkGoldenrod;
                    row.DefaultCellStyle.BackColor = SystemColors.Window;
                    return;
                }

                // Normaal
                row.DefaultCellStyle.ForeColor = SystemColors.ControlText;
                row.DefaultCellStyle.BackColor = SystemColors.Window;
            }
            catch
            {
                // non-fatal
            }
        };


        _lblInfo = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = "",
            ForeColor = SystemColors.GrayText
        };

        _lblOutput = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = $"Ontbrekende paden worden gelogd in: {_outputFile}",
            ForeColor = SystemColors.GrayText
        };

        // Layout
        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 3,
            Height = 260,
            Padding = new Padding(6)
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lblAsp = new Label { Text = "ASP-namen (één per regel):", AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
        var lblPaths = new Label { Text = "Paden (één per regel):", AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };

        top.Controls.Add(lblAsp, 0, 0);
        top.Controls.Add(lblPaths, 1, 0);
        top.Controls.Add(_txtAsp, 0, 1);
        top.Controls.Add(_txtPaths, 1, 1);

        var btnRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true
        };
        btnRow.Controls.Add(_btnBuild);
        btnRow.Controls.Add(_btnCheckSelected);
        btnRow.Controls.Add(_btnCheckAll);
        btnRow.Controls.Add(_btnClear);
        btnRow.Controls.Add(_btnOpenLog);
        btnRow.Controls.Add(_btnOpenFolder);

        top.Controls.Add(btnRow, 0, 2);
        top.SetColumnSpan(btnRow, 2);

        var gridCard = BuildCard("ASP / Path lijst (na koppelen)", _grid);

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            Padding = new Padding(6)
        };
        bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottom.Controls.Add(_lblInfo, 0, 0);
        bottom.Controls.Add(_lblOutput, 0, 1);

        Controls.Add(gridCard);
        Controls.Add(top);
        Controls.Add(bottom);

        Log.Info("asppath", "ASPPathChecker form constructed");
    }

    protected override void UpdateUiRunningState(bool isRunning)
    {
        _btnBuild.Enabled = !isRunning;
        _btnCheckSelected.Enabled = !isRunning;
        _btnCheckAll.Enabled = !isRunning;
        _btnClear.Enabled = !isRunning;
        _btnOpenLog.Enabled = !isRunning;
        _btnOpenFolder.Enabled = !isRunning;

        _txtAsp.Enabled = !isRunning;
        _txtPaths.Enabled = !isRunning;
        _grid.Enabled = !isRunning;
    }

    private async System.Threading.Tasks.Task BuildListAsync()
    {
        Log.Info("asppath", "CLICK Build list");

        var tool = new AspPathCheckerTool();
        var p = ToolParameters.Empty
            .With(AspPathCheckerParameterKeys.Mode, "build")
            .With(AspPathCheckerParameterKeys.AspText, _txtAsp.Text ?? "")
            .With(AspPathCheckerParameterKeys.PathText, _txtPaths.Text ?? "")
            .With(AspPathCheckerParameterKeys.OutputFile, _outputFile);

        await RunToolAsync(
            tool: tool,
            parameters: p,
            toolLogTag: "asppath",
            onCompleted: r =>
            {
                PopulateFromResult(r);
                return System.Threading.Tasks.Task.CompletedTask;
            },
            showWarningsOnSuccess: false,
            successMessageFactory: _ => "");
    }

    private async System.Threading.Tasks.Task CheckSelectedAsync()
    {
        Log.Info("asppath", "CLICK Check selected");

        var selected = _grid.SelectedRows.Cast<DataGridViewRow>()
            .Select(r => r.Index)
            .Distinct()
            .OrderBy(i => i)
            .ToList();

        if (selected.Count == 0)
        {
            _lblInfo.Text = "Geen selectie.";
            Log.Info("asppath", "CheckSelected: no selection");
            return;
        }

        await CheckAsync(checkAll: false, selectedIndices: selected);
    }

    private async System.Threading.Tasks.Task CheckAllAsync()
    {
        Log.Info("asppath", "CLICK Check all");
        await CheckAsync(checkAll: true, selectedIndices: new List<int>());
    }

    private async System.Threading.Tasks.Task CheckAsync(bool checkAll, List<int> selectedIndices)
    {
        var tool = new AspPathCheckerTool();

        var p = ToolParameters.Empty
            .With(AspPathCheckerParameterKeys.Mode, "check")
            .With(AspPathCheckerParameterKeys.CheckAll, checkAll ? "true" : "false")
            .With(AspPathCheckerParameterKeys.SelectedIndices, string.Join(",", selectedIndices))
            .With(AspPathCheckerParameterKeys.AspText, _txtAsp.Text ?? "")
            .With(AspPathCheckerParameterKeys.PathText, _txtPaths.Text ?? "")
            .With(AspPathCheckerParameterKeys.OutputFile, _outputFile);

        await RunToolAsync(
            tool: tool,
            parameters: p,
            toolLogTag: "asppath",
            onCompleted: r =>
            {
                PopulateFromResult(r);
                return System.Threading.Tasks.Task.CompletedTask;
            },
            showWarningsOnSuccess: false,
            successMessageFactory: _ => "");
    }

    private void ClearList()
    {
        Log.Info("asppath", "CLICK Clear list");
        _grid.DataSource = null;
        _lblInfo.Text = "Lijst geleegd.";
    }

    private void PopulateFromResult(ToolResult r)
    {
        if (!r.Success) return;

        var json = r.TryGetOutput(AspPathCheckerTool.OutputKeyResultJson);
        if (string.IsNullOrWhiteSpace(json))
        {
            Log.Warn("asppath", "ResultJson missing");
            return;
        }

        var dto = JsonSerializer.Deserialize<AspPathCheckerResultDto>(json);
        if (dto is null)
        {
            Log.Warn("asppath", "ResultJson deserialize failed");
            return;
        }

        _grid.DataSource = dto.Rows;

        _lblInfo.Text =
            $"ASP={dto.AspCount} | Paths={dto.PathCount} | Matches={dto.MatchCount} | " +
            $"ASP zonder match={dto.AspWithoutMatchCount} | Paths zonder ASP={dto.PathsWithoutAspCount} | " +
            $"Checked={dto.CheckedCount} | Missing={dto.MissingCount}";

        Log.Info("asppath", $"UI updated rows={dto.Rows.Count} checked={dto.CheckedCount} missing={dto.MissingCount}");
    }

    private static TextBox MakeMultilineBox()
    {
        var tb = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = false,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9f)
        };

        // Paste-replace behavior (Ctrl+V)
        tb.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.V)
            {
                e.SuppressKeyPress = true;
                var txt = Clipboard.GetText();
                if (!string.IsNullOrEmpty(txt))
                {
                    tb.Text = txt;
                    tb.SelectionStart = tb.TextLength;
                }
            }
        };

        return tb;
    }

    private static Button MakeButton(string text, int width, EventHandler onClick)
    {
        var b = new Button { Text = text, Width = width };
        b.Click += onClick;
        return b;
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

    private static string GetDefaultOutputFile()
    {
        // match your file-log location convention
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StruxureGuard",
            "Logs");

        Directory.CreateDirectory(baseDir);

        return Path.Combine(baseDir, "missing_asp_paths.txt");
    }

    private void OpenLogFile()
{
    try
    {
        Log.Info("asppath", $"CLICK Open log file '{_outputFile}'");

        if (!File.Exists(_outputFile))
        {
            Log.Warn("asppath", $"Log file not found: '{_outputFile}'");
            MessageBox.Show(this, "Logbestand bestaat nog niet. Doe eerst een check.", "Open log",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = _outputFile,
            UseShellExecute = true
        });
    }
    catch (Exception ex)
    {
        Log.Warn("asppath", $"OpenLogFile failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
        MessageBox.Show(this, "Kon logbestand niet openen. Zie log (Alt+L).", "Open log",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}

private void OpenLogFolder()
{
    try
    {
        var folder = Path.GetDirectoryName(_outputFile) ?? "";
        Log.Info("asppath", $"CLICK Open log folder '{folder}'");

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            Log.Warn("asppath", $"Log folder not found: '{folder}'");
            MessageBox.Show(this, "Logfolder bestaat niet.", "Open folder",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }
    catch (Exception ex)
    {
        Log.Warn("asppath", $"OpenLogFolder failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
        MessageBox.Show(this, "Kon folder niet openen. Zie log (Alt+L).", "Open folder",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}

}
