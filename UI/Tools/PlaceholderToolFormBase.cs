using System.Drawing;
using System.Windows.Forms;
using StruxureGuard.Core.Logging;
using StruxureGuard.Styling;

namespace StruxureGuard.UI.Tools;

/// <summary>
/// Placeholder tool window (compile-ready).
/// Use for tools that are not yet migrated to the runner infrastructure.
/// </summary>
public abstract class PlaceholderToolFormBase : Form
{
    protected PlaceholderToolFormBase(string title, string description, string logTag = "tool")
    {
        Text = title;
        Width = 860;
        Height = 560;
        StartPosition = FormStartPosition.CenterParent;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            RowCount = 3,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        var header = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 14f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(header, 0, 0);

        var body = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            Text = description
        };
        root.Controls.Add(body, 0, 1);

        var close = new Button
        {
            Text = "Close",
            Dock = DockStyle.Right,
            Width = 120
        };
        close.Click += (_, __) => Close();

        var btnRow = new Panel { Dock = DockStyle.Fill };
        btnRow.Controls.Add(close);
        root.Controls.Add(btnRow, 0, 2);

        Load += (_, __) =>
        {
            ThemeManager.ApplyTheme(this);
            Log.Info(logTag, $"{title} opened");
        };
    }
}
