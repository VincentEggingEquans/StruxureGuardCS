using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using StruxureGuard.Styling;
using StruxureGuard.UI.Controls;

namespace StruxureGuard.UI.DevTools
{
    public sealed class UiStyleToolForm : Form
    {
        private readonly ToolStrip _strip;
        private readonly ToolStripLabel _lblPreset;
        private readonly ToolStripComboBox _presetCombo;
        private readonly ToolStripButton _btnSaveCustom;
        private readonly ToolStripButton _btnResetToPreset;
        private readonly ToolStripButton _btnExportToCs;

        private readonly ToolStripSeparator _sep1;

        // Startup theme policy (startupTheme.json)
        private readonly ToolStripDropDownButton _btnStartup;
        private readonly ToolStripMenuItem _miForceStartupPreset;
        private readonly ToolStripLabel _lblStartupPreset;
        private readonly ToolStripComboBox _startupPresetCombo;
        private readonly ToolStripMenuItem _miUseCurrentAsStartup;
        private readonly ToolStripMenuItem _miSaveStartup;


        private readonly SplitContainer _mainSplit;      // left editor / right preview
        private readonly SplitContainer _rightSplit;     // preview / overrides
        private readonly PropertyGrid _themeGrid;
        private readonly TabControl _tabs;

        // Overrides UI
        private readonly Label _selectedLabel;
        private readonly TextBox _selectedKeyBox;
        private readonly Button _btnCopyKey;
        private readonly PropertyGrid _overrideGrid;
        private readonly Button _btnOverrideEnable;
        private readonly Button _btnOverrideRemove;
        private readonly Button _btnOverrideApply;

        private ThemeSettings _workingTheme = null!;
        private string? _currentBasePresetName;
        private Control? _selectedControl;
        private string? _selectedKeyPath;

        private bool _loadingPresets;

        // Icons / imagelists for preview
        private readonly ImageList _smallImages;
        private readonly ImageList _largeImages;

        // Components for preview
        private readonly ToolTip _toolTip;
        private readonly ErrorProvider _errorProvider;
        private readonly NotifyIcon _notifyIcon;

        // Demo progress timer
        private readonly System.Windows.Forms.Timer _timer;
        private ThemedProgressBar _themedProgressA = null!;
        private ThemedProgressBar _themedProgressB = null!;
        private ProgressBar _winProgress = null!;
        private int _progressDir = 1;

        public UiStyleToolForm()
        {
            Text = "UI Style Tool";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1400;
            Height = 900;

            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            ControlBox = true;

            KeyPreview = true;
            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    Close();
                    e.Handled = true;
                }
            };

            _smallImages = BuildImageList(16);
            _largeImages = BuildImageList(32);

            _toolTip = new ToolTip
            {
                AutoPopDelay = 8000,
                InitialDelay = 300,
                ReshowDelay = 100,
                ShowAlways = true
            };

            _errorProvider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };

            _notifyIcon = new NotifyIcon
            {
                Visible = true,
                Icon = SystemIcons.Application,
                Text = "StruxureGuard UI Style Tool (Dev)"
            };
            _notifyIcon.DoubleClick += (_, __) =>
            {
                WindowState = FormWindowState.Normal;
                BringToFront();
                Activate();
            };

            // Toolstrip
            _strip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };

            _lblPreset = new ToolStripLabel("Preset:");
            _presetCombo = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
            _presetCombo.SelectedIndexChanged += (_, __) =>
            {
                if (_loadingPresets) return;
                if (_presetCombo.SelectedItem is string name)
                    LoadPresetIntoWorking(name);
            };

            _btnSaveCustom = new ToolStripButton("Save as Custom…");
            _btnSaveCustom.Click += (_, __) => SaveAsCustom();

            _btnResetToPreset = new ToolStripButton("Reset to Preset");
            _btnResetToPreset.Click += (_, __) =>
            {
                if (!string.IsNullOrWhiteSpace(_currentBasePresetName))
                    LoadPresetIntoWorking(_currentBasePresetName!);
            };

            _btnExportToCs = new ToolStripButton("Export → C# Preset…");
            _btnExportToCs.Click += (_, __) => ExportToCsPreset();

            // Startup default theme config (startupTheme.json)
            _btnStartup = new ToolStripDropDownButton("Startup");
            _miForceStartupPreset = new ToolStripMenuItem("Force startup preset (ignore theme.json)")
            {
                CheckOnClick = true
            };

            _lblStartupPreset = new ToolStripLabel("Default preset:");
            _startupPresetCombo = new ToolStripComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 220
            };

            _miUseCurrentAsStartup = new ToolStripMenuItem("Use current theme name as default preset");
            _miUseCurrentAsStartup.Click += (_, __) =>
            {
                // Convenience: assumes the current theme name corresponds to a registered preset/custom preset
                var name = _workingTheme?.Name;
                if (string.IsNullOrWhiteSpace(name)) return;
                _startupPresetCombo.SelectedItem = name;
                if (_startupPresetCombo.SelectedItem is null)
                {
                    // If not in list, add it (custom preset names are also valid if registered)
                    _startupPresetCombo.Items.Add(name);
                    _startupPresetCombo.SelectedItem = name;
                }
            };

            _miSaveStartup = new ToolStripMenuItem("Save startup settings");
            _miSaveStartup.Click += (_, __) => SaveStartupSettingsFromUi();

            _btnStartup.DropDownOpening += (_, __) => SyncStartupUiFromConfig();

            _btnStartup.DropDownItems.AddRange(new ToolStripItem[]
            {
                _miForceStartupPreset,
                new ToolStripSeparator(),
                _lblStartupPreset,
                _startupPresetCombo,
                new ToolStripSeparator(),
                _miUseCurrentAsStartup,
                _miSaveStartup
            });

            _sep1 = new ToolStripSeparator();

            _strip.Items.AddRange(new ToolStripItem[]
            {
                _lblPreset,
                _presetCombo,
                _sep1,
                _btnSaveCustom,
                _btnResetToPreset,
                new ToolStripSeparator(),
                _btnExportToCs
            });

            // Root split containers
            _mainSplit = new SplitContainer { Dock = DockStyle.Fill };
            _rightSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };

            // Left: theme editor
            _themeGrid = new PropertyGrid
            {
                Dock = DockStyle.Fill,
                ToolbarVisible = false,
                HelpVisible = true
            };
            _themeGrid.PropertyValueChanged += (_, __) => ApplyWorkingTheme();
            _mainSplit.Panel1.Controls.Add(_themeGrid);

            // Right: preview tabs
            _tabs = new TabControl { Dock = DockStyle.Fill };
            _tabs.TabPages.Add(BuildOverviewTab());
            _tabs.TabPages.Add(BuildButtonsTab());
            _tabs.TabPages.Add(BuildInputsTab());
            _tabs.TabPages.Add(BuildTextAndRichTab());
            _tabs.TabPages.Add(BuildListsTab());
            _tabs.TabPages.Add(BuildTreesAndIconsTab());
            _tabs.TabPages.Add(BuildDataGridTab());
            _tabs.TabPages.Add(BuildContainersLayoutTab());
            _tabs.TabPages.Add(BuildStripsMenusStatusTab());
            _tabs.TabPages.Add(BuildPickersAndSlidersTab());
            _tabs.TabPages.Add(BuildProgressAndTimersTab(out _themedProgressA, out _themedProgressB, out _winProgress));
            _tabs.TabPages.Add(BuildDialogsAndComponentsTab());

            // Overrides panel bottom
            var overrideRoot = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };

            _selectedLabel = new Label
            {
                Text = "Selected: (click a control in the preview)",
                Dock = DockStyle.Top,
                Height = 44
            };

            var keyRow = new Panel { Dock = DockStyle.Top, Height = 34 };
            var keyLbl = new Label { Text = "ThemeKey:", AutoSize = true, Left = 0, Top = 8 };
            _selectedKeyBox = new TextBox
            {
                Left = 76,
                Top = 5,
                Width = 820,
                ReadOnly = true,
                Name = "txtSelectedThemeKey"
            };
            _btnCopyKey = new Button
            {
                Text = "Copy",
                Left = _selectedKeyBox.Right + 8,
                Top = 4,
                Width = 80,
                Height = 26
            };
            _btnCopyKey.Click += (_, __) =>
            {
                if (!string.IsNullOrWhiteSpace(_selectedKeyBox.Text))
                    Clipboard.SetText(_selectedKeyBox.Text);
            };
            keyRow.Controls.Add(keyLbl);
            keyRow.Controls.Add(_selectedKeyBox);
            keyRow.Controls.Add(_btnCopyKey);

            var btnRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 38,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            _btnOverrideEnable = new Button { Text = "Create/Enable Override (ThemeKey)", Width = 260, Height = 30 };
            _btnOverrideRemove = new Button { Text = "Remove Override", Width = 150, Height = 30 };
            _btnOverrideApply = new Button { Text = "Apply Now", Width = 120, Height = 30 };

            _btnOverrideEnable.Click += (_, __) => EnsureOverrideForSelected();
            _btnOverrideRemove.Click += (_, __) => RemoveOverrideForSelected();
            _btnOverrideApply.Click += (_, __) => ApplyWorkingTheme();

            btnRow.Controls.Add(_btnOverrideEnable);
            btnRow.Controls.Add(_btnOverrideRemove);
            btnRow.Controls.Add(_btnOverrideApply);

            _overrideGrid = new PropertyGrid
            {
                Dock = DockStyle.Fill,
                ToolbarVisible = false,
                HelpVisible = true
            };
            _overrideGrid.PropertyValueChanged += (_, __) => ApplyWorkingTheme();

            overrideRoot.Controls.Add(_overrideGrid);
            overrideRoot.Controls.Add(btnRow);
            overrideRoot.Controls.Add(keyRow);
            overrideRoot.Controls.Add(_selectedLabel);

            _rightSplit.Panel1.Controls.Add(_tabs);
            _rightSplit.Panel2.Controls.Add(overrideRoot);

            _mainSplit.Panel2.Controls.Add(_rightSplit);

            Controls.Add(_mainSplit);
            Controls.Add(_strip);

            _timer = new System.Windows.Forms.Timer { Interval = 60 };
            _timer.Tick += (_, __) =>
            {
                if (_themedProgressA is not null)
                    _themedProgressA.Value = (_themedProgressA.Value + 1) % 101;
                if (_themedProgressB is not null)
                    _themedProgressB.Value = (_themedProgressB.Value + 2) % 101;

                if (_winProgress is not null)
                {
                    var v = _winProgress.Value + _progressDir * 2;
                    if (v >= _winProgress.Maximum) { v = _winProgress.Maximum; _progressDir = -1; }
                    if (v <= _winProgress.Minimum) { v = _winProgress.Minimum; _progressDir = 1; }
                    _winProgress.Value = v;
                }
            };

            Shown += (_, __) =>
            {
                ApplyRootSplitDefaults();
                HookPreviewClicks();
                _timer.Start();

                // Extra: force a full re-theme after handle creation to avoid “white” leftovers
                ApplyWorkingTheme();
            };
            Resize += (_, __) => ApplyRootSplitDefaults(clampOnly: true);

            Load += (_, __) =>
            {
                LoadPresets();

                var startName = ThemeManager.Current?.Name;
                if (!string.IsNullOrWhiteSpace(startName) && ThemeManager.GetPreset(startName!) != null)
                    LoadPresetIntoWorking(startName!);
                else if (_presetCombo.Items.Count > 0)
                    LoadPresetIntoWorking(_presetCombo.Items[0]!.ToString()!);
                else
                    _workingTheme = (ThemeManager.Current?.Clone()) ?? new ThemeSettings { Name = "Custom" };

                _themeGrid.SelectedObject = _workingTheme;

                ApplyWorkingTheme();
            };

            FormClosing += (_, __) =>
            {
                ThemeManager.SaveCurrent();
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            };
        }

        // =========================
        // Root SplitContainer safe setup
        // =========================
        private void ApplyRootSplitDefaults(bool clampOnly = false)
        {
            ApplyMinSizesSafe(_mainSplit, 340, 560);
            ApplyMinSizesSafe(_rightSplit, 320, 240);

            SetSplitterDistanceSafe(_mainSplit, desired: 460);

            var desiredTop = Math.Max(_rightSplit.Panel1MinSize, _rightSplit.Height - 280);
            SetSplitterDistanceSafe(_rightSplit, desired: desiredTop);
        }

        private static void ApplyMinSizesSafe(SplitContainer sc, int panel1Min, int panel2Min)
        {
            if (sc is null) return;

            int total = sc.Orientation == Orientation.Vertical ? sc.Width : sc.Height;
            if (total <= 0) return;

            panel1Min = Math.Max(0, panel1Min);
            panel2Min = Math.Max(0, panel2Min);

            if (panel1Min + panel2Min > total)
            {
                int half = total / 2;
                panel1Min = Math.Min(panel1Min, half);
                panel2Min = Math.Min(panel2Min, total - panel1Min);
            }

            try
            {
                sc.Panel1MinSize = panel1Min;
                sc.Panel2MinSize = panel2Min;
            }
            catch { }
        }

        private static void SetSplitterDistanceSafe(SplitContainer sc, int desired)
        {
            if (sc is null) return;

            int total = sc.Orientation == Orientation.Vertical ? sc.Width : sc.Height;
            if (total <= 0) return;

            int min = sc.Panel1MinSize;
            int max = total - sc.Panel2MinSize;
            if (max < min) return;

            int clamped = Math.Max(min, Math.Min(desired, max));

            try
            {
                if (sc.SplitterDistance != clamped)
                    sc.SplitterDistance = clamped;
            }
            catch { }
        }

        private static SplitContainer CreateSafeSplitContainer(
            Orientation orientation,
            DockStyle dock,
            int panel1Min,
            int panel2Min,
            int desiredSplitterDistance,
            int? fixedHeight = null)
        {
            var sc = new SplitContainer
            {
                Orientation = orientation,
                Dock = dock
            };
            if (fixedHeight.HasValue) sc.Height = fixedHeight.Value;

            void Apply()
            {
                try
                {
                    int total = orientation == Orientation.Vertical ? sc.Width : sc.Height;
                    if (total <= 0) return;

                    int p1 = panel1Min;
                    int p2 = panel2Min;

                    if (p1 + p2 > total)
                    {
                        int half = total / 2;
                        p1 = Math.Min(p1, half);
                        p2 = Math.Min(p2, total - p1);
                    }

                    sc.Panel1MinSize = p1;
                    sc.Panel2MinSize = p2;

                    int min = sc.Panel1MinSize;
                    int max = total - sc.Panel2MinSize;
                    if (max < min) return;

                    int desired = Math.Max(min, Math.Min(desiredSplitterDistance, max));
                    if (sc.SplitterDistance != desired)
                        sc.SplitterDistance = desired;
                }
                catch { }
            }

            sc.HandleCreated += (_, __) => Apply();
            sc.SizeChanged += (_, __) => Apply();

            return sc;
        }

        // =========================
        // Presets
        // =========================
        private void LoadPresets()
        {
            _loadingPresets = true;
            try
            {
                _presetCombo.Items.Clear();
                foreach (var n in ThemeManager.GetPresetNames())
                    _presetCombo.Items.Add(n);

                if (_presetCombo.Items.Count > 0)
                    _presetCombo.SelectedIndex = 0;
            }
            finally
            {
                _loadingPresets = false;
            }
        }

        private void LoadPresetIntoWorking(string presetName)
        {
            var preset = ThemeManager.GetPresetCopy(presetName);
            if (preset is null) return;

            _currentBasePresetName = presetName;
            _workingTheme = preset;

            _themeGrid.SelectedObject = _workingTheme;
            ApplyWorkingTheme();

            ReselectIfPossible();
        }

        private void ApplyWorkingTheme()
        {
            ThemeManager.ApplyThemeSettings(_workingTheme, persist: true);

            // Apply theme to this tool + force refresh of “non-control-tree” surfaces
            ThemeManager.ApplyTheme(this);

            // PropertyGrid is notoriously special: theme it explicitly
            ApplyPropertyGridTheme(_themeGrid, _workingTheme);
            ApplyPropertyGridTheme(_overrideGrid, _workingTheme);

            // Theme any PropertyGrid in preview tabs (like pgMisc) and also re-theme strips/menus/context menus
            ApplyExtrasAcrossPreviewTree(_workingTheme);

            // Re-select (so override grid stays relevant after changes)
            ReselectIfPossible();
        }

        private void SaveAsCustom()
        {
            var name = Prompt.Show("Save as Custom", "Name:", _workingTheme.Name);
            if (string.IsNullOrWhiteSpace(name)) return;

            _workingTheme.Name = name.Trim();

            // Save current working theme as custom preset and persist
            ThemeManager.ApplyThemeSettings(_workingTheme, persist: true);
            ThemeManager.SaveCurrentAsCustomPreset(_workingTheme.Name);

            LoadPresets();
            var idx = _presetCombo.Items.IndexOf(_workingTheme.Name);
            if (idx >= 0) _presetCombo.SelectedIndex = idx;

            ApplyWorkingTheme();
        }

        private void ExportToCsPreset()
        {
            var theme = _workingTheme;
            if (theme is null) return;

            var className = Prompt.Show("Export to C# preset", "Class name:", theme.Name.Replace(" ", ""));
            if (string.IsNullOrWhiteSpace(className)) return;

            var include = MessageBox.Show(
                this,
                "Include per-control Overrides (ThemeKey overrides) in the generated C# preset?" +
                "Yes = include overrides No = base palette only Cancel = abort export",
                "Export options",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (include == DialogResult.Cancel) return;

            bool includeOverrides = include == DialogResult.Yes;

            var code = ThemePresetExporter.ToPresetClass(
                theme,
                className.Trim(),
                presetNameOverride: theme.Name,
                includeOverrides: includeOverrides);

            using var sfd = new SaveFileDialog
            {
                Title = "Save preset .cs",
                Filter = "C# file (*.cs)|*.cs|All files (*.*)|*.*",
                FileName = className.Trim() + ".cs"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            File.WriteAllText(sfd.FileName, code);

            try { Clipboard.SetText(code); } catch { }

            MessageBox.Show(this, "Preset exported.\r\n(Also copied to clipboard.)", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }


        // =========================
        // Startup theme policy (startupTheme.json)
        // =========================
        private void SyncStartupUiFromConfig()
        {
            // Populate dropdown items (presets + custom presets registered in ThemeManager)
            _startupPresetCombo.Items.Clear();
            foreach (var n in ThemeManager.GetPresetNames())
                _startupPresetCombo.Items.Add(n);

            // Ensure current config name exists in list
            var cfg = ThemeManager.StartupConfig;
            _miForceStartupPreset.Checked = cfg.ForceDefaultPreset;

            if (!string.IsNullOrWhiteSpace(cfg.DefaultPresetName))
            {
                var idx = _startupPresetCombo.Items.IndexOf(cfg.DefaultPresetName);
                if (idx >= 0) _startupPresetCombo.SelectedIndex = idx;
                else
                {
                    _startupPresetCombo.Items.Add(cfg.DefaultPresetName);
                    _startupPresetCombo.SelectedItem = cfg.DefaultPresetName;
                }
            }
            else if (_startupPresetCombo.Items.Count > 0)
            {
                _startupPresetCombo.SelectedIndex = 0;
            }
        }

        private void SaveStartupSettingsFromUi()
        {
            var chosen = _startupPresetCombo.SelectedItem as string
                         ?? _startupPresetCombo.Text
                         ?? "";

            chosen = chosen.Trim();

            if (string.IsNullOrWhiteSpace(chosen))
            {
                MessageBox.Show(this, "Choose a preset name first.", "Startup settings",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Only allow names that exist as presets (including custom presets registered)
            if (ThemeManager.GetPreset(chosen) is null)
            {
                MessageBox.Show(this,
                    $"Preset '{chosen}' is not registered." +
                    "Tip: save your theme as Custom first, then pick it here.",
                    "Startup settings",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            ThemeManager.StartupConfig.ForceDefaultPreset = _miForceStartupPreset.Checked;
            ThemeManager.StartupConfig.DefaultPresetName = chosen;
            ThemeManager.SaveStartupConfig();

            MessageBox.Show(this,
                $"Saved.ForceDefaultPreset: {ThemeManager.StartupConfig.ForceDefaultPreset}" +
                $"DefaultPresetName: {ThemeManager.StartupConfig.DefaultPresetName}",
                "Startup settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // =========================
        // FIX: “white areas” + menus not updating
        // =========================
        private void ApplyExtrasAcrossPreviewTree(ThemeSettings t)
        {
            // Theme all property grids in the preview (e.g. pgMisc)
            foreach (var pg in EnumerateControls(this).OfType<PropertyGrid>())
                ApplyPropertyGridTheme(pg, t);

            // Re-apply renderer to *all* toolstrips + their dropdowns
            foreach (var ts in EnumerateControls(this).OfType<ToolStrip>())
                ApplyToolStripDeep(ts, t);

            // ContextMenuStrip is not always in Controls; we must pick them off from ContextMenuStrip property
            foreach (var c in EnumerateControls(this))
            {
                if (c.ContextMenuStrip is ContextMenuStrip cms)
                    ApplyContextMenuDeep(cms, t);
            }

            // Also: tabs/pages sometimes need explicit back fill after theme changes
            foreach (var page in _tabs.TabPages.Cast<TabPage>())
                page.Invalidate(true);

            Invalidate(true);
            Refresh();
        }

        private static void ApplyPropertyGridTheme(PropertyGrid pg, ThemeSettings t)
        {
            if (pg is null) return;

            try
            {
                pg.BackColor = t.Surface;
                pg.ForeColor = t.Text;

                // View area (right side)
                pg.ViewBackColor = t.InputBack;
                pg.ViewForeColor = t.InputText;

                // Help panel (bottom)
                pg.HelpBackColor = t.Surface;
                pg.HelpForeColor = t.Text;

                // Category list / left side styling
                pg.CategoryForeColor = t.Text;
                pg.CategorySplitterColor = t.Border;

                // Lines / separators
                pg.LineColor = t.Border;

                // Selection colors (these exist in newer WinForms; wrap in try)
                try { pg.SelectedItemWithFocusBackColor = t.SelectionBack; } catch { }
                try { pg.SelectedItemWithFocusForeColor = t.SelectionText; } catch { }

                // Force repaint
                pg.Invalidate(true);
                pg.Refresh();
            }
            catch
            {
                // fail-soft
            }
        }

        private static void ApplyToolStripDeep(ToolStrip ts, ThemeSettings t)
        {
            if (ts is null) return;

            try
            {
                ts.BackColor = t.StripBack;
                ts.ForeColor = t.StripText;
                ts.Renderer = new ThemedToolStripRenderer(t);
                ts.Invalidate(true);
            }
            catch { }

            // Deep-apply dropdowns for items
            foreach (ToolStripItem item in ts.Items)
            {
                if (item is ToolStripDropDownItem ddi)
                {
                    if (ddi.DropDown is ToolStripDropDown dd)
                    {
                        ApplyToolStripDropDownDeep(dd, t);
                    }
                }
            }
        }

        private static void ApplyToolStripDropDownDeep(ToolStripDropDown dd, ThemeSettings t)
        {
            if (dd is null) return;

            try
            {
                dd.BackColor = t.StripBack;
                dd.ForeColor = t.StripText;
                dd.Renderer = new ThemedToolStripRenderer(t);
                dd.Invalidate(true);
            }
            catch { }

            foreach (ToolStripItem sub in dd.Items)
            {
                if (sub is ToolStripDropDownItem subDdi)
                {
                    if (subDdi.DropDown is ToolStripDropDown subDd)
                        ApplyToolStripDropDownDeep(subDd, t);
                }
            }
        }

        private static void ApplyContextMenuDeep(ContextMenuStrip cms, ThemeSettings t)
        {
            if (cms is null) return;

            try
            {
                cms.BackColor = t.StripBack;
                cms.ForeColor = t.StripText;
                cms.Renderer = new ThemedToolStripRenderer(t);
                cms.Invalidate(true);
            }
            catch { }

            foreach (ToolStripItem item in cms.Items)
            {
                if (item is ToolStripDropDownItem ddi && ddi.DropDown is ToolStripDropDown dd)
                    ApplyToolStripDropDownDeep(dd, t);
            }
        }

        // =========================
        // Click-to-select control in preview (ThemeKey-first)
        // =========================
        private void HookPreviewClicks()
        {
            foreach (var page in _tabs.TabPages.Cast<TabPage>())
            {
                foreach (var c in EnumerateControls(page))
                {
                    c.MouseDown -= OnPreviewMouseDown;
                    c.MouseDown += OnPreviewMouseDown;

                    if (c is DataGridView dgv)
                    {
                        dgv.CellMouseDown -= OnGridCellMouseDown;
                        dgv.CellMouseDown += OnGridCellMouseDown;
                    }

                    if (c is ToolStrip ts)
                    {
                        ts.ItemClicked -= OnToolStripItemClicked;
                        ts.ItemClicked += OnToolStripItemClicked;
                    }
                }
            }
        }

        private void OnGridCellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (sender is DataGridView dgv)
                SelectControl(dgv);
        }

        private void OnToolStripItemClicked(object? sender, ToolStripItemClickedEventArgs e)
        {
            if (sender is ToolStrip ts)
                SelectControl(ts);
        }

        private void OnPreviewMouseDown(object? sender, MouseEventArgs e)
        {
            if (sender is Control c)
                SelectControl(c);
        }

        private void SelectControl(Control c)
        {
            _selectedControl = c;

            _selectedKeyPath = ThemeKey.ForControl(c);
            _selectedKeyBox.Text = _selectedKeyPath ?? "";

            var name = string.IsNullOrWhiteSpace(c.Name) ? "(no Name)" : c.Name;
            _selectedLabel.Text = $"Selected: {c.GetType().Name} • Name='{name}'";

            if (!string.IsNullOrWhiteSpace(_selectedKeyPath) &&
                _workingTheme.Overrides.TryGetValue(_selectedKeyPath, out var ovByKey))
            {
                _overrideGrid.SelectedObject = ovByKey;
                return;
            }

            if (!string.IsNullOrWhiteSpace(c.Name) &&
                _workingTheme.Overrides.TryGetValue(c.Name, out var ovByName))
            {
                _overrideGrid.SelectedObject = ovByName;
                return;
            }

            _overrideGrid.SelectedObject = null;
        }

        private void EnsureOverrideForSelected()
        {
            if (_selectedControl is null) return;

            var key = _selectedKeyPath ?? ThemeKey.ForControl(_selectedControl);
            _selectedKeyPath = key;
            _selectedKeyBox.Text = key ?? "";

            if (string.IsNullOrWhiteSpace(key)) return;

            if (!_workingTheme.Overrides.TryGetValue(key, out var ov))
            {
                ov = new ThemeSettings.ControlOverride();
                _workingTheme.Overrides[key] = ov;
            }

            _overrideGrid.SelectedObject = ov;
            ApplyWorkingTheme();
        }

        private void RemoveOverrideForSelected()
        {
            if (_selectedControl is null) return;

            var key = _selectedKeyPath ?? ThemeKey.ForControl(_selectedControl);

            var removed = false;

            if (!string.IsNullOrWhiteSpace(key))
                removed |= _workingTheme.Overrides.Remove(key);

            if (!string.IsNullOrWhiteSpace(_selectedControl.Name))
                removed |= _workingTheme.Overrides.Remove(_selectedControl.Name);

            if (removed)
            {
                _overrideGrid.SelectedObject = null;
                ApplyWorkingTheme();
            }
        }

        private void ReselectIfPossible()
        {
            if (_selectedControl is null) return;
            SelectControl(_selectedControl);
        }

        private static IEnumerable<Control> EnumerateControls(Control root)
        {
            foreach (Control c in root.Controls)
            {
                yield return c;
                foreach (var child in EnumerateControls(c))
                    yield return child;
            }
        }

        // =========================
        // Tabs (extensive playground)
        // =========================

        private TabPage BuildOverviewTab()
        {
            var page = new TabPage("Overview");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };
            page.Controls.Add(root);

            // Add a PropertyGrid on the left side to test its theming
            var split = CreateSafeSplitContainer(Orientation.Vertical, DockStyle.Fill, 360, 600, 420);
            root.Controls.Add(split);

            var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            split.Panel1.Controls.Add(left);

            // "Misc" property grid (the area that used to stay white)
            var misc = new PropertyGrid
            {
                Name = "pgMisc",
                Dock = DockStyle.Fill,
                SelectedObject = new DemoMiscObject()
            };
            left.Controls.Add(misc);

            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), AutoScroll = true };
            split.Panel2.Controls.Add(right);

            var header = new Label
            {
                Name = "lblOverviewHeader",
                Text = "StruxureGuard UI Style Tool — Preview Playground",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                AutoSize = true
            };
            right.Controls.Add(header);

            var desc = new Label
            {
                Name = "lblOverviewDesc",
                Top = header.Bottom + 10,
                AutoSize = true,
                MaximumSize = new Size(900, 0),
                Text =
                    "ThemeKey-first overrides:\r\n" +
                    "- Click any control to select it\r\n" +
                    "- Use 'Create/Enable Override (ThemeKey)' to edit per-control styling\r\n" +
                    "- The ThemeKey is shown and can be copied\r\n" +
                    "- Export → C# Preset generates a Preset class from your current theme"
            };
            right.Controls.Add(desc);

            var grp = new GroupBox
            {
                Name = "grpQuick",
                Text = "Quick smoke test",
                Top = desc.Bottom + 14,
                Width = 980,
                Height = 260
            };
            right.Controls.Add(grp);

            var btn1 = new Button
            {
                Name = "btnQuickPrimary",
                Text = "Primary",
                Width = 160,
                Height = 34,
                Left = 14,
                Top = 28,
                Image = _smallImages.Images[0],
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleRight
            };

            var btn2 = new Button
            {
                Name = "btnQuickSecondary",
                Text = "Secondary",
                Width = 160,
                Height = 34,
                Left = 184,
                Top = 28,
                Image = _smallImages.Images[1],
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleRight
            };

            var btn3 = new Button { Name = "btnQuickDisabled", Text = "Disabled", Width = 160, Height = 34, Left = 354, Top = 28, Enabled = false };

            grp.Controls.Add(btn1);
            grp.Controls.Add(btn2);
            grp.Controls.Add(btn3);

            var tb = new TextBox { Name = "txtQuick", Left = 14, Top = 80, Width = 320, Text = "TextBox sample (border, fore/back)" };
            grp.Controls.Add(tb);

            var m = new MaskedTextBox { Name = "mskQuick", Left = 354, Top = 80, Width = 240, Mask = "00/00/0000", Text = "01012025" };
            grp.Controls.Add(m);

            var cb = new ComboBox { Name = "cmbQuick", Left = 14, Top = 120, Width = 320, DropDownStyle = ComboBoxStyle.DropDownList };
            cb.Items.AddRange(new object[] { "Option A", "Option B", "Option C" });
            cb.SelectedIndex = 0;
            grp.Controls.Add(cb);

            var nud = new NumericUpDown { Name = "nudQuick", Left = 354, Top = 120, Width = 120, Minimum = -100, Maximum = 100, Value = 42 };
            grp.Controls.Add(nud);

            var chk = new CheckBox { Name = "chkQuick", Left = 14, Top = 160, AutoSize = true, Text = "CheckBox" };
            var rb = new RadioButton { Name = "rbQuick", Left = 140, Top = 160, AutoSize = true, Text = "RadioButton" };
            grp.Controls.Add(chk);
            grp.Controls.Add(rb);

            var link = new LinkLabel { Name = "lnkQuick", Left = 354, Top = 160, AutoSize = true, Text = "LinkLabel (click)" };
            link.LinkClicked += (_, __) => MessageBox.Show(this, "LinkLabel clicked", "LinkLabel");
            grp.Controls.Add(link);

            _toolTip.SetToolTip(btn1, "Button with icon + text alignment");
            _toolTip.SetToolTip(tb, "TextBox: check InputBack/InputText/border");
            _toolTip.SetToolTip(cb, "ComboBox: test dropdown + selection");
            _toolTip.SetToolTip(link, "LinkLabel click test");

            _errorProvider.SetError(tb, "Example ErrorProvider message");

            return page;
        }

        private TabPage BuildButtonsTab()
        {
            var page = new TabPage("Buttons");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };
            page.Controls.Add(root);

            var grp1 = new GroupBox { Name = "grpButtonsStandard", Text = "Standard Buttons", Dock = DockStyle.Top, Height = 260 };
            root.Controls.Add(grp1);

            grp1.Controls.Add(new Button { Name = "btnStdA", Text = "Button A", Left = 14, Top = 30, Width = 170, Height = 36 });
            grp1.Controls.Add(new Button { Name = "btnStdB", Text = "Button B", Left = 194, Top = 30, Width = 170, Height = 36 });
            grp1.Controls.Add(new Button { Name = "btnStdDisabled", Text = "Disabled", Left = 374, Top = 30, Width = 170, Height = 36, Enabled = false });

            var btnIconL = new Button
            {
                Name = "btnIconLeft",
                Text = "Icon Left",
                Left = 14,
                Top = 86,
                Width = 170,
                Height = 40,
                Image = _smallImages.Images[2],
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleRight
            };
            grp1.Controls.Add(btnIconL);

            var btnIconR = new Button
            {
                Name = "btnIconRight",
                Text = "Icon Right",
                Left = 194,
                Top = 86,
                Width = 170,
                Height = 40,
                Image = _smallImages.Images[3],
                ImageAlign = ContentAlignment.MiddleRight,
                TextAlign = ContentAlignment.MiddleLeft
            };
            grp1.Controls.Add(btnIconR);

            var btnImageAbove = new Button
            {
                Name = "btnImageAbove",
                Text = "Image Above",
                Left = 374,
                Top = 86,
                Width = 170,
                Height = 80,
                Image = _largeImages.Images[0],
                ImageAlign = ContentAlignment.TopCenter,
                TextAlign = ContentAlignment.BottomCenter
            };
            grp1.Controls.Add(btnImageAbove);

            var grp2 = new GroupBox { Name = "grpButtonsOther", Text = "Other clickable controls", Dock = DockStyle.Top, Height = 240, Top = grp1.Bottom + 12 };
            root.Controls.Add(grp2);

            grp2.Controls.Add(new CheckBox { Name = "chkA", Text = "CheckBox A", Left = 14, Top = 30, AutoSize = true, Checked = true });
            grp2.Controls.Add(new CheckBox { Name = "chkB", Text = "CheckBox B", Left = 14, Top = 60, AutoSize = true });

            grp2.Controls.Add(new RadioButton { Name = "rbA", Text = "Radio A", Left = 170, Top = 30, AutoSize = true, Checked = true });
            grp2.Controls.Add(new RadioButton { Name = "rbB", Text = "Radio B", Left = 170, Top = 60, AutoSize = true });

            var toggle = new CheckBox
            {
                Name = "chkToggleStyle",
                Text = "Toggle style placeholder (CheckBox)",
                Appearance = Appearance.Button,
                TextAlign = ContentAlignment.MiddleCenter,
                Left = 14,
                Top = 100,
                Width = 240,
                Height = 36
            };
            grp2.Controls.Add(toggle);

            var lnk = new LinkLabel { Name = "lnkButtons", Text = "LinkLabel in Buttons tab", Left = 14, Top = 150, AutoSize = true };
            lnk.LinkClicked += (_, __) => MessageBox.Show(this, "LinkLabel clicked", "Buttons Tab");
            grp2.Controls.Add(lnk);

            return page;
        }

        private TabPage BuildInputsTab()
        {
            var page = new TabPage("Inputs");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };
            page.Controls.Add(root);

            var grp = new GroupBox { Name = "grpInputs", Text = "Inputs", Dock = DockStyle.Top, Height = 360 };
            root.Controls.Add(grp);

            grp.Controls.Add(new Label { Name = "lblInputA", Text = "TextBox:", Left = 14, Top = 34, AutoSize = true });
            grp.Controls.Add(new TextBox { Name = "txtInputA", Left = 140, Top = 30, Width = 360, Text = "Single line TextBox" });

            grp.Controls.Add(new Label { Name = "lblInputB", Text = "Multiline:", Left = 14, Top = 74, AutoSize = true });
            grp.Controls.Add(new TextBox { Name = "txtInputB", Left = 140, Top = 70, Width = 360, Height = 80, Multiline = true, Text = "Line 1\r\nLine 2\r\nLine 3" });

            grp.Controls.Add(new Label { Name = "lblMasked", Text = "MaskedTextBox:", Left = 14, Top = 170, AutoSize = true });
            grp.Controls.Add(new MaskedTextBox { Name = "mskInput", Left = 140, Top = 166, Width = 200, Mask = "(999) 000-0000" });

            grp.Controls.Add(new Label { Name = "lblCombo", Text = "ComboBox:", Left = 14, Top = 210, AutoSize = true });
            var cb = new ComboBox { Name = "cmbInput", Left = 140, Top = 206, Width = 360, DropDownStyle = ComboBoxStyle.DropDownList };
            cb.Items.AddRange(new object[] { "Alpha", "Beta", "Gamma", "Delta" });
            cb.SelectedIndex = 1;
            grp.Controls.Add(cb);

            grp.Controls.Add(new Label { Name = "lblEditableCombo", Text = "ComboBox (editable):", Left = 14, Top = 250, AutoSize = true });
            var cbEdit = new ComboBox { Name = "cmbInputEditable", Left = 140, Top = 246, Width = 360, DropDownStyle = ComboBoxStyle.DropDown };
            cbEdit.Items.AddRange(new object[] { "Item 1", "Item 2", "Item 3" });
            cbEdit.Text = "Type here…";
            grp.Controls.Add(cbEdit);

            grp.Controls.Add(new Label { Name = "lblNumeric", Text = "NumericUpDown:", Left = 14, Top = 290, AutoSize = true });
            grp.Controls.Add(new NumericUpDown { Name = "nudInput", Left = 140, Top = 286, Width = 120, Minimum = -1000, Maximum = 1000, Value = 42 });

            grp.Controls.Add(new Label { Name = "lblDomain", Text = "DomainUpDown:", Left = 14, Top = 330, AutoSize = true });
            var dud = new DomainUpDown { Name = "dudInput", Left = 140, Top = 326, Width = 200 };
            dud.Items.Add("One");
            dud.Items.Add("Two");
            dud.Items.Add("Three");
            dud.SelectedIndex = 1;
            grp.Controls.Add(dud);

            return page;
        }

        private TabPage BuildTextAndRichTab()
        {
            var page = new TabPage("Text/Rich");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };
            page.Controls.Add(root);

            var grp = new GroupBox { Name = "grpText", Text = "Labels, text rendering, rich text", Dock = DockStyle.Top, Height = 460 };
            root.Controls.Add(grp);

            var h1 = new Label { Name = "lblH1", Text = "Header (Bold)", Left = 14, Top = 30, AutoSize = true, Font = new Font("Segoe UI", 14f, FontStyle.Bold) };
            grp.Controls.Add(h1);

            var p = new Label
            {
                Name = "lblParagraph",
                Left = 14,
                Top = 70,
                AutoSize = true,
                MaximumSize = new Size(900, 0),
                Text = "Sample paragraph label. Override this label to test muted/contrast behaviors."
            };
            grp.Controls.Add(p);

            var lblFixed = new Label
            {
                Name = "lblFixedBorder",
                Left = 14,
                Top = 140,
                Width = 380,
                Height = 60,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "Bordered Label\r\n(alignments)",
                TextAlign = ContentAlignment.MiddleCenter
            };
            grp.Controls.Add(lblFixed);

            var link = new LinkLabel { Name = "lnkTextTab", Left = 410, Top = 160, AutoSize = true, Text = "LinkLabel in text tab" };
            link.LinkClicked += (_, __) => MessageBox.Show(this, "LinkLabel clicked", "Text/Rich");
            grp.Controls.Add(link);

            var rt = new RichTextBox
            {
                Name = "rtbDemo",
                Left = 14,
                Top = 220,
                Width = 900,
                Height = 190,
                WordWrap = false,
                Text =
                    "RichTextBox demo:\r\n" +
                    "- Check InputBack/InputText styling\r\n" +
                    "- Mixed formatting manually\r\n\r\n" +
                    "0123456789 ABCDEFGHIJKLMNOPQRSTUVWXYZ"
            };
            rt.Select(0, 11);
            rt.SelectionFont = new Font(rt.Font, FontStyle.Bold);
            rt.Select(0, 0);
            grp.Controls.Add(rt);

            return page;
        }

        private TabPage BuildListsTab()
        {
            var page = new TabPage("Lists");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14) };
            page.Controls.Add(root);

            var sc = CreateSafeSplitContainer(Orientation.Vertical, DockStyle.Fill, 320, 420, 420);
            root.Controls.Add(sc);

            var leftTabs = new TabControl { Dock = DockStyle.Fill, Name = "tabListsLeft" };
            sc.Panel1.Controls.Add(leftTabs);

            var listBoxPage = new TabPage("ListBox");
            var lb = new ListBox { Name = "listBoxDemo", Dock = DockStyle.Fill };
            lb.Items.AddRange(Enumerable.Range(1, 50).Select(i => $"List item {i}").Cast<object>().ToArray());
            listBoxPage.Controls.Add(lb);
            leftTabs.TabPages.Add(listBoxPage);

            var clbPage = new TabPage("CheckedListBox");
            var clb = new CheckedListBox { Name = "checkedListDemo", Dock = DockStyle.Fill };
            clb.Items.Add("Option A", true);
            clb.Items.Add("Option B", false);
            clb.Items.Add("Option C", true);
            clb.Items.Add("Option D", false);
            clbPage.Controls.Add(clb);
            leftTabs.TabPages.Add(clbPage);

            var listViewPage = new TabPage("ListView (Details)");
            var lv = new ListView
            {
                Name = "listViewDetails",
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            lv.Columns.Add("Name", 180);
            lv.Columns.Add("Value", 120);
            lv.Columns.Add("Status", 120);
            lv.SmallImageList = _smallImages;

            for (int i = 0; i < 20; i++)
            {
                var item = new ListViewItem($"Item {i + 1}", i % _smallImages.Images.Count);
                item.SubItems.Add((1000 + i).ToString());
                item.SubItems.Add(i % 3 == 0 ? "OK" : i % 3 == 1 ? "WARN" : "FAIL");
                lv.Items.Add(item);
            }
            listViewPage.Controls.Add(lv);
            leftTabs.TabPages.Add(listViewPage);

            var rightRoot = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), AutoScroll = true };
            sc.Panel2.Controls.Add(rightRoot);

            var grp = new GroupBox { Name = "grpListExtras", Text = "Extra list-ish controls", Dock = DockStyle.Top, Height = 340 };
            rightRoot.Controls.Add(grp);

            var dd = new ComboBox { Name = "cmbListExtras", Left = 14, Top = 56, Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
            dd.Items.AddRange(new object[] { "Selection A", "Selection B", "Selection C" });
            dd.SelectedIndex = 0;
            grp.Controls.Add(new Label { Name = "lblListCombo", Text = "ComboBox:", Left = 14, Top = 32, AutoSize = true });
            grp.Controls.Add(dd);

            var lst = new ListBox { Name = "listBoxSmall", Left = 14, Top = 96, Width = 300, Height = 200 };
            lst.Items.AddRange(new object[] { "Apple", "Banana", "Cherry", "Date", "Elderberry", "Fig", "Grape" });
            grp.Controls.Add(lst);

            return page;
        }

        private TabPage BuildTreesAndIconsTab()
        {
            var page = new TabPage("Tree/Icons");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14) };
            page.Controls.Add(root);

            var sc = CreateSafeSplitContainer(Orientation.Vertical, DockStyle.Fill, 420, 420, 520);
            root.Controls.Add(sc);

            var tv = new TreeView
            {
                Name = "treeViewIcons",
                Dock = DockStyle.Fill,
                HideSelection = false,
                ImageList = _smallImages
            };
            var rootNode = new TreeNode("Root", 0, 0);
            for (int i = 0; i < 10; i++)
            {
                var child = new TreeNode($"Node {i + 1}", (i + 1) % _smallImages.Images.Count, (i + 1) % _smallImages.Images.Count);
                child.Nodes.Add(new TreeNode("SubNode A", 2, 2));
                child.Nodes.Add(new TreeNode("SubNode B", 3, 3));
                rootNode.Nodes.Add(child);
            }
            tv.Nodes.Add(rootNode);
            rootNode.Expand();
            sc.Panel1.Controls.Add(tv);

            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), AutoScroll = true, Name = "panelIconGallery" };
            sc.Panel2.Controls.Add(right);

            var lbl = new Label
            {
                Name = "lblIconGallery",
                Text = "SystemIcons gallery (small & large). Useful for testing icon contrast and alignment.",
                AutoSize = true
            };
            right.Controls.Add(lbl);

            var flow = new FlowLayoutPanel
            {
                Name = "flowIcons",
                Dock = DockStyle.Top,
                Height = 420,
                AutoScroll = true,
                WrapContents = true
            };
            right.Controls.Add(flow);

            for (int i = 0; i < _largeImages.Images.Count; i++)
            {
                var p = new Panel { Width = 140, Height = 90, Margin = new Padding(8) };
                var pic = new PictureBox
                {
                    Name = $"picIcon_{i}",
                    Width = 40,
                    Height = 40,
                    Left = 10,
                    Top = 10,
                    SizeMode = PictureBoxSizeMode.CenterImage,
                    Image = _largeImages.Images[i]
                };
                var l = new Label
                {
                    Name = $"lblIcon_{i}",
                    Left = 60,
                    Top = 20,
                    Width = 70,
                    Height = 50,
                    Text = $"#{i}",
                    TextAlign = ContentAlignment.MiddleLeft
                };
                p.Controls.Add(pic);
                p.Controls.Add(l);
                flow.Controls.Add(p);
            }

            return page;
        }

        private TabPage BuildDataGridTab()
        {
            var page = new TabPage("DataGrid");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14) };
            page.Controls.Add(root);

            var dgv = new DataGridView
            {
                Name = "gridDemo",
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true
            };
            dgv.Columns.Add("colA", "Name");
            dgv.Columns.Add("colB", "Value");
            dgv.Columns.Add("colC", "State");
            dgv.Columns.Add("colD", "Notes");

            for (int i = 0; i < 25; i++)
            {
                dgv.Rows.Add(
                    $"Row {i + 1}",
                    (1000 + i).ToString(),
                    i % 3 == 0 ? "OK" : i % 3 == 1 ? "WARN" : "FAIL",
                    "Some longer text to test wrapping/selection/contrast."
                );
            }

            root.Controls.Add(dgv);
            return page;
        }

        private TabPage BuildContainersLayoutTab()
        {
            var page = new TabPage("Layout");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14) };
            page.Controls.Add(root);

            var sc = CreateSafeSplitContainer(Orientation.Vertical, DockStyle.Fill, 420, 520, 520);
            root.Controls.Add(sc);

            var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), AutoScroll = true, Name = "panelLayoutLeft" };
            sc.Panel1.Controls.Add(left);

            var tlp = new TableLayoutPanel
            {
                Name = "tlpDemo",
                Dock = DockStyle.Top,
                ColumnCount = 3,
                RowCount = 6,
                AutoSize = true,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            void AddRow(int row, string label, Control c1, Control c2)
            {
                tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
                var lbl = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Name = $"lbl_{label.Replace(" ", "_")}" };
                c1.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                c2.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                tlp.Controls.Add(lbl, 0, row);
                tlp.Controls.Add(c1, 1, row);
                tlp.Controls.Add(c2, 2, row);
            }

            AddRow(0, "Row 1", new TextBox { Name = "txtRow1A", Text = "A" }, new TextBox { Name = "txtRow1B", Text = "B" });
            AddRow(1, "Row 2", new ComboBox { Name = "cmbRow2A", DropDownStyle = ComboBoxStyle.DropDownList, Items = { "One", "Two", "Three" }, SelectedIndex = 1 }, new NumericUpDown { Name = "nudRow2B", Value = 5 });
            AddRow(2, "Row 3", new Button { Name = "btnRow3A", Text = "Button" }, new CheckBox { Name = "chkRow3B", Text = "Check", AutoSize = true });
            AddRow(3, "Row 4", new ProgressBar { Name = "progRow4A", Value = 65 }, new ThemedProgressBar { Name = "progRow4B", Value = 35, Width = 200, Height = 22 });
            AddRow(4, "Row 5", new DateTimePicker { Name = "dtpRow5A" }, new TextBox { Name = "txtRow5B", Text = "More text" });
            AddRow(5, "Row 6", new TrackBar { Name = "trkRow6A", Minimum = 0, Maximum = 10, Value = 4, Width = 120 }, new Button { Name = "btnRow6B", Text = "Icon", Image = _smallImages.Images[4], ImageAlign = ContentAlignment.MiddleLeft, TextAlign = ContentAlignment.MiddleRight });

            left.Controls.Add(tlp);

            var flow = new FlowLayoutPanel
            {
                Name = "flowLayoutDemo",
                Dock = DockStyle.Top,
                Height = 120,
                Top = tlp.Bottom + 10,
                WrapContents = true
            };
            flow.Controls.Add(new GroupBox { Name = "grpFlowA", Text = "Flow A", Width = 160, Height = 90 });
            flow.Controls.Add(new GroupBox { Name = "grpFlowB", Text = "Flow B", Width = 160, Height = 90 });
            flow.Controls.Add(new GroupBox { Name = "grpFlowC", Text = "Flow C", Width = 160, Height = 90 });
            left.Controls.Add(flow);

            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), Name = "panelLayoutRight" };
            sc.Panel2.Controls.Add(right);

            var nestedSplit = CreateSafeSplitContainer(Orientation.Horizontal, DockStyle.Fill, 220, 220, 260);
            right.Controls.Add(nestedSplit);

            var nestedTabs = new TabControl { Dock = DockStyle.Fill, Name = "tabNested" };
            var t1 = new TabPage("Nested A");
            var t2 = new TabPage("Nested B");

            t1.Controls.Add(new Label { Name = "lblNestedA", Text = "Nested Tab A", AutoSize = true, Left = 12, Top = 12 });
            t1.Controls.Add(new TextBox { Name = "txtNestedA", Left = 12, Top = 40, Width = 320, Text = "Nested TextBox" });

            t2.Controls.Add(new Label { Name = "lblNestedB", Text = "Nested Tab B", AutoSize = true, Left = 12, Top = 12 });
            t2.Controls.Add(new ListBox { Name = "lbNestedB", Left = 12, Top = 40, Width = 320, Height = 130 });

            nestedTabs.TabPages.Add(t1);
            nestedTabs.TabPages.Add(t2);

            nestedSplit.Panel1.Controls.Add(nestedTabs);

            var panelBottom = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), Name = "panelBottom" };
            panelBottom.Controls.Add(new GroupBox { Name = "grpBottom", Text = "Bottom Panel", Dock = DockStyle.Fill });
            nestedSplit.Panel2.Controls.Add(panelBottom);

            return page;
        }

        private TabPage BuildStripsMenusStatusTab()
        {
            var page = new TabPage("Strips/Menus");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0) };
            page.Controls.Add(root);

            var menu = new MenuStrip { Name = "menuDemo", Dock = DockStyle.Top };
            var file = new ToolStripMenuItem("File") { Image = _smallImages.Images[0] };
            file.DropDownItems.Add(new ToolStripMenuItem("New") { Image = _smallImages.Images[1] });
            file.DropDownItems.Add(new ToolStripMenuItem("Open…") { Image = _smallImages.Images[2] });
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(new ToolStripMenuItem("Exit") { Image = _smallImages.Images[3] });

            var view = new ToolStripMenuItem("View");
            view.DropDownItems.Add(new ToolStripMenuItem("Toggle A") { Checked = true, CheckOnClick = true });
            view.DropDownItems.Add(new ToolStripMenuItem("Toggle B") { Checked = false, CheckOnClick = true });

            var help = new ToolStripMenuItem("Help");
            help.DropDownItems.Add(new ToolStripMenuItem("About") { Image = _smallImages.Images[4] });

            menu.Items.Add(file);
            menu.Items.Add(view);
            menu.Items.Add(help);

            root.Controls.Add(menu);

            var ts = new ToolStrip { Name = "toolStripDemo", Dock = DockStyle.Top };
            ts.Items.Add(new ToolStripButton("Run") { Image = _smallImages.Images[2], DisplayStyle = ToolStripItemDisplayStyle.ImageAndText });
            ts.Items.Add(new ToolStripButton("Stop") { Image = _smallImages.Images[3], DisplayStyle = ToolStripItemDisplayStyle.ImageAndText });
            ts.Items.Add(new ToolStripSeparator());
            ts.Items.Add(new ToolStripDropDownButton("Options") { Image = _smallImages.Images[1] });
            ts.Items.Add(new ToolStripTextBox { Text = "Search…", Width = 180 });
            ts.Items.Add(new ToolStripComboBox { Width = 140, Items = { "One", "Two", "Three" }, SelectedIndex = 0 });

            root.Controls.Add(ts);

            var status = new StatusStrip { Name = "statusDemo", Dock = DockStyle.Bottom };
            var sl1 = new ToolStripStatusLabel("Ready") { Image = _smallImages.Images[0] };
            var sl2 = new ToolStripStatusLabel("Theme preview") { Spring = true };
            var sl3 = new ToolStripStatusLabel("12:34") { Image = _smallImages.Images[4] };
            status.Items.Add(sl1);
            status.Items.Add(sl2);
            status.Items.Add(sl3);

            root.Controls.Add(status);

            var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true, Name = "panelStripsContent" };
            root.Controls.Add(content);

            var lbl = new Label
            {
                Name = "lblStripsInfo",
                Text = "This tab tests MenuStrip/ToolStrip/StatusStrip rendering.\r\nRight-click the button below to test ContextMenuStrip.",
                AutoSize = true
            };
            content.Controls.Add(lbl);

            var ctx = new ContextMenuStrip { Name = "ctxDemo" };
            ctx.Items.Add("Copy", null, (_, __) => MessageBox.Show(this, "Copy clicked", "ContextMenu"));
            ctx.Items.Add("Paste", null, (_, __) => MessageBox.Show(this, "Paste clicked", "ContextMenu"));
            ctx.Items.Add(new ToolStripSeparator());
            ctx.Items.Add("Advanced", _smallImages.Images[1], (_, __) => MessageBox.Show(this, "Advanced clicked", "ContextMenu"));

            var btnCtx = new Button
            {
                Name = "btnContextMenu",
                Text = "Right-click me (ContextMenuStrip)",
                Width = 320,
                Height = 36,
                Left = 14,
                Top = lbl.Bottom + 14,
                ContextMenuStrip = ctx
            };
            content.Controls.Add(btnCtx);

            return page;
        }

        private TabPage BuildPickersAndSlidersTab()
        {
            var page = new TabPage("Pickers/Sliders");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14) };
            page.Controls.Add(root);

            var sc = CreateSafeSplitContainer(Orientation.Vertical, DockStyle.Fill, 420, 420, 520);
            root.Controls.Add(sc);

            var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), AutoScroll = true, Name = "panelPickersLeft" };
            sc.Panel1.Controls.Add(left);

            var dtp1 = new DateTimePicker { Name = "dtpShort", Left = 14, Top = 14, Width = 260, Format = DateTimePickerFormat.Short };
            var dtp2 = new DateTimePicker { Name = "dtpLong", Left = 14, Top = 54, Width = 420, Format = DateTimePickerFormat.Long };
            var dtp3 = new DateTimePicker { Name = "dtpTime", Left = 14, Top = 94, Width = 180, Format = DateTimePickerFormat.Time, ShowUpDown = true };

            left.Controls.Add(new Label { Name = "lblDtp", Text = "DateTimePicker:", Left = 14, Top = 0, AutoSize = true });
            left.Controls.Add(dtp1);
            left.Controls.Add(dtp2);
            left.Controls.Add(dtp3);

            var cal = new MonthCalendar { Name = "monthCal", Left = 14, Top = 140, MaxSelectionCount = 14 };
            left.Controls.Add(cal);

            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), AutoScroll = true, Name = "panelPickersRight" };
            sc.Panel2.Controls.Add(right);

            right.Controls.Add(new Label { Name = "lblSliders", Text = "TrackBar / ScrollBar / UpDown:", Left = 14, Top = 14, AutoSize = true });

            var trk = new TrackBar
            {
                Name = "trackBarDemo",
                Left = 14,
                Top = 40,
                Width = 360,
                Minimum = 0,
                Maximum = 100,
                TickFrequency = 10,
                Value = 35
            };
            right.Controls.Add(trk);

            var hsb = new HScrollBar { Name = "hsbDemo", Left = 14, Top = 100, Width = 360, Minimum = 0, Maximum = 200, Value = 50 };
            var vsb = new VScrollBar { Name = "vsbDemo", Left = 390, Top = 40, Height = 160, Minimum = 0, Maximum = 200, Value = 80 };
            right.Controls.Add(hsb);
            right.Controls.Add(vsb);

            var upDown = new NumericUpDown { Name = "nudSliders", Left = 14, Top = 140, Width = 140, Minimum = 0, Maximum = 10000, Value = 1234 };
            right.Controls.Add(upDown);

            return page;
        }

        private TabPage BuildProgressAndTimersTab(out ThemedProgressBar a, out ThemedProgressBar b, out ProgressBar win)
        {
            var page = new TabPage("Progress");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };
            page.Controls.Add(root);

            root.Controls.Add(new Label
            {
                Name = "lblProgressInfo",
                Text = "Progress controls: WinForms ProgressBar + custom ThemedProgressBar + marquee style.\r\nTimer animates values.",
                AutoSize = true
            });

            win = new ProgressBar { Name = "progWin", Left = 14, Top = 60, Width = 500, Height = 22, Minimum = 0, Maximum = 100, Value = 40 };
            root.Controls.Add(win);

            var marquee = new ProgressBar { Name = "progMarquee", Left = 14, Top = 92, Width = 500, Height = 18, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 28 };
            root.Controls.Add(marquee);

            a = new ThemedProgressBar { Name = "progThemedA", Left = 14, Top = 130, Width = 500, Height = 22, Value = 25 };
            b = new ThemedProgressBar { Name = "progThemedB", Left = 14, Top = 162, Width = 500, Height = 22, Value = 75 };
            root.Controls.Add(a);
            root.Controls.Add(b);

            var grp = new GroupBox { Name = "grpSpinner", Text = "Indeterminate / Busy indicators", Left = 14, Top = 210, Width = 780, Height = 220 };
            root.Controls.Add(grp);

            var busyLbl = new Label { Name = "lblBusy", Text = "Busy…", Left = 14, Top = 32, AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
            grp.Controls.Add(busyLbl);

            var busyBar = new ProgressBar { Name = "progBusy", Left = 14, Top = 62, Width = 520, Height = 18, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 22 };
            grp.Controls.Add(busyBar);

            var btn = new Button { Name = "btnProgressTest", Left = 14, Top = 110, Width = 240, Height = 34, Text = "Show fake task dialog" };
            btn.Click += (_, __) => MessageBox.Show(this, "Pretend task finished!", "Progress");
            grp.Controls.Add(btn);

            return page;
        }

        private TabPage BuildDialogsAndComponentsTab()
        {
            var page = new TabPage("Dialogs/Components");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };
            page.Controls.Add(root);

            var grp = new GroupBox { Name = "grpDialogs", Text = "Common dialogs", Width = 1100, Height = 220 };
            root.Controls.Add(grp);

            var btnMsg = new Button { Name = "btnDlgMessageBox", Text = "MessageBox", Left = 14, Top = 30, Width = 160, Height = 34 };
            btnMsg.Click += (_, __) => MessageBox.Show(this, "MessageBox test", "Dialog", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            grp.Controls.Add(btnMsg);

            var btnColor = new Button { Name = "btnDlgColor", Text = "ColorDialog", Left = 184, Top = 30, Width = 160, Height = 34 };
            btnColor.Click += (_, __) =>
            {
                using var dlg = new ColorDialog();
                dlg.Color = _workingTheme.Accent;
                dlg.ShowDialog(this);
            };
            grp.Controls.Add(btnColor);

            var btnFont = new Button { Name = "btnDlgFont", Text = "FontDialog", Left = 354, Top = 30, Width = 160, Height = 34 };
            btnFont.Click += (_, __) =>
            {
                using var dlg = new FontDialog();
                dlg.Font = Font;
                dlg.ShowDialog(this);
            };
            grp.Controls.Add(btnFont);

            var btnOpen = new Button { Name = "btnDlgOpen", Text = "OpenFileDialog", Left = 524, Top = 30, Width = 160, Height = 34 };
            btnOpen.Click += (_, __) =>
            {
                using var dlg = new OpenFileDialog();
                dlg.Title = "OpenFileDialog (test)";
                dlg.Filter = "All files (*.*)|*.*";
                dlg.ShowDialog(this);
            };
            grp.Controls.Add(btnOpen);

            var btnSave = new Button { Name = "btnDlgSave", Text = "SaveFileDialog", Left = 694, Top = 30, Width = 160, Height = 34 };
            btnSave.Click += (_, __) =>
            {
                using var dlg = new SaveFileDialog();
                dlg.Title = "SaveFileDialog (test)";
                dlg.Filter = "Text (*.txt)|*.txt|All files (*.*)|*.*";
                dlg.ShowDialog(this);
            };
            grp.Controls.Add(btnSave);

            var btnFolder = new Button { Name = "btnDlgFolder", Text = "FolderBrowserDialog", Left = 864, Top = 30, Width = 200, Height = 34 };
            btnFolder.Click += (_, __) =>
            {
                using var dlg = new FolderBrowserDialog();
                dlg.Description = "FolderBrowserDialog (test)";
                dlg.ShowDialog(this);
            };
            grp.Controls.Add(btnFolder);

            var grp2 = new GroupBox { Name = "grpComponents", Text = "Components / non-visual", Top = grp.Bottom + 14, Width = 1100, Height = 300 };
            root.Controls.Add(grp2);

            grp2.Controls.Add(new Label
            {
                Name = "lblComponentsInfo",
                Left = 14,
                Top = 28,
                AutoSize = true,
                MaximumSize = new Size(1000, 0),
                Text =
                    "ToolTip: hover on controls in other tabs.\r\n" +
                    "ErrorProvider: shown on Overview tab TextBox.\r\n" +
                    "NotifyIcon: tray icon; double-click to restore.\r\n" +
                    "These are important for contrast/icon visibility testing."
            });

            var btnNotify = new Button { Name = "btnNotify", Left = 14, Top = 120, Width = 240, Height = 34, Text = "Show tray balloon" };
            btnNotify.Click += (_, __) =>
            {
                try
                {
                    _notifyIcon.BalloonTipTitle = "StruxureGuard UI Tool";
                    _notifyIcon.BalloonTipText = "This is a NotifyIcon balloon test.";
                    _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
                    _notifyIcon.ShowBalloonTip(2500);
                }
                catch { }
            };
            grp2.Controls.Add(btnNotify);

            var btnError = new Button { Name = "btnToggleError", Left = 270, Top = 120, Width = 240, Height = 34, Text = "Toggle ErrorProvider" };
            btnError.Click += (_, __) =>
            {
                var txt = FindControlByName(_tabs, "txtQuick");
                if (txt is null) { MessageBox.Show(this, "Could not find txtQuick", "ErrorProvider"); return; }

                var existing = _errorProvider.GetError(txt);
                _errorProvider.SetError(txt, string.IsNullOrWhiteSpace(existing) ? "Example ErrorProvider message" : "");
            };
            grp2.Controls.Add(btnError);

            var btnTopMost = new Button { Name = "btnTopMost", Left = 526, Top = 120, Width = 240, Height = 34, Text = "Toggle TopMost" };
            btnTopMost.Click += (_, __) => TopMost = !TopMost;
            grp2.Controls.Add(btnTopMost);

            return page;
        }

        // =========================
        // Icons helpers
        // =========================
        private static ImageList BuildImageList(int size)
        {
            var il = new ImageList { ImageSize = new Size(size, size), ColorDepth = ColorDepth.Depth32Bit };

            var icons = new[]
            {
                SystemIcons.Application,
                SystemIcons.Information,
                SystemIcons.Question,
                SystemIcons.Warning,
                SystemIcons.Error,
                SystemIcons.Shield,
                SystemIcons.WinLogo,
                SystemIcons.Asterisk,
                SystemIcons.Hand
            };

            foreach (var ic in icons)
                il.Images.Add(new Icon(ic, new Size(size, size)).ToBitmap());

            return il;
        }

        private static Control? FindControlByName(Control root, string name)
        {
            if (root.Name == name) return root;
            foreach (Control c in root.Controls)
            {
                var found = FindControlByName(c, name);
                if (found is not null) return found;
            }
            return null;
        }
    }

    internal static class Prompt
    {
        public static string? Show(string title, string label, string initial)
        {
            using var f = new Form
            {
                Text = title,
                Width = 420,
                Height = 160,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lbl = new Label { Text = label, Left = 12, Top = 16, AutoSize = true };
            var txt = new TextBox { Left = 12, Top = 40, Width = 380, Text = initial ?? "" };
            var ok = new Button { Text = "OK", Left = 232, Width = 75, Top = 76, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancel", Left = 317, Width = 75, Top = 76, DialogResult = DialogResult.Cancel };

            f.Controls.Add(lbl);
            f.Controls.Add(txt);
            f.Controls.Add(ok);
            f.Controls.Add(cancel);

            f.AcceptButton = ok;
            f.CancelButton = cancel;

            return f.ShowDialog() == DialogResult.OK ? txt.Text : null;
        }
    }

    // Demo object for PropertyGrid
    internal sealed class DemoMiscObject
    {
        public string Name { get; set; } = "Example";
        public int Count { get; set; } = 42;
        public bool Enabled { get; set; } = true;
        public DateTime Date { get; set; } = DateTime.Today;
        public DayOfWeek Day { get; set; } = DateTime.Today.DayOfWeek;
    }
}
