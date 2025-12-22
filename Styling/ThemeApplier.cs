using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Drawing;
using System.Windows.Forms;
using StruxureGuard.UI.Controls;

namespace StruxureGuard.Styling
{
    public static class ThemeApplier
    {
        // We avoid using Tag (might be used by app). ConditionalWeakTable is perfect for "hooked once" state.
        private sealed class HookState { }
        private static readonly ConditionalWeakTable<object, HookState> _hooked = new();

        public static void Apply(Control root, ThemeSettings t)
        {
            if (root is null) return;

            if (root is Form f)
            {
                f.BackColor = t.AppBack;
                f.ForeColor = t.Text;
                f.Font = new Font(t.FontFamily, t.FontSize);

                if (f.MainMenuStrip is not null)
                    ApplyToolStripDeep(f.MainMenuStrip, t);

                if (f.ContextMenuStrip is not null)
                    ApplyToolStripDeep(f.ContextMenuStrip, t);

                // Menus/toolstrips not in Controls tree (misc menus, tray menus, etc.)
                ApplyLooseMenuComponentsOnForm(f, t);
            }

            ApplyOne(root, t);

            // ContextMenuStrip attached to controls
            ApplyContextMenusIfAny(root, t);

            foreach (Control c in root.Controls)
                Apply(c, t);
        }

        public static void ApplyContextMenusIfAny(Control c, ThemeSettings t)
        {
            if (c is null) return;

            if (c.ContextMenuStrip is not null)
                ApplyToolStripDeep(c.ContextMenuStrip, t);

            if (c is ToolStrip ts)
                ApplyToolStripDeep(ts, t);
        }

        // =========================
        // Loose component scanning (menus not in Controls tree)
        // =========================
        private static void ApplyLooseMenuComponentsOnForm(Form f, ThemeSettings t)
        {
            try
            {
                var componentsField = f.GetType().GetField("components",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (componentsField?.GetValue(f) is IContainer container)
                {
                    foreach (var comp in container.Components)
                        ApplyIfMenuLike(comp, t);
                }

                var fields = f.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var field in fields)
                {
                    object? value = null;
                    try { value = field.GetValue(f); } catch { }
                    ApplyIfMenuLike(value, t);
                }
            }
            catch
            {
                // fail-soft
            }
        }

        private static void ApplyIfMenuLike(object? obj, ThemeSettings t)
        {
            if (obj is null) return;

            if (obj is ContextMenuStrip cms)
            {
                ApplyToolStripDeep(cms, t);
                return;
            }

            if (obj is ToolStrip ts)
            {
                ApplyToolStripDeep(ts, t);
                return;
            }

            if (obj is ToolStripDropDown dd)
            {
                ApplyToolStripDropDownDeep(dd, t);
                return;
            }

            if (obj is NotifyIcon ni && ni.ContextMenuStrip is not null)
            {
                ApplyToolStripDeep(ni.ContextMenuStrip, t);
                return;
            }

            if (obj is ToolStripDropDownItem ddi && ddi.DropDown is not null)
            {
                ApplyToolStripDropDownDeep(ddi.DropDown, t);
                return;
            }
        }

        // =========================
        // Per-control theming
        // =========================
        private static void ApplyOne(Control c, ThemeSettings t)
        {
            // defaults
            c.ForeColor = t.Text;

            // SplitContainer panels are separate surfaces and often remain white
            if (c is SplitContainer sc)
            {
                sc.BackColor = t.Surface;
                sc.Panel1.BackColor = t.Surface;
                sc.Panel2.BackColor = t.Surface;
            }

            // Surfaces
            if (c is Panel or GroupBox or TabPage or TableLayoutPanel or FlowLayoutPanel)
                c.BackColor = t.Surface;

            // TabControl surface itself
            if (c is TabControl tc)
                tc.BackColor = t.Surface;

            // PropertyGrid needs explicit theming
            if (c is PropertyGrid pg)
                ApplyPropertyGridTheme(pg, t);

            // Inputs
            if (c is TextBoxBase tb)
            {
                tb.BackColor = t.InputBack;
                tb.ForeColor = t.InputText;
                tb.BorderStyle = BorderStyle.FixedSingle;
            }

            if (c is ComboBox cb)
            {
                cb.BackColor = t.InputBack;
                cb.ForeColor = t.InputText;
            }

            if (c is NumericUpDown nud)
            {
                nud.BackColor = t.InputBack;
                nud.ForeColor = t.InputText;
            }

            // ✅ Buttons (with auto re-apply on EnabledChanged)
            if (c is Button btn)
            {
                ApplyButtonTheme(btn, t);
                HookButton(btn);
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

            // Strips (these are Controls)
            if (c is MenuStrip ms) ApplyToolStripDeep(ms, t);
            if (c is ToolStrip ts) ApplyToolStripDeep(ts, t);
            if (c is StatusStrip ss) ApplyToolStripDeep(ss, t);

            // Custom
            if (c is ThemedProgressBar tp)
            {
                tp.TrackColor = t.ProgressTrack;
                tp.BarColor = t.Accent;
                tp.BorderColor = t.Border;
                tp.TextColor = t.ProgressText;
            }

            // Overrides win at the end
            ApplyOverrideIfAny(c, t);
        }

        // =========================
        // ✅ Button theming (enabled/disabled always correct)
        // =========================
        private static void ApplyButtonTheme(Button btn, ThemeSettings t)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = t.Border;

            // Important: disabled visuals must be unmistakable
            btn.BackColor = btn.Enabled ? t.ButtonBack : t.ButtonDisabledBack;
            btn.ForeColor = btn.Enabled ? t.ButtonText : t.ButtonDisabledText;
        }

        private static void HookButton(Button btn)
        {
            if (btn is null) return;
            if (_hooked.TryGetValue(btn, out _)) return;
            _hooked.Add(btn, new HookState());

            btn.EnabledChanged += (_, __) =>
            {
                if (ThemeManager.Current is { } cur)
                {
                    ApplyButtonTheme(btn, cur);
                    btn.Invalidate();
                }
            };

            // Some controls recreate handles; re-apply when that happens.
            btn.HandleCreated += (_, __) =>
            {
                if (ThemeManager.Current is { } cur)
                    ApplyButtonTheme(btn, cur);
            };
        }

        // =========================
        // PropertyGrid theming (stable + hooks)
        // =========================
        public static void ApplyPropertyGridTheme(PropertyGrid pg, ThemeSettings t)
        {
            if (pg is null) return;

            // Base surfaces
            pg.BackColor = t.Surface;
            pg.ForeColor = t.Text;

            // Right-side grid view
            pg.ViewBackColor = t.InputBack;
            pg.ViewForeColor = t.InputText;

            // Help panel
            pg.HelpBackColor = t.Surface;
            pg.HelpForeColor = t.Text;

            // Lines / separators / categories
            pg.LineColor = t.Border;
            pg.CategoryForeColor = t.Text;
            pg.CategorySplitterColor = t.Border;

            // Commands (top toolbar area)
            pg.CommandsBackColor = t.Surface;
            pg.CommandsForeColor = t.Text;
            pg.CommandsBorderColor = t.Border;

            // Best-effort: theme child controls (internal ToolStrip etc.)
            foreach (Control child in pg.Controls)
            {
                child.BackColor = t.Surface;
                child.ForeColor = t.Text;

                if (child is ToolStrip ts)
                    ApplyToolStripDeep(ts, t);
            }

            // Ensure it stays correct even if PropertyGrid internally refreshes / recreates bits
            HookPropertyGrid(pg);
            pg.Invalidate(true);
        }

        private static void HookPropertyGrid(PropertyGrid pg)
        {
            if (pg is null) return;
            if (_hooked.TryGetValue(pg, out _)) return;
            _hooked.Add(pg, new HookState());

            // Re-apply on moments where PG tends to rebuild UI
            pg.HandleCreated += (_, __) =>
            {
                if (ThemeManager.Current is { } cur)
                    ApplyPropertyGridTheme(pg, cur);
            };
            pg.VisibleChanged += (_, __) =>
            {
                if (!pg.Visible) return;
                if (ThemeManager.Current is { } cur)
                    ApplyPropertyGridTheme(pg, cur);
            };
            pg.SelectedGridItemChanged += (_, __) =>
            {
                if (ThemeManager.Current is { } cur)
                    ApplyPropertyGridTheme(pg, cur);
            };
            pg.PropertyValueChanged += (_, __) =>
            {
                if (ThemeManager.Current is { } cur)
                    ApplyPropertyGridTheme(pg, cur);
            };
        }

        // =========================
        // ToolStrip deep theming + hook-on-open
        // =========================
        public static void ApplyToolStripDeep(ToolStrip ts, ThemeSettings t)
        {
            if (ts is null) return;

            try
            {
                ts.BackColor = t.StripBack;
                ts.ForeColor = t.StripText;
                ts.Renderer = new ThemedToolStripRenderer(t);
            }
            catch { /* fail-soft */ }

            HookToolStrip(ts);

            foreach (ToolStripItem item in ts.Items)
            {
                try
                {
                    item.BackColor = t.StripBack;
                    item.ForeColor = t.StripText;
                }
                catch { }

                if (item is ToolStripComboBox tscb)
                {
                    try
                    {
                        tscb.ComboBox.BackColor = t.InputBack;
                        tscb.ComboBox.ForeColor = t.InputText;
                    }
                    catch { }
                }

                if (item is ToolStripTextBox tstb)
                {
                    try
                    {
                        tstb.BackColor = t.InputBack;
                        tstb.ForeColor = t.InputText;
                    }
                    catch { }
                }

                if (item is ToolStripDropDownItem ddi)
                {
                    HookDropDownItem(ddi);

                    // If dropdown already exists, theme it now
                    if (ddi.DropDown is ToolStripDropDown dd)
                        ApplyToolStripDropDownDeep(dd, t);
                }
            }

            ts.Invalidate(true);
        }

        private static void ApplyToolStripDropDownDeep(ToolStripDropDown dd, ThemeSettings t)
        {
            if (dd is null) return;

            try
            {
                dd.BackColor = t.StripBack;
                dd.ForeColor = t.StripText;
                dd.Renderer = new ThemedToolStripRenderer(t);
            }
            catch { }

            HookDropDown(dd);

            foreach (ToolStripItem sub in dd.Items)
            {
                try
                {
                    sub.BackColor = t.StripBack;
                    sub.ForeColor = t.StripText;
                }
                catch { }

                if (sub is ToolStripComboBox tscb)
                {
                    try
                    {
                        tscb.ComboBox.BackColor = t.InputBack;
                        tscb.ComboBox.ForeColor = t.InputText;
                    }
                    catch { }
                }

                if (sub is ToolStripTextBox tstb)
                {
                    try
                    {
                        tstb.BackColor = t.InputBack;
                        tstb.ForeColor = t.InputText;
                    }
                    catch { }
                }

                if (sub is ToolStripDropDownItem subDdi)
                {
                    HookDropDownItem(subDdi);

                    if (subDdi.DropDown is ToolStripDropDown subDd)
                        ApplyToolStripDropDownDeep(subDd, t);
                }
            }

            dd.Invalidate(true);
        }

        private static void HookToolStrip(ToolStrip ts)
        {
            if (_hooked.TryGetValue(ts, out _)) return;
            _hooked.Add(ts, new HookState());

            // If a strip becomes visible later or recreates handle
            ts.HandleCreated += (_, __) =>
            {
                if (ThemeManager.Current is { } cur)
                    ApplyToolStripDeep(ts, cur);
            };
            ts.VisibleChanged += (_, __) =>
            {
                if (!ts.Visible) return;
                if (ThemeManager.Current is { } cur)
                    ApplyToolStripDeep(ts, cur);
            };
        }

        private static void HookDropDown(ToolStripDropDown dd)
        {
            if (_hooked.TryGetValue(dd, out _)) return;
            _hooked.Add(dd, new HookState());

            // Critical: right before showing, ensure current theme is applied
            dd.Opening += (_, __) =>
            {
                if (ThemeManager.Current is { } cur)
                    ApplyToolStripDropDownDeep(dd, cur);
            };

            dd.HandleCreated += (_, __) =>
            {
                if (ThemeManager.Current is { } cur)
                    ApplyToolStripDropDownDeep(dd, cur);
            };
        }

        private static void HookDropDownItem(ToolStripDropDownItem ddi)
        {
            if (_hooked.TryGetValue(ddi, out _)) return;
            _hooked.Add(ddi, new HookState());

            ddi.DropDownOpening += (_, __) =>
            {
                if (ddi.DropDown is not ToolStripDropDown dd) return;
                if (ThemeManager.Current is { } cur)
                    ApplyToolStripDropDownDeep(dd, cur);
            };
        }

        // =========================
        // Overrides
        // =========================
        private static void ApplyOverrideIfAny(Control c, ThemeSettings t)
        {
            var keyPath = ThemeKey.ForControl(c);
            ThemeSettings.ControlOverride? ov = null;

            if (!string.IsNullOrWhiteSpace(keyPath) && t.Overrides.TryGetValue(keyPath, out var byPath))
                ov = byPath;
            else if (!string.IsNullOrWhiteSpace(c.Name) && t.Overrides.TryGetValue(c.Name, out var byName))
                ov = byName;

            if (ov is null) return;

            if (ov.BackColor.HasValue) c.BackColor = ov.BackColor.Value;
            if (ov.ForeColor.HasValue) c.ForeColor = ov.ForeColor.Value;

            if (!string.IsNullOrWhiteSpace(ov.FontFamily) || ov.FontSize.HasValue)
            {
                var family = !string.IsNullOrWhiteSpace(ov.FontFamily) ? ov.FontFamily! : c.Font.FontFamily.Name;
                var size = ov.FontSize ?? c.Font.Size;
                c.Font = new Font(family, size, c.Font.Style);
            }

            if (c is Button btn)
            {
                if (ov.BorderColor.HasValue) btn.FlatAppearance.BorderColor = ov.BorderColor.Value;

                // Respect enabled/disabled - but overrides can still win
                if (ov.ButtonBack.HasValue) btn.BackColor = ov.ButtonBack.Value;
                if (ov.ButtonText.HasValue) btn.ForeColor = ov.ButtonText.Value;
            }

            if (c is ThemedProgressBar tp)
            {
                if (ov.ProgressTrack.HasValue) tp.TrackColor = ov.ProgressTrack.Value;
                if (ov.ProgressBar.HasValue) tp.BarColor = ov.ProgressBar.Value;
                if (ov.ProgressText.HasValue) tp.TextColor = ov.ProgressText.Value;
            }
        }
    }
}
