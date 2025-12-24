using System;
using System.Diagnostics;
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
    private readonly TextBox _txtOutputFile;

    private readonly Button _btnBuild;
    private readonly Button _btnCheckSelected;
    private readonly Button _btnCheckAll;
    private readonly Button _btnOpenOutput;

    private readonly DataGridView _grid;
    private readonly Label _lblStatus;

    public AspPathCheckerToolForm()
    {
        Text = "ASP Path Checker";
        MinimumSize = new Size(1100, 650);

        Log.Info("asppath", "ASPPathChecker form constructed");

        var root = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            SplitterDistance = 420
        };
        Controls.Add(root);

        // LEFT: inputs
        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Padding = new Padding(10)
        };
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // ASP label
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 45)); // ASP box
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // PATH label
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 45)); // PATH box
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // output label
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // output box
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // buttons
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // status
        root.Panel1.Controls.Add(left);

        left.Controls.Add(new Label { Text = "ASP input (TSV: kolom 'Name' of losse regels):", AutoSize = true }, 0, 0);

        _txtAsp = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Dock = DockStyle.Fill
        };
        _txtAsp.KeyDown += OnPasteReplaceKeyDown;
        left.Controls.Add(_txtAsp, 0, 1);

        left.Controls.Add(new Label { Text = "Paths input (TSV: kolom 'Path' of losse regels):", AutoSize = true }, 0, 2);

        _txtPaths = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Dock = DockStyle.Fill
        };
        _txtPaths.KeyDown += OnPasteReplaceKeyDown;
        left.Controls.Add(_txtPaths, 0, 3);

        left.Controls.Add(new Label { Text = "Output file (missings):", AutoSize = true }, 0, 4);

        _txtOutputFile = new TextBox
        {
            Dock = DockStyle.Top,
            Width = 380
        };
        _txtOutputFile.Text = Path.Combine(Log.LogFolder, "missing_asp_paths.txt");
        left.Controls.Add(_txtOutputFile, 0, 5);

        var btnRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false
        };
        left.Controls.Add(btnRow, 0, 6);

        _btnBuild = new Button { Text = "Build list", AutoSize = true };
        _btnBuild.Click += (_, __) => RunBuild();
        btnRow.Controls.Add(_btnBuild);

        _btnCheckSelected = new Button { Text = "Check selected", AutoSize = true };
        _btnCheckSelected.Click += (_, __) => RunCheckSelected();
        btnRow.Controls.Add(_btnCheckSelected);

        _btnCheckAll = new Button { Text = "Check all", AutoSize = true };
        _btnCheckAll.Click += (_, __) => RunCheckAll();
        btnRow.Controls.Add(_btnCheckAll);

        _btnOpenOutput = new Button { Text = "Open output file", AutoSize = true };
        _btnOpenOutput.Click += (_, __) => OpenOutputFile();
        btnRow.Controls.Add(_btnOpenOutput);

        _lblStatus = new Label
        {
            Text = "Ready.",
            AutoSize = true,
            Dock = DockStyle.Top
        };
        left.Controls.Add(_lblStatus, 0, 7);

        // RIGHT: grid
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true,
            AutoGenerateColumns = false
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "AspName",
            HeaderText = "ASP",
            DataPropertyName = "AspName",
            Width = 220
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Path",
            HeaderText = "Path",
            DataPropertyName = "Path",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Status",
            HeaderText = "Status",
            DataPropertyName = "Status",
            Width = 180
        });

        root.Panel2.Controls.Add(_grid);

        UpdateUiRunningState(false);
    }

    protected override void UpdateUiRunningState(bool isRunning)
    {
        _btnBuild.Enabled = !isRunning;
        _btnCheckSelected.Enabled = !isRunning;
        _btnCheckAll.Enabled = !isRunning;
        _btnOpenOutput.Enabled = !isRunning;

        _txtAsp.ReadOnly = isRunning;
        _txtPaths.ReadOnly = isRunning;
        _txtOutputFile.ReadOnly = isRunning;
    }

    protected override void OnProgress(ToolProgressInfo p)
    {
        _lblStatus.Text = $"{p.Phase} - {p.Percent}% - {p.Message}";
    }

    private void RunBuild()
    {
        Log.Info("asppath", "CLICK Build list");

        var tool = new AspPathCheckerTool();

        var parameters = new ToolParameters()
            .With(AspPathCheckerParameterKeys.Mode, "build")
            .With(AspPathCheckerParameterKeys.AspText, _txtAsp.Text)
            .With(AspPathCheckerParameterKeys.PathText, _txtPaths.Text)
            .With(AspPathCheckerParameterKeys.OutputFile, _txtOutputFile.Text);

        _ = RunToolAsync(
            tool,
            parameters,
            toolLogTag: "asppath",
            onCompleted: result =>
            {
                ApplyResultToGrid(result);
                return System.Threading.Tasks.Task.CompletedTask;
            },
            successMessage: "");
    }

    private void RunCheckSelected()
    {
        var sel = _grid.SelectedRows.Cast<DataGridViewRow>()
            .Select(r => r.Index)
            .Distinct()
            .OrderBy(i => i)
            .ToList();

        Log.Info("asppath", $"CLICK Check selected selectedCount={sel.Count}");

        if (sel.Count == 0)
        {
            MessageBox.Show(this, "Selecteer eerst één of meerdere regels in de lijst.", "Check selected",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var tool = new AspPathCheckerTool();

        var parameters = new ToolParameters()
            .With(AspPathCheckerParameterKeys.Mode, "check")
            .With(AspPathCheckerParameterKeys.CheckAll, "false")
            .With(AspPathCheckerParameterKeys.SelectedIndices, string.Join(",", sel))
            .With(AspPathCheckerParameterKeys.AspText, _txtAsp.Text)
            .With(AspPathCheckerParameterKeys.PathText, _txtPaths.Text)
            .With(AspPathCheckerParameterKeys.OutputFile, _txtOutputFile.Text);

        _ = RunToolAsync(
            tool,
            parameters,
            toolLogTag: "asppath",
            onCompleted: result =>
            {
                ApplyResultToGrid(result);
                return System.Threading.Tasks.Task.CompletedTask;
            },
            successMessage: "");
    }

    private void RunCheckAll()
    {
        Log.Info("asppath", "CLICK Check all");

        var tool = new AspPathCheckerTool();

        var parameters = new ToolParameters()
            .With(AspPathCheckerParameterKeys.Mode, "check")
            .With(AspPathCheckerParameterKeys.CheckAll, "true")
            .With(AspPathCheckerParameterKeys.SelectedIndices, "")
            .With(AspPathCheckerParameterKeys.AspText, _txtAsp.Text)
            .With(AspPathCheckerParameterKeys.PathText, _txtPaths.Text)
            .With(AspPathCheckerParameterKeys.OutputFile, _txtOutputFile.Text);

        _ = RunToolAsync(
            tool,
            parameters,
            toolLogTag: "asppath",
            onCompleted: result =>
            {
                ApplyResultToGrid(result);
                return System.Threading.Tasks.Task.CompletedTask;
            },
            successMessage: "");
    }

    private void ApplyResultToGrid(ToolResult result)
    {
        var json = result.TryGetOutput(AspPathCheckerTool.OutputKeyResultJson);
        if (string.IsNullOrWhiteSpace(json))
        {
            Log.Warn("asppath", "No ResultJson output found.");
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
            Log.Warn("asppath", $"Deserialize result failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
            MessageBox.Show(this, "Kon resultaat niet lezen. Zie log (Alt+L).", "ASP Path Checker",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (dto == null)
            return;

        _grid.Rows.Clear();
        foreach (var r in dto.Rows)
        {
            var idx = _grid.Rows.Add(r.AspName, r.Path, r.Status);
            ApplyRowStyle(_grid.Rows[idx], r.Status);
        }

        _lblStatus.Text = result.Summary;

        Log.Info("asppath", $"UI updated rows={dto.Rows.Count} checked={dto.CheckedCount} missing={dto.MissingCount}");
    }

    private static void ApplyRowStyle(DataGridViewRow row, string status)
    {
        status ??= "";

        // Rood: ontbreekt / gelogd
        if (status.Contains("Ontbreekt", StringComparison.OrdinalIgnoreCase))
        {
            row.DefaultCellStyle.ForeColor = Color.Firebrick;
            row.DefaultCellStyle.BackColor = SystemColors.Window;
            return;
        }

        // Oranje: geen match
        if (status.Contains("Geen path gevonden", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("Geen ASP gevonden", StringComparison.OrdinalIgnoreCase))
        {
            row.DefaultCellStyle.ForeColor = Color.DarkGoldenrod;
            row.DefaultCellStyle.BackColor = SystemColors.Window;
            return;
        }

        // Default
        row.DefaultCellStyle.ForeColor = SystemColors.ControlText;
        row.DefaultCellStyle.BackColor = SystemColors.Window;
    }

    private void OpenOutputFile()
    {
        var file = (_txtOutputFile.Text ?? "").Trim();
        Log.Info("asppath", $"Open log file '{file}'");

        try
        {
            if (!File.Exists(file))
            {
                MessageBox.Show(this, "Output file bestaat nog niet (nog geen missings gelogd).", "Open output",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = file,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Warn("asppath", $"OpenOutputFile failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
            MessageBox.Show(this, "Kon output file niet openen. Zie log (Alt+L).", "Open output",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnPasteReplaceKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.V)
        {
            try
            {
                var txt = Clipboard.GetText() ?? "";
                if (sender is TextBox tb)
                {
                    tb.Text = txt;
                    tb.SelectionStart = tb.TextLength;
                    tb.SelectionLength = 0;

                    Log.Info("asppath", $"Paste-replace into textbox len={txt.Length}");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("asppath", $"Paste-replace failed: {ex.GetType().Name}: {ex.Message}\n{ex}");
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }
    }
}
