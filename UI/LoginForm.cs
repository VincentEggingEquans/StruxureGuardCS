using System;
using System.Drawing;
using System.Windows.Forms;
using StruxureGuard.Core.Logging;
using StruxureGuard.Core.Security;
using StruxureGuard.Styling;

namespace StruxureGuard.UI;

public sealed class LoginForm : Form
{
    private readonly TextBox _txtPassword;
    private readonly Label _lblLock;
    private readonly Button _btnOk;
    private readonly Button _btnCancel;

    private bool _reveal;

    public LoginForm()
    {
        Text = "StruxureGuard - Wachtwoord";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Width = 420;
        Height = 190;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(14),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lblTitle = new Label
        {
            Text = "Voer wachtwoord in",
            AutoSize = true,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10)
        };

        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Slotje icoon (klik = show/hide), géén bypass
        _lblLock = new Label
        {
            Text = "🔒",
            AutoSize = true,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Emoji", 14f, FontStyle.Regular),
            Margin = new Padding(0, 0, 8, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _lblLock.Click += (_, __) => ToggleReveal();

        _txtPassword = new TextBox
        {
            UseSystemPasswordChar = true,
            Dock = DockStyle.Fill
        };
        _txtPassword.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                TryLogin();
            }
        };

        row.Controls.Add(_lblLock, 0, 0);
        row.Controls.Add(_txtPassword, 1, 0);

        var hint = new Label
        {
            Text = "Tip: klik op het slotje om het wachtwoord te tonen/verbergen.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 8, 0, 0)
        };

        var btnRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0)
        };

        _btnOk = new Button { Text = "OK", Width = 90 };
        _btnOk.Click += (_, __) => TryLogin();

        _btnCancel = new Button { Text = "Annuleren", Width = 90 };
        _btnCancel.Click += (_, __) =>
        {
            Log.Info("auth", "Login canceled by user.");
            DialogResult = DialogResult.Cancel;
            Close();
        };

        btnRow.Controls.Add(_btnOk);
        btnRow.Controls.Add(_btnCancel);

        root.Controls.Add(lblTitle, 0, 0);
        root.Controls.Add(row, 0, 1);
        root.Controls.Add(hint, 0, 2);
        root.Controls.Add(btnRow, 0, 3);

        Controls.Add(root);

        Load += (_, __) => ThemeManager.ApplyTheme(this);
        Shown += (_, __) =>
        {
            Log.Info("auth", "LoginForm shown.");
            _txtPassword.Focus();
        };
    }

    private void ToggleReveal()
    {
        _reveal = !_reveal;
        _txtPassword.UseSystemPasswordChar = !_reveal;
        _lblLock.Text = _reveal ? "🔓" : "🔒";
        Log.Info("auth", $"LoginForm reveal toggled: {_reveal}");
    }

    private void TryLogin()
    {
        var pwd = _txtPassword.Text ?? "";

        var ok = PasswordVerifier.Verify(pwd);

        Log.Info("auth", $"Login attempt: ok={ok} len={pwd.Length}");

        if (ok)
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        MessageBox.Show(this, "Wachtwoord onjuist.", "Login", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        _txtPassword.SelectAll();
        _txtPassword.Focus();
    }
}
