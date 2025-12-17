using System.Drawing;
using System.Windows.Forms;
using StruxureGuard.UI.Controls;
using StruxureGuard.Core.Logging;

namespace StruxureGuard.Styling
{
    public static class ThemeApplier
    {
        public static void Apply(Control root, ThemeSettings t)
        {
            if (root is null) return;

            if (root is Form f)
            {
                f.BackColor = t.AppBack;
                f.ForeColor = t.Text;
                f.Font = new Font(t.FontFamily, t.FontSize);
            }

            ApplyOne(root, t);

            foreach (Control c in root.Controls)
                Apply(c, t);
        }

        private static void ApplyOne(Control c, ThemeSettings t)
        {
            c.ForeColor = t.Text;

            // Surfaces
            if (c is Panel or GroupBox or TabPage or SplitContainer or TableLayoutPanel or FlowLayoutPanel)
                c.BackColor = t.Surface;

            // Inputs
            if (c is TextBoxBase tb)
            {
                tb.BackColor = t.InputBack;
                tb.ForeColor = t.InputText;
                tb.BorderStyle = BorderStyle.FixedSingle;
            }

            if (c is ComboBox cb)
            {
                // 🔥 critical: don't touch styling while dropdown is open
                if (cb.DroppedDown)
                {
                    Log.Warn("theme.apply", $"ComboBox styling skipped (DroppedDown=true). Name='{cb.Name}' Text='{cb.Text}'");
                    return;
                }

                cb.BackColor = t.InputBack;
                cb.ForeColor = t.InputText;
            }

            if (c is NumericUpDown nud)
            {
                nud.BackColor = t.InputBack;
                nud.ForeColor = t.InputText;
            }

            // Buttons
            if (c is Button btn)
            {
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderColor = t.Border;
                btn.BackColor = btn.Enabled ? t.ButtonBack : t.ButtonDisabledBack;
                btn.ForeColor = btn.Enabled ? t.ButtonText : t.ButtonDisabledText;
            }

            // Lists
            if (c is ListBox lb)
            {
                lb.BackColor = t.ListBack;
                lb.ForeColor = t.ListText;
            }

            if (c is CheckedListBox clb)
            {
                clb.BackColor = t.ListBack;
                clb.ForeColor = t.ListText;
            }

            if (c is ListView lv)
            {
                lv.BackColor = t.ListBack;
                lv.ForeColor = t.ListText;
            }

            if (c is TreeView tv)
            {
                tv.BackColor = t.ListBack;
                tv.ForeColor = t.ListText;
            }

            if (c is DataGridView dgv)
            {
                dgv.BackgroundColor = t.Surface;
                dgv.GridColor = t.Border;
                dgv.EnableHeadersVisualStyles = false;

                dgv.ColumnHeadersDefaultCellStyle.BackColor = t.StripBack;
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = t.StripText;

                dgv.DefaultCellStyle.BackColor = t.ListBack;
                dgv.DefaultCellStyle.ForeColor = t.ListText;
                dgv.DefaultCellStyle.SelectionBackColor = t.SelectionBack;
                dgv.DefaultCellStyle.SelectionForeColor = t.SelectionText;
            }

            // Strips
            if (c is MenuStrip ms)
            {
                ms.BackColor = t.StripBack;
                ms.ForeColor = t.StripText;
                ms.Renderer = new ThemedToolStripRenderer(t);
            }
            if (c is ToolStrip ts)
            {
                ts.BackColor = t.StripBack;
                ts.ForeColor = t.StripText;
                ts.Renderer = new ThemedToolStripRenderer(t);
            }
            if (c is StatusStrip ss)
            {
                ss.BackColor = t.StripBack;
                ss.ForeColor = t.StripText;
                ss.Renderer = new ThemedToolStripRenderer(t);
            }

            // Custom
            if (c is ThemedProgressBar tp)
            {
                tp.TrackColor = t.ProgressTrack;
                tp.BarColor = t.Accent;
                tp.BorderColor = t.Border;
                tp.TextColor = t.ProgressText;
            }
        }
    }
}
