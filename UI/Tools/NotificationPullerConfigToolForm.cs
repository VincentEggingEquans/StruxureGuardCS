using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using StruxureGuard.Core.Logging;
using StruxureGuard.Core.Tools.Infrastructure;
using StruxureGuard.Core.Tools.NotificationPullerConfig;

namespace StruxureGuard.UI.Tools;

public sealed class NotificationPullerConfigToolForm : ToolBaseForm
{
    private readonly TextBox _txtIps;

    private readonly TextBox _txtUsername;
    private readonly TextBox _txtPw1;
    private readonly TextBox _txtPw2;
    private readonly TextBox _txtPw3;
    private readonly NumericUpDown _numPort;

    private readonly TextBox _txtRemoteDir;
    private readonly TextBox _txtPattern;
    private readonly TextBox _txtExportRoot;

    private readonly CheckBox _chkZip;
    private readonly ComboBox _cmbSaveMode;

    private readonly CheckBox _chkDelOh;
    private readonly CheckBox _chkDelAssets;
    private readonly CheckBox _chkDelCustom;
    private readonly TextBox _txtDelCustomPattern;
    private readonly CheckBox _chkDelAll;

    private readonly TextBox _txtConfigPath;
    private readonly Button _btnBrowse;
    private readonly Button _btnGenerate;

    private readonly TextBox _txtPreview;
    private readonly Label _lblStatus;

    public NotificationPullerConfigToolForm()
    {
        Text = "NotificationPuller Config";
        MinimumSize = new Size(1200, 750);

        Log.Info("notif-puller", "ui: form constructed");

        // Outer: main + bottom status
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(outer);

        // Main split: left inputs / right preview
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            FixedPanel = FixedPanel.Panel1,
            IsSplitterFixed = false,
            SplitterDistance = 520
        };
        outer.Controls.Add(split, 0, 0);

        // Left: scrollable inputs
        var leftScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        split.Panel1.Controls.Add(leftScroll);

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(10)
        };
        left.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        leftScroll.Controls.Add(left);

        int r = 0;

        // IP list
        left.Controls.Add(new Label { Text = "ASP IP-adressen (1 per regel):", AutoSize = true }, 0, r);
        left.SetColumnSpan(left.Controls[left.Controls.Count - 1], 2);
        r++;

        _txtIps = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Height = 160,
            Dock = DockStyle.Top
        };
        left.Controls.Add(_txtIps, 0, r);
        left.SetColumnSpan(_txtIps, 2);
        r++;

        // SSH group
        var grpSsh = new GroupBox { Text = "SSH-gegevens", Dock = DockStyle.Top, AutoSize = true };
        var ssh = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(10) };
        ssh.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        ssh.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grpSsh.Controls.Add(ssh);

        _txtUsername = NewTextBox();
        _txtPw1 = NewPasswordBox();
        _txtPw2 = NewPasswordBox();
        _txtPw3 = NewPasswordBox();

        _numPort = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 22, Width = 120 };

        AddRow(ssh, "SSH-username:", _txtUsername);
        AddRow(ssh, "Primary SSH-password:", _txtPw1);
        AddRow(ssh, "Secondary SSH-password:", _txtPw2);
        AddRow(ssh, "Tertiary SSH-password:", _txtPw3);
        AddRow(ssh, "SSH-port:", _numPort);

        left.Controls.Add(grpSsh, 0, r);
        left.SetColumnSpan(grpSsh, 2);
        r++;

        // Remote location
        var grpRemote = new GroupBox { Text = "ASP notificatie-locatie", Dock = DockStyle.Top, AutoSize = true };
        var rem = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(10) };
        rem.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        rem.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grpRemote.Controls.Add(rem);

        _txtRemoteDir = NewTextBox("/var/sbo/db/notifications");
        AddRow(rem, "Remote notification dir:", _txtRemoteDir);

        var lblInfo = new Label
        {
            Text = "ASP-naam wordt bepaald via /var/sbo/db_backup/LocalBackup.",
            AutoSize = true
        };
        rem.Controls.Add(lblInfo, 0, rem.RowCount);
        rem.SetColumnSpan(lblInfo, 2);
        rem.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        left.Controls.Add(grpRemote, 0, r);
        left.SetColumnSpan(grpRemote, 2);
        r++;

        // Filter
        var grpFilter = new GroupBox { Text = "Bestandsselectie (filter)", Dock = DockStyle.Top, AutoSize = true };
        var fil = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(10) };
        fil.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        fil.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grpFilter.Controls.Add(fil);

        _txtPattern = NewTextBox("*");
        AddRow(fil, "Bestandsnaam-patroon:", _txtPattern);

        var lblFilter = new Label { Text = "Filtert notificatiebestanden, bijv. '*.xlsx'.", AutoSize = true };
        fil.Controls.Add(lblFilter, 0, fil.RowCount);
        fil.SetColumnSpan(lblFilter, 2);
        fil.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        left.Controls.Add(grpFilter, 0, r);
        left.SetColumnSpan(grpFilter, 2);
        r++;

        // Export
        var grpExport = new GroupBox { Text = "Exportmap (REMOTE PC)", Dock = DockStyle.Top, AutoSize = true };
        var exp = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(10) };
        exp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        exp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grpExport.Controls.Add(exp);

        _txtExportRoot = NewTextBox(@"C:\Notificationpuller_Export");
        AddRow(exp, "Export root (lokaal op agent):", _txtExportRoot);

        left.Controls.Add(grpExport, 0, r);
        left.SetColumnSpan(grpExport, 2);
        r++;

        // Options
        var grpOpt = new GroupBox { Text = "Opties", Dock = DockStyle.Top, AutoSize = true };
        var opt = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(10) };
        opt.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        opt.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grpOpt.Controls.Add(opt);

        _chkZip = new CheckBox { Text = "Maak ZIP van exportmap", Checked = true, AutoSize = true };
        opt.Controls.Add(_chkZip, 0, 0);
        opt.SetColumnSpan(_chkZip, 2);
        opt.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _cmbSaveMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        _cmbSaveMode.Items.AddRange(new object[] { "all", "latest" });
        _cmbSaveMode.SelectedIndex = 0;

        AddRow(opt, "Opslagmodus:", _cmbSaveMode);

        var lblSave = new Label
        {
            AutoSize = true,
            Text = "all    = alle bestanden die aan patroon voldoen\r\nlatest = alleen het 'laatste' bestand op naam"
        };
        opt.Controls.Add(lblSave, 0, opt.RowCount);
        opt.SetColumnSpan(lblSave, 2);
        opt.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        left.Controls.Add(grpOpt, 0, r);
        left.SetColumnSpan(grpOpt, 2);
        r++;

        // Cleanup
        var grpClean = new GroupBox { Text = "Opschonen notificatie-map", Dock = DockStyle.Top, AutoSize = true };
        var cl = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(10) };
        cl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        cl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grpClean.Controls.Add(cl);

        _chkDelOh = new CheckBox { Text = "Verwijder bestanden met 'OH_' in de naam", AutoSize = true };
        _chkDelAssets = new CheckBox { Text = "Verwijder bestanden met 'Assetslijst_' in de naam", AutoSize = true };
        _chkDelCustom = new CheckBox { Text = "Verwijder bestanden die matchen met patroon:", AutoSize = true };
        _txtDelCustomPattern = NewTextBox();
        _txtDelCustomPattern.Enabled = false;

        _chkDelAll = new CheckBox { Text = "Verwijder ALLE bestanden in notification-dir", AutoSize = true };

        _chkDelCustom.CheckedChanged += (_, _) =>
        {
            _txtDelCustomPattern.Enabled = _chkDelCustom.Checked;
            Log.Info("notif-puller", $"ui: delete_custom toggled={_chkDelCustom.Checked}");
        };

        cl.Controls.Add(_chkDelOh, 0, 0);
        cl.SetColumnSpan(_chkDelOh, 2);
        cl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        cl.Controls.Add(_chkDelAssets, 0, 1);
        cl.SetColumnSpan(_chkDelAssets, 2);
        cl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        cl.Controls.Add(_chkDelCustom, 0, 2);
        cl.Controls.Add(_txtDelCustomPattern, 1, 2);
        cl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        cl.Controls.Add(_chkDelAll, 0, 3);
        cl.SetColumnSpan(_chkDelAll, 2);
        cl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lblClean = new Label
        {
            AutoSize = true,
            Text = "Let op: verwijderen gebeurt NA downloaden.\r\nAls 'verwijder alle bestanden' aan staat, hebben andere opties geen effect."
        };
        cl.Controls.Add(lblClean, 0, 4);
        cl.SetColumnSpan(lblClean, 2);
        cl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        left.Controls.Add(grpClean, 0, r);
        left.SetColumnSpan(grpClean, 2);
        r++;

        // Config path
        var grpCfg = new GroupBox { Text = "Configbestand", Dock = DockStyle.Top, AutoSize = true };
        var cfg = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Padding = new Padding(10) };
        cfg.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        cfg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cfg.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        grpCfg.Controls.Add(cfg);

        _txtConfigPath = NewTextBox(Path.Combine(Environment.CurrentDirectory, "notificationpuller_config.json"));
        _btnBrowse = new Button { Text = "Bladeren...", Width = 110 };
        _btnBrowse.Click += (_, _) => BrowseConfigPath();

        cfg.Controls.Add(new Label { Text = "Configpad (lokaal):", AutoSize = true }, 0, 0);
        cfg.Controls.Add(_txtConfigPath, 1, 0);
        cfg.Controls.Add(_btnBrowse, 2, 0);
        cfg.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        left.Controls.Add(grpCfg, 0, r);
        left.SetColumnSpan(grpCfg, 2);
        r++;

        // Generate button
        _btnGenerate = new Button { Text = "Configbestand genereren", Height = 34, Dock = DockStyle.Top };
        _btnGenerate.Click += (_, _) => RunGenerate();

        left.Controls.Add(_btnGenerate, 0, r);
        left.SetColumnSpan(_btnGenerate, 2);
        r++;

        // Right: preview
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, Padding = new Padding(10) };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        split.Panel2.Controls.Add(right);

        _txtPreview = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9f)
        };
        right.Controls.Add(_txtPreview, 0, 0);

        // Bottom status row
        _lblStatus = new Label { AutoSize = true, Text = "", Padding = new Padding(10, 0, 10, 10) };
        outer.Controls.Add(_lblStatus, 0, 1);

        // Nice default: apply splitter after layout
        Shown += (_, _) => BeginInvoke(new Action(() => split.SplitterDistance = 520));
    }

    protected override void UpdateUiRunningState(bool isRunning)
    {
        _btnGenerate.Enabled = !isRunning;
        _btnBrowse.Enabled = !isRunning;
        _txtIps.Enabled = !isRunning;
        _txtPreview.Enabled = !isRunning;
        if (isRunning) _lblStatus.Text = "Bezig...";
    }

    private void BrowseConfigPath()
    {
        Log.Info("notif-puller", "ui: click browse config path");

        using var dlg = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json",
            FileName = Path.GetFileName(_txtConfigPath.Text.Trim().Length == 0 ? "notificationpuller_config.json" : _txtConfigPath.Text)
        };

        var current = _txtConfigPath.Text.Trim();
        if (current.Length > 0)
        {
            try
            {
                dlg.InitialDirectory = Path.GetDirectoryName(current);
                dlg.FileName = Path.GetFileName(current);
            }
            catch { /* ignore */ }
        }

        if (dlg.ShowDialog(this) == DialogResult.OK)
            _txtConfigPath.Text = dlg.FileName;
    }

    private void RunGenerate()
    {
        Log.Info("notif-puller", "ui: CLICK generate config");

        var tool = new NotificationPullerConfigTool();

        var p = ToolParameters.Empty
            .With(NotificationPullerConfigParameterKeys.IpsRaw, _txtIps.Text ?? "")
            .With(NotificationPullerConfigParameterKeys.Username, _txtUsername.Text ?? "")
            .With(NotificationPullerConfigParameterKeys.Password1, _txtPw1.Text ?? "")
            .With(NotificationPullerConfigParameterKeys.Password2, _txtPw2.Text ?? "")
            .With(NotificationPullerConfigParameterKeys.Password3, _txtPw3.Text ?? "")
            .With(NotificationPullerConfigParameterKeys.SshPort, ((int)_numPort.Value).ToString())
            .With(NotificationPullerConfigParameterKeys.RemoteNotificationsDir, _txtRemoteDir.Text ?? "")
            .With(NotificationPullerConfigParameterKeys.Pattern, _txtPattern.Text ?? "")
            .With(NotificationPullerConfigParameterKeys.ExportRoot, _txtExportRoot.Text ?? "")
            .With(NotificationPullerConfigParameterKeys.MakeZip, _chkZip.Checked.ToString())
            .With(NotificationPullerConfigParameterKeys.SaveMode, (_cmbSaveMode.SelectedItem?.ToString() ?? "all"))
            .With(NotificationPullerConfigParameterKeys.DeleteOh, _chkDelOh.Checked.ToString())
            .With(NotificationPullerConfigParameterKeys.DeleteAssets, _chkDelAssets.Checked.ToString())
            .With(NotificationPullerConfigParameterKeys.DeleteCustom, _chkDelCustom.Checked.ToString())
            .With(NotificationPullerConfigParameterKeys.DeleteCustomPattern, _txtDelCustomPattern.Text ?? "")
            .With(NotificationPullerConfigParameterKeys.DeleteAll, _chkDelAll.Checked.ToString())
            .With(NotificationPullerConfigParameterKeys.ConfigPath, _txtConfigPath.Text ?? "");

        _ = RunToolAsync(
            tool: tool,
            parameters: p,
            toolLogTag: "notif-puller",
            onCompleted: result =>
            {
                var json = result.TryGetOutput(NotificationPullerConfigTool.OutputKeyJson);
                if (!string.IsNullOrWhiteSpace(json))
                    _txtPreview.Text = json;

                _lblStatus.Text = result.Summary;
                return System.Threading.Tasks.Task.CompletedTask;
            },
            showWarningsOnSuccess: true,
            // leeg => geen success popup (jullie stijl)
            successMessageFactory: _ => "");
    }

    private static TextBox NewTextBox(string? text = null)
    {
        var tb = new TextBox { Dock = DockStyle.Fill };
        if (text != null) tb.Text = text;
        return tb;
    }

    private static TextBox NewPasswordBox()
        => new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };

    private static void AddRow(TableLayoutPanel tlp, string label, Control input)
    {
        var row = tlp.RowCount;
        tlp.RowCount++;
        tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        tlp.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);

        input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        tlp.Controls.Add(input, 1, row);
    }
}
