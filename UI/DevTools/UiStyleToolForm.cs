using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using StruxureGuard.Styling;
using StruxureGuard.UI.Controls;
using StruxureGuard.Core.Logging;

namespace StruxureGuard.UI.DevTools
{
    public class UiStyleToolForm : Form
    {
        // Top bar (custom)
        private readonly Panel _topBar;
        private readonly Label _title;
        private readonly ComboBox _presetCombo;
        private readonly TextBox _iconSearch;
        private readonly Button _closeBtn;

        private readonly CheckBox _toggleDisableAll;
        private readonly CheckBox _toggleReadOnlyInputs;
        private readonly CheckBox _toggleLongText;
        private readonly CheckBox _toggleDenseLayout;

        private readonly SplitContainer _split;
        private readonly TabControl _tabs;

        // Icon gallery
        private readonly ListView _iconList;
        private readonly ImageList _iconImages;
        private readonly List<(string Name, string SearchKey)> _iconIndex = new();

        // Demo progress timer
        private readonly System.Windows.Forms.Timer _timer;
        private readonly ThemedProgressBar _themedProgressA;
        private readonly ThemedProgressBar _themedProgressB;

        // State toggle tracking
        private readonly List<Control> _allDemoControls = new();
        private readonly List<TextBoxBase> _textInputs = new();
        private readonly List<ComboBox> _comboInputs = new();
        private readonly List<NumericUpDown> _numericInputs = new();
        private readonly List<DateTimePicker> _dateInputs = new();
        private bool _loadingPresets;

        private const string LongText =
            "StruxureGuard UI style stress test — this is a long piece of text to validate padding, " +
            "alignment, wrapping, ellipsis behavior, and overall readability across themes.";

        public UiStyleToolForm()
        {
            // Fullscreen (borderless)
            Text = "UI Style Tool";
            WindowState = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.None;
            KeyPreview = true;
            StartPosition = FormStartPosition.CenterScreen;

            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    Close();
                    e.Handled = true;
                }
            };

            // ===== Top bar =====
            _topBar = new Panel { Dock = DockStyle.Top, Height = 64, Padding = new Padding(12, 10, 12, 10) };

            _title = new Label
            {
                AutoSize = true,
                Text = "UI Style Tool  •  ESC to close",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Location = new Point(12, 10)
            };

            _presetCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 200
            };

                        // ===== Logging hooks (diagnose why selection isn't committing) =====
            _presetCombo.DropDown += (_, __) =>
                Log.Info("theme.ui", "PresetCombo DropDown opened");

            _presetCombo.DropDownClosed += (_, __) =>
                Log.Info("theme.ui", $"PresetCombo DropDownClosed. Selected='{_presetCombo.SelectedItem}' Index={_presetCombo.SelectedIndex}");

            _presetCombo.SelectionChangeCommitted += (_, __) =>
                Log.Info("theme.ui", $"PresetCombo SelectionChangeCommitted. Selected='{_presetCombo.SelectedItem}' Index={_presetCombo.SelectedIndex}");

            _presetCombo.SelectedIndexChanged += (_, __) =>
                Log.Info("theme.ui", $"PresetCombo SelectedIndexChanged. Selected='{_presetCombo.SelectedItem}' Index={_presetCombo.SelectedIndex} loading={_loadingPresets}");


            // Alleen triggeren wanneer user echt een keuze commit (niet tijdens Items vullen)
            _presetCombo.SelectionChangeCommitted += (_, __) =>
            {
                if (_loadingPresets) return;
                if (_presetCombo.SelectedItem is not string name) return;

                // Laat WinForms eerst de dropdown sluiten en selection committen
                BeginInvoke(new Action(() =>
                {
                    ThemeManager.ApplyPreset(name);     // Save + ApplyThemeToAllOpenForms
                    ThemeManager.ApplyTheme(this);      // tool zelf (mag, maar is optioneel)
                }));
            };

            _iconSearch = new TextBox
            {
                Width = 260,
                PlaceholderText = "Search icons…"
            };
            _iconSearch.TextChanged += (_, __) => ApplyIconFilter();

            _closeBtn = new Button
            {
                Text = "Close (ESC)",
                Width = 120,
                Height = 34
            };
            _closeBtn.Click += (_, __) => Close();

            _toggleDisableAll = new CheckBox { Text = "Disable all", AutoSize = true };
            _toggleReadOnlyInputs = new CheckBox { Text = "Read-only inputs", AutoSize = true };
            _toggleLongText = new CheckBox { Text = "Use long text", AutoSize = true };
            _toggleDenseLayout = new CheckBox { Text = "Dense layout", AutoSize = true };

            _toggleDisableAll.CheckedChanged += (_, __) => ApplyStateToggles();
            _toggleReadOnlyInputs.CheckedChanged += (_, __) => ApplyStateToggles();
            _toggleLongText.CheckedChanged += (_, __) => ApplyStateToggles();
            _toggleDenseLayout.CheckedChanged += (_, __) => ApplyStateToggles();

            // Layout top bar elements (right aligned)
            var rightFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight
            };

            rightFlow.Controls.Add(new Label { Text = "Preset:", AutoSize = true, Padding = new Padding(0, 8, 4, 0) });
            rightFlow.Controls.Add(_presetCombo);
            rightFlow.Controls.Add(new Panel { Width = 14, Height = 1 });
            rightFlow.Controls.Add(new Label { Text = "Icons:", AutoSize = true, Padding = new Padding(0, 8, 4, 0) });
            rightFlow.Controls.Add(_iconSearch);
            rightFlow.Controls.Add(new Panel { Width = 14, Height = 1 });
            rightFlow.Controls.Add(_toggleDisableAll);
            rightFlow.Controls.Add(_toggleReadOnlyInputs);
            rightFlow.Controls.Add(_toggleLongText);
            rightFlow.Controls.Add(_toggleDenseLayout);
            rightFlow.Controls.Add(new Panel { Width = 14, Height = 1 });
            rightFlow.Controls.Add(_closeBtn);

            _topBar.Controls.Add(rightFlow);
            _topBar.Controls.Add(_title);

            // ===== Split: left icons + right tabs =====
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 360,
                Panel1MinSize = 280
            };

            // Left: icon gallery
            _iconImages = new ImageList { ImageSize = new Size(24, 24), ColorDepth = ColorDepth.Depth32Bit };

            _iconList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.LargeIcon,
                LargeImageList = _iconImages,
                MultiSelect = false
            };

            var iconLeftTop = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(12, 10, 12, 8) };
            iconLeftTop.Controls.Add(new Label
            {
                Text = "Icon Gallery",
                AutoSize = true,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Dock = DockStyle.Left
            });

            var iconLeft = new Panel { Dock = DockStyle.Fill };
            iconLeft.Controls.Add(_iconList);
            iconLeft.Controls.Add(iconLeftTop);
            _split.Panel1.Controls.Add(iconLeft);

            // Right: tabs
            _tabs = new TabControl { Dock = DockStyle.Fill };
            _split.Panel2.Controls.Add(_tabs);

            // Build tabs
            _tabs.TabPages.Add(BuildOverviewTab());
            _tabs.TabPages.Add(BuildButtonsTab());
            _tabs.TabPages.Add(BuildInputsTab());
            _tabs.TabPages.Add(BuildLayoutsTab());
            _tabs.TabPages.Add(BuildListsAndDataTab());
            _tabs.TabPages.Add(BuildSystemTab());
            _tabs.TabPages.Add(BuildProgressAndFeedbackTab(out _themedProgressA, out _themedProgressB));
            _tabs.TabPages.Add(BuildTypographyTab());

            // Timer for demo progress
            _timer = new System.Windows.Forms.Timer { Interval = 80 };
            _timer.Tick += (_, __) =>
            {
                _themedProgressA.Value = (_themedProgressA.Value + 1) % 101;
                _themedProgressB.Value = (_themedProgressB.Value + 2) % 101;
            };

            // Compose
            Controls.Add(_split);
            Controls.Add(_topBar);

            Load += (_, __) =>
            {
                LoadPresets();
                LoadIcons();
                ThemeManager.ApplyTheme(this);

                _timer.Start();
                ApplyStateToggles();
            };

            FormClosed += (_, __) => _timer.Stop();
            FormClosing += (_, __) => ThemeManager.SaveCurrent();
        }

        // =========================
        // Presets + Icons
        // =========================
        private void LoadPresets()
        {
            _loadingPresets = true;
            try
            {
                _presetCombo.BeginUpdate();
                _presetCombo.Items.Clear();

                foreach (var n in ThemeManager.GetPresetNames())
                    _presetCombo.Items.Add(n);

                var current = ThemeManager.Current?.Name;
                if (!string.IsNullOrWhiteSpace(current) && _presetCombo.Items.Contains(current))
                    _presetCombo.SelectedItem = current;
                else if (_presetCombo.Items.Count > 0)
                    _presetCombo.SelectedIndex = 0;
            }
            finally
            {
                _presetCombo.EndUpdate();
                _loadingPresets = false;
            }
        }
        private void LoadIcons()
        {
            _iconImages.Images.Clear();
            _iconList.Items.Clear();
            _iconIndex.Clear();

            // SystemIcons (real Windows icons)
            AddIcon("Application", SystemIcons.Application);
            AddIcon("Asterisk", SystemIcons.Asterisk);
            AddIcon("Error", SystemIcons.Error);
            AddIcon("Exclamation", SystemIcons.Exclamation);
            AddIcon("Hand", SystemIcons.Hand);
            AddIcon("Information", SystemIcons.Information);
            AddIcon("Question", SystemIcons.Question);
            AddIcon("Shield", SystemIcons.Shield);
            AddIcon("Warning", SystemIcons.Warning);
            AddIcon("WinLogo", SystemIcons.WinLogo);

            // Custom drawn icons (gear, plus, search, close, check)
            AddIcon("Gear", CreateGearIcon(24, 24));
            AddIcon("Plus", CreatePlusIcon(24, 24));
            AddIcon("Search", CreateSearchIcon(24, 24));
            AddIcon("Close", CreateCloseIcon(24, 24));
            AddIcon("Check", CreateCheckIcon(24, 24));

            ApplyIconFilter();
        }

        private void AddIcon(string name, Icon icon)
        {
            using var bmp = icon.ToBitmap();
            AddIcon(name, bmp);
        }

        private void AddIcon(string name, Bitmap bmp)
        {
            _iconImages.Images.Add(name, bmp);
            _iconIndex.Add((name, name.ToLowerInvariant()));
        }

        private void ApplyIconFilter()
        {
            var q = (_iconSearch.Text ?? "").Trim().ToLowerInvariant();

            _iconList.BeginUpdate();
            _iconList.Items.Clear();

            foreach (var (name, key) in _iconIndex)
            {
                if (string.IsNullOrEmpty(q) || key.Contains(q))
                    _iconList.Items.Add(new ListViewItem(name, name));
            }

            _iconList.EndUpdate();
        }

        // =========================
        // State toggles (disable/readonly/longtext/dense)
        // =========================
        private void ApplyStateToggles()
        {
            bool disableAll = _toggleDisableAll.Checked;
            bool readOnlyInputs = _toggleReadOnlyInputs.Checked;
            bool useLongText = _toggleLongText.Checked;
            bool dense = _toggleDenseLayout.Checked;

            foreach (var c in _allDemoControls)
                c.Enabled = !disableAll;

            foreach (var tb in _textInputs)
                tb.ReadOnly = readOnlyInputs;

            foreach (var cb in _comboInputs) cb.Enabled = !disableAll && !readOnlyInputs;
            foreach (var nud in _numericInputs) nud.Enabled = !disableAll && !readOnlyInputs;
            foreach (var dtp in _dateInputs) dtp.Enabled = !disableAll && !readOnlyInputs;

            // Long text: any label tagged LongTextTarget
            foreach (var page in _tabs.TabPages.Cast<TabPage>())
            {
                foreach (var lbl in EnumerateControls(page).OfType<Label>())
                {
                    if (lbl.Tag as string == "LongTextTarget")
                        lbl.Text = useLongText ? LongText : "Short text label";
                }
            }

            // Dense layout: reduce padding on top-level panels
            foreach (var page in _tabs.TabPages.Cast<TabPage>())
            {
                var basePad = dense ? new Padding(8) : new Padding(14);
                if (page.Controls.Count > 0 && page.Controls[0] is Control root)
                    root.Padding = basePad;
            }

            ThemeManager.ApplyTheme(this);
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

        private T TrackControl<T>(T c) where T : Control
        {
            _allDemoControls.Add(c);
            return c;
        }

        private TextBox TrackTextInput(TextBox tb) { _textInputs.Add(tb); return tb; }
        private MaskedTextBox TrackTextInput(MaskedTextBox mtb) { _textInputs.Add(mtb); return mtb; }
        private RichTextBox TrackTextInput(RichTextBox rtb) { _textInputs.Add(rtb); return rtb; }
        private ComboBox TrackCombo(ComboBox cb) { _comboInputs.Add(cb); return cb; }
        private NumericUpDown TrackNumeric(NumericUpDown nud) { _numericInputs.Add(nud); return nud; }
        private DateTimePicker TrackDate(DateTimePicker dtp) { _dateInputs.Add(dtp); return dtp; }

        // =========================
        // Tabs
        // =========================
        private TabPage BuildOverviewTab()
        {
            var page = new TabPage("Overview");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };
            page.Controls.Add(root);

            var header = new Label
            {
                Text = "Overview / Quick sanity check",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                AutoSize = true
            };
            root.Controls.Add(header);

            var desc = new Label
            {
                Top = header.Bottom + 10,
                AutoSize = true,
                MaximumSize = new Size(980, 0),
                Text = "Validate themes across a wide range of WinForms controls, states, layouts and data density. " +
                       "Use the top toggles to stress-test disabled/read-only states and long-text rendering."
            };
            root.Controls.Add(desc);

            var badgeRow = new FlowLayoutPanel
            {
                Top = desc.Bottom + 12,
                Left = 0,
                AutoSize = true,
                WrapContents = true
            };
            badgeRow.Controls.Add(TrackControl(new Label { Text = "OK", AutoSize = true, Padding = new Padding(10, 6, 10, 6), BorderStyle = BorderStyle.FixedSingle }));
            badgeRow.Controls.Add(TrackControl(new Label { Text = "Warning", AutoSize = true, Padding = new Padding(10, 6, 10, 6), BorderStyle = BorderStyle.FixedSingle }));
            badgeRow.Controls.Add(TrackControl(new Label { Text = "Error", AutoSize = true, Padding = new Padding(10, 6, 10, 6), BorderStyle = BorderStyle.FixedSingle }));
            root.Controls.Add(badgeRow);

            var group = new GroupBox
            {
                Text = "Quick controls",
                Top = badgeRow.Bottom + 14,
                Width = 980,
                Height = 220
            };

            group.Controls.Add(TrackControl(new Button { Text = "Primary", Width = 140, Height = 34, Left = 14, Top = 28 }));
            group.Controls.Add(TrackControl(new Button { Text = "Secondary", Width = 140, Height = 34, Left = 164, Top = 28 }));
            group.Controls.Add(TrackControl(new Button { Text = "Disabled", Width = 140, Height = 34, Left = 314, Top = 28, Enabled = false }));

            group.Controls.Add(TrackTextInput(new TextBox { Left = 14, Top = 78, Width = 280, Text = "TextBox" }));

            var cb = TrackCombo(new ComboBox { Left = 314, Top = 78, Width = 240, DropDownStyle = ComboBoxStyle.DropDownList });
            cb.Items.AddRange(new object[] { "Item 1", "Item 2", "A very very long item that should show truncation" });
            cb.SelectedIndex = 0;
            group.Controls.Add(cb);

            group.Controls.Add(TrackControl(new CheckBox { Left = 14, Top = 118, AutoSize = true, Text = "CheckBox", Checked = true }));
            group.Controls.Add(TrackControl(new RadioButton { Left = 14, Top = 146, AutoSize = true, Text = "RadioButton", Checked = true }));

            group.Controls.Add(new Label
            {
                Left = 580,
                Top = 32,
                Width = 380,
                Tag = "LongTextTarget",
                Text = "Short text label"
            });

            root.Controls.Add(group);
            return page;
        }

        private TabPage BuildButtonsTab()
        {
            var page = new TabPage("Buttons");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };
            page.Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text = "Buttons & States",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                AutoSize = true
            });

            var row1 = new FlowLayoutPanel { Top = 50, Left = 0, AutoSize = true, WrapContents = true };
            row1.Controls.Add(TrackControl(new Button { Text = "Primary", Width = 170, Height = 38 }));
            row1.Controls.Add(TrackControl(new Button { Text = "Secondary", Width = 170, Height = 38 }));
            row1.Controls.Add(TrackControl(new Button { Text = "Disabled", Width = 170, Height = 38, Enabled = false }));
            row1.Controls.Add(TrackControl(new Button { Text = "Default (Enter)", Width = 170, Height = 38 }));
            root.Controls.Add(row1);

            var row2 = new FlowLayoutPanel { Top = row1.Bottom + 12, Left = 0, AutoSize = true, WrapContents = true };
            row2.Controls.Add(TrackControl(new Button
            {
                Text = "Settings",
                Width = 170,
                Height = 38,
                Image = _iconImages.Images.ContainsKey("Gear") ? _iconImages.Images["Gear"] : SystemIcons.Application.ToBitmap(),
                TextImageRelation = TextImageRelation.ImageBeforeText
            }));
            row2.Controls.Add(TrackControl(new Button
            {
                Text = "Search",
                Width = 170,
                Height = 38,
                Image = _iconImages.Images.ContainsKey("Search") ? _iconImages.Images["Search"] : SystemIcons.Application.ToBitmap(),
                TextImageRelation = TextImageRelation.ImageBeforeText
            }));
            row2.Controls.Add(TrackControl(new Button
            {
                Text = "Close",
                Width = 170,
                Height = 38,
                Image = _iconImages.Images.ContainsKey("Close") ? _iconImages.Images["Close"] : SystemIcons.Application.ToBitmap(),
                TextImageRelation = TextImageRelation.ImageBeforeText
            }));
            row2.Controls.Add(TrackControl(new Button
            {
                Text = "Icon only",
                Width = 52,
                Height = 38,
                Image = _iconImages.Images.ContainsKey("Gear") ? _iconImages.Images["Gear"] : SystemIcons.Application.ToBitmap()
            }));
            root.Controls.Add(row2);

            var row3 = new FlowLayoutPanel { Top = row2.Bottom + 12, Left = 0, AutoSize = true, WrapContents = true };
            row3.Controls.Add(TrackControl(new CheckBox { Text = "Toggle A", Appearance = Appearance.Button, AutoSize = true, Padding = new Padding(10, 8, 10, 8) }));
            row3.Controls.Add(TrackControl(new CheckBox { Text = "Toggle B", Appearance = Appearance.Button, AutoSize = true, Padding = new Padding(10, 8, 10, 8), Checked = true }));
            row3.Controls.Add(TrackControl(new CheckBox { Text = "Toggle Disabled", Appearance = Appearance.Button, AutoSize = true, Padding = new Padding(10, 8, 10, 8), Enabled = false }));
            root.Controls.Add(row3);

            var row4 = new FlowLayoutPanel { Top = row3.Bottom + 12, Left = 0, AutoSize = true, WrapContents = true };
            row4.Controls.Add(TrackControl(new Button { Text = "Small", Width = 110, Height = 30 }));
            row4.Controls.Add(TrackControl(new Button { Text = "Large", Width = 220, Height = 48 }));
            row4.Controls.Add(TrackControl(new LinkLabel { Text = "LinkLabel example", AutoSize = true, Margin = new Padding(10, 12, 10, 0) }));
            root.Controls.Add(row4);

            var ctxBtn = TrackControl(new Button { Text = "Context menu", Width = 170, Height = 38 });
            var ctx = new ContextMenuStrip();
            ctx.Items.Add("Action 1");
            ctx.Items.Add("Action 2");
            ctx.Items.Add(new ToolStripSeparator());
            ctx.Items.Add("Danger action");
            ctxBtn.Click += (_, __) => ctx.Show(ctxBtn, new Point(0, ctxBtn.Height));

            var row5 = new FlowLayoutPanel { Top = row4.Bottom + 12, Left = 0, AutoSize = true, WrapContents = true };
            row5.Controls.Add(ctxBtn);
            row5.Controls.Add(TrackControl(new Button { Text = "Another action…", Width = 170, Height = 38 }));
            root.Controls.Add(row5);

            return page;
        }

        private TabPage BuildInputsTab()
        {
            var page = new TabPage("Inputs");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };
            page.Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text = "Inputs (including edge cases)",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                AutoSize = true
            });

            var grid = new TableLayoutPanel
            {
                Top = 50,
                Left = 0,
                AutoSize = true,
                ColumnCount = 2
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 560));

            int row = 0;
            void Add(string label, Control c)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var lbl = new Label { Text = label, AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
                grid.Controls.Add(lbl, 0, row);
                grid.Controls.Add(c, 1, row);
                row++;
            }

            Add("TextBox (normal)", TrackTextInput(new TextBox { Width = 460, Text = "Normal TextBox" }));
            Add("TextBox (empty)", TrackTextInput(new TextBox { Width = 460, Text = "" }));
            Add("TextBox (readonly)", TrackTextInput(new TextBox { Width = 460, Text = "ReadOnly TextBox", ReadOnly = true }));
            Add("TextBox (password)", TrackTextInput(new TextBox { Width = 460, Text = "secret", UseSystemPasswordChar = true }));
            Add("TextBox (multiline)", TrackTextInput(new TextBox { Width = 460, Height = 90, Multiline = true, ScrollBars = ScrollBars.Vertical, Text = "Multiline...\r\nLine 2\r\nLine 3" }));
            Add("MaskedTextBox (date)", TrackTextInput(new MaskedTextBox("00/00/0000") { Width = 160, Text = "01012025" }));

            var combo = TrackCombo(new ComboBox { Width = 460, DropDownStyle = ComboBoxStyle.DropDownList });
            combo.Items.AddRange(new object[] { "Short item", "Medium item", "A very very long item that should show truncation / ellipsis behavior in dropdown" });
            combo.SelectedIndex = 0;
            Add("ComboBox (DropDownList)", combo);

            var editableCombo = TrackCombo(new ComboBox { Width = 460, DropDownStyle = ComboBoxStyle.DropDown });
            editableCombo.Items.AddRange(new object[] { "Editable 1", "Editable 2", "Editable 3" });
            editableCombo.Text = "Type here…";
            Add("ComboBox (editable)", editableCombo);

            Add("NumericUpDown (-1000..1000)", TrackNumeric(new NumericUpDown { Width = 160, Minimum = -1000, Maximum = 1000, Value = 5 }));
            Add("DateTimePicker", TrackDate(new DateTimePicker { Width = 240 }));
            Add("RichTextBox", TrackTextInput(new RichTextBox { Width = 460, Height = 110, Text = "RichTextBox\n- bullets\n- styles (WinForms default)" }));

            var toggles = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
            toggles.Controls.Add(TrackControl(new CheckBox { Text = "CheckBox", AutoSize = true, Checked = true }));
            toggles.Controls.Add(TrackControl(new RadioButton { Text = "Radio A", AutoSize = true, Checked = true }));
            toggles.Controls.Add(TrackControl(new RadioButton { Text = "Radio B", AutoSize = true }));
            Add("Toggles", toggles);

            root.Controls.Add(grid);

            root.Controls.Add(new Label
            {
                Top = grid.Bottom + 20,
                Left = 0,
                Width = 980,
                Tag = "LongTextTarget",
                Text = "Short text label"
            });

            return page;
        }

        private TabPage BuildLayoutsTab()
        {
            var page = new TabPage("Layouts");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };
            page.Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text = "Layouts & density stress tests",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                AutoSize = true
            });

            var outerSplit = new SplitContainer
            {
                Top = 50,
                Left = 0,
                Width = 1200,
                Height = 540,
                SplitterDistance = 560
            };

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                Padding = new Padding(10)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            for (int i = 0; i < 6; i++)
                table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            table.Controls.Add(new Label { Text = "Label", AutoSize = true }, 0, 0);
            table.Controls.Add(TrackTextInput(new TextBox { Width = 260, Text = "Aligned input" }), 1, 0);

            table.Controls.Add(new Label { Text = "Combo", AutoSize = true }, 0, 1);
            var cb = TrackCombo(new ComboBox { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList });
            cb.Items.AddRange(new object[] { "A", "B", "C" });
            cb.SelectedIndex = 0;
            table.Controls.Add(cb, 1, 1);

            table.Controls.Add(new Label { Text = "Checkbox", AutoSize = true }, 0, 2);
            table.Controls.Add(TrackControl(new CheckBox { Text = "Enable something", AutoSize = true, Checked = true }), 1, 2);

            table.Controls.Add(new Label { Text = "Buttons", AutoSize = true }, 0, 3);
            var btnRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            btnRow.Controls.Add(TrackControl(new Button { Text = "OK", Width = 90, Height = 32 }));
            btnRow.Controls.Add(TrackControl(new Button { Text = "Cancel", Width = 90, Height = 32 }));
            btnRow.Controls.Add(TrackControl(new Button { Text = "Apply", Width = 90, Height = 32 }));
            table.Controls.Add(btnRow, 1, 3);

            table.Controls.Add(new Label { Text = "Progress", AutoSize = true }, 0, 4);
            var pb = TrackControl(new ThemedProgressBar { Width = 260, Height = 20, Value = 45, ShowPercentText = true });
            table.Controls.Add(pb, 1, 4);

            table.Controls.Add(new Label { Text = "Long label", AutoSize = true }, 0, 5);
            table.Controls.Add(new Label { Tag = "LongTextTarget", Text = "Short text label", AutoSize = true }, 1, 5);

            outerSplit.Panel1.Controls.Add(table);

            var rightPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                Padding = new Padding(10)
            };

            for (int i = 1; i <= 18; i++)
                flow.Controls.Add(TrackControl(new Button { Text = $"Button {i}", Width = 160, Height = 36 }));

            var gbOuter = new GroupBox { Text = "GroupBox Outer", Width = 520, Height = 240, Padding = new Padding(10) };
            var gbInner = new GroupBox { Text = "GroupBox Inner", Width = 480, Height = 170, Left = 12, Top = 28, Padding = new Padding(10) };
            gbInner.Controls.Add(TrackTextInput(new TextBox { Width = 320, Left = 12, Top = 30, Text = "Nested input" }));
            gbInner.Controls.Add(TrackControl(new Button { Width = 160, Height = 34, Left = 12, Top = 68, Text = "Nested action" }));
            gbOuter.Controls.Add(gbInner);

            flow.Controls.Add(gbOuter);

            rightPanel.Controls.Add(flow);
            outerSplit.Panel2.Controls.Add(rightPanel);

            root.Controls.Add(outerSplit);
            return page;
        }

        private TabPage BuildListsAndDataTab()
        {
            var page = new TabPage("Lists & Data");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14) };
            page.Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text = "Lists, Trees, ListView, DataGridView (with density tests)",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                AutoSize = true
            });

            var split = new SplitContainer
            {
                Top = 50,
                Left = 0,
                Dock = DockStyle.Fill,
                SplitterDistance = 560
            };
            root.Controls.Add(split);

            var left = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 320 };
            split.Panel1.Controls.Add(left);

            var tree = TrackControl(new TreeView { Dock = DockStyle.Fill });

            var treeIcons = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
            treeIcons.Images.Add("folder", SystemIcons.Application.ToBitmap());
            var gearImg = _iconImages.Images.ContainsKey("Gear") ? _iconImages.Images["Gear"] : null;
            treeIcons.Images.Add("gear", gearImg ?? SystemIcons.Application.ToBitmap());
            tree.ImageList = treeIcons;

            var rootNode = new TreeNode("Root") { ImageKey = "folder", SelectedImageKey = "folder" };
            rootNode.Nodes.Add(new TreeNode("Settings") { ImageKey = "gear", SelectedImageKey = "gear" });
            rootNode.Nodes.Add(new TreeNode("Logs"));
            rootNode.Nodes.Add(new TreeNode("Reports"));
            tree.Nodes.Add(rootNode);
            tree.ExpandAll();

            var listBox = TrackControl(new ListBox { Dock = DockStyle.Fill });
            listBox.Items.AddRange(Enumerable.Range(1, 60).Select(i => $"List item {i}").ToArray());

            left.Panel1.Controls.Add(tree);
            left.Panel2.Controls.Add(listBox);

            var right = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 260 };
            split.Panel2.Controls.Add(right);

            var listDetails = TrackControl(new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true
            });
            listDetails.Columns.Add("Name", 220);
            listDetails.Columns.Add("Status", 120);
            listDetails.Columns.Add("Notes", 420);

            for (int i = 1; i <= 40; i++)
            {
                var item = new ListViewItem($"Device {i:00}");
                item.SubItems.Add(i % 3 == 0 ? "Warning" : (i % 5 == 0 ? "Error" : "OK"));
                item.SubItems.Add("Some details / notes…");
                listDetails.Items.Add(item);
            }

            var grid = TrackControl(new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                RowHeadersVisible = false
            });
            grid.Columns.Add("col1", "Key");
            grid.Columns.Add("col2", "Value");
            grid.Columns.Add("col3", "Description");

            for (int i = 1; i <= 60; i++)
                grid.Rows.Add($"Setting_{i:00}", $"Value {i}", i % 7 == 0 ? "Long description to test wrapping/ellipsis behavior" : "Short");

            right.Panel1.Controls.Add(listDetails);
            right.Panel2.Controls.Add(grid);

            return page;
        }

        private TabPage BuildSystemTab()
        {
            var page = new TabPage("System");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };
            page.Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text = "System dialogs & common UI",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                AutoSize = true
            });

            var row = new FlowLayoutPanel
            {
                Top = 50,
                Left = 0,
                AutoSize = true,
                WrapContents = true
            };

            Button Make(string text, Action onClick)
            {
                var b = TrackControl(new Button { Text = text, Width = 220, Height = 38 });
                b.Click += (_, __) => onClick();
                return b;
            }

            row.Controls.Add(Make("MessageBox Info", () => MessageBox.Show(this, "Information message.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information)));
            row.Controls.Add(Make("MessageBox Warning", () => MessageBox.Show(this, "Warning message.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
            row.Controls.Add(Make("MessageBox Error", () => MessageBox.Show(this, "Error message.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
            row.Controls.Add(Make("MessageBox Yes/No", () => MessageBox.Show(this, "Continue?", "Question", MessageBoxButtons.YesNo, MessageBoxIcon.Question)));

            row.Controls.Add(Make("OpenFileDialog", () => { using var d = new OpenFileDialog(); d.ShowDialog(this); }));
            row.Controls.Add(Make("SaveFileDialog", () => { using var d = new SaveFileDialog(); d.ShowDialog(this); }));
            row.Controls.Add(Make("FolderBrowserDialog", () => { using var d = new FolderBrowserDialog(); d.ShowDialog(this); }));
            row.Controls.Add(Make("ColorDialog", () => { using var d = new ColorDialog(); d.ShowDialog(this); }));
            row.Controls.Add(Make("FontDialog", () => { using var d = new FontDialog(); d.ShowDialog(this); }));

            root.Controls.Add(row);

            root.Controls.Add(new Label
            {
                Top = row.Bottom + 16,
                Left = 0,
                Width = 980,
                Tag = "LongTextTarget",
                Text = "Short text label"
            });

            return page;
        }

        private TabPage BuildProgressAndFeedbackTab(out ThemedProgressBar themedA, out ThemedProgressBar themedB)
        {
            // IMPORTANT: use locals in lambdas, then assign OUT at end (CS1628 safe)
            var page = new TabPage("Progress");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };
            page.Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text = "Progress & Feedback",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                AutoSize = true
            });

            var themedALocal = TrackControl(new ThemedProgressBar { Width = 520, Height = 22, Value = 35, ShowPercentText = true });
            var themedBLocal = TrackControl(new ThemedProgressBar { Width = 520, Height = 22, Value = 65, ShowPercentText = true });

            var native = TrackControl(new ProgressBar { Width = 520, Height = 22, Value = 35 });
            var marquee = TrackControl(new ProgressBar { Width = 520, Height = 22, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25 });

            var track = TrackControl(new TrackBar { Width = 520, Minimum = 0, Maximum = 100, Value = 35, TickFrequency = 10 });
            track.ValueChanged += (_, __) =>
            {
                themedALocal.Value = track.Value;
                native.Value = track.Value;
            };

            var stack = new FlowLayoutPanel
            {
                Top = 50,
                Left = 0,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };

            stack.Controls.Add(new Label { Text = "Themed Progress A (animated)", AutoSize = true });
            stack.Controls.Add(themedALocal);

            stack.Controls.Add(new Label { Text = "Themed Progress B (animated)", AutoSize = true, Padding = new Padding(0, 10, 0, 0) });
            stack.Controls.Add(themedBLocal);

            stack.Controls.Add(new Label { Text = "Native ProgressBar", AutoSize = true, Padding = new Padding(0, 10, 0, 0) });
            stack.Controls.Add(native);

            stack.Controls.Add(new Label { Text = "Indeterminate (Marquee)", AutoSize = true, Padding = new Padding(0, 10, 0, 0) });
            stack.Controls.Add(marquee);

            stack.Controls.Add(new Label { Text = "TrackBar (drives Native + Themed A)", AutoSize = true, Padding = new Padding(0, 10, 0, 0) });
            stack.Controls.Add(track);

            var statusRow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Padding = new Padding(0, 14, 0, 0) };
            statusRow.Controls.Add(TrackControl(new Label { Text = "Status: OK", AutoSize = true, Padding = new Padding(10, 6, 10, 6), BorderStyle = BorderStyle.FixedSingle }));
            statusRow.Controls.Add(TrackControl(new Label { Text = "Status: Warning", AutoSize = true, Padding = new Padding(10, 6, 10, 6), BorderStyle = BorderStyle.FixedSingle }));
            statusRow.Controls.Add(TrackControl(new Label { Text = "Status: Error", AutoSize = true, Padding = new Padding(10, 6, 10, 6), BorderStyle = BorderStyle.FixedSingle }));
            stack.Controls.Add(statusRow);

            root.Controls.Add(stack);

            themedA = themedALocal;
            themedB = themedBLocal;
            return page;
        }

        private TabPage BuildTypographyTab()
        {
            var page = new TabPage("Typography");
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = true };
            page.Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text = "Typography & text rendering",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                AutoSize = true
            });

            var sampleA = new Label { Text = "Heading (Bold 14)", AutoSize = true, Font = new Font("Segoe UI", 14f, FontStyle.Bold), Top = 50, Left = 0 };
            var sampleB = new Label { Text = "Subheading (Bold 11)", AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold), Top = sampleA.Bottom + 10, Left = 0 };
            var sampleC = new Label { Text = "Body (Regular 9)", AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Regular), Top = sampleB.Bottom + 10, Left = 0 };
            var sampleD = new Label { Text = "Monospace: 0123 ABC xyz", AutoSize = true, Font = new Font("Consolas", 10f, FontStyle.Regular), Top = sampleC.Bottom + 10, Left = 0 };

            root.Controls.Add(sampleA);
            root.Controls.Add(sampleB);
            root.Controls.Add(sampleC);
            root.Controls.Add(sampleD);

            root.Controls.Add(new Label
            {
                Top = sampleD.Bottom + 16,
                Left = 0,
                Width = 980,
                Tag = "LongTextTarget",
                Text = "Short text label"
            });

            root.Controls.Add(TrackTextInput(new TextBox
            {
                Top = sampleD.Bottom + 46,
                Left = 0,
                Width = 980,
                Height = 140,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Text = "Wrap test:\r\n\r\n- Check line spacing\r\n- Check scrollbar contrast\r\n- Check selection colors\r\n"
            }));

            return page;
        }

        // =========================
        // Simple drawn icons (white shapes; theme recolor later if desired)
        // =========================
        private static Bitmap CreateGearIcon(int w, int h)
        {
            var bmp = new Bitmap(w, h);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var pen = new Pen(Color.White, 2f);
            using var brush = new SolidBrush(Color.White);

            float cx = w / 2f, cy = h / 2f;
            float rOuter = Math.Min(w, h) * 0.42f;
            float rInner = Math.Min(w, h) * 0.18f;

            for (int i = 0; i < 8; i++)
            {
                float a = (float)(i * Math.PI / 4);
                float tx = cx + (float)Math.Cos(a) * rOuter;
                float ty = cy + (float)Math.Sin(a) * rOuter;
                var rect = new RectangleF(tx - 2.5f, ty - 2.5f, 5f, 5f);
                g.FillRectangle(brush, rect);
            }

            g.DrawEllipse(pen, cx - rOuter, cy - rOuter, rOuter * 2, rOuter * 2);
            g.FillEllipse(brush, cx - rInner, cy - rInner, rInner * 2, rInner * 2);

            // hole (transparent)
            g.CompositingMode = CompositingMode.SourceCopy;
            using var holeBrush = new SolidBrush(Color.Transparent);
            g.FillEllipse(holeBrush, cx - rInner * 0.55f, cy - rInner * 0.55f, rInner * 1.1f, rInner * 1.1f);

            return bmp;
        }

        private static Bitmap CreatePlusIcon(int w, int h)
        {
            var bmp = new Bitmap(w, h);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var pen = new Pen(Color.White, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            float cx = w / 2f, cy = h / 2f;
            g.DrawLine(pen, cx, 4, cx, h - 4);
            g.DrawLine(pen, 4, cy, w - 4, cy);

            return bmp;
        }

        private static Bitmap CreateSearchIcon(int w, int h)
        {
            var bmp = new Bitmap(w, h);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var pen = new Pen(Color.White, 2.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

            g.DrawEllipse(pen, 4, 4, 12, 12);
            g.DrawLine(pen, 14, 14, 20, 20);

            return bmp;
        }

        private static Bitmap CreateCloseIcon(int w, int h)
        {
            var bmp = new Bitmap(w, h);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var pen = new Pen(Color.White, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, 5, 5, w - 5, h - 5);
            g.DrawLine(pen, w - 5, 5, 5, h - 5);

            return bmp;
        }

        private static Bitmap CreateCheckIcon(int w, int h)
        {
            var bmp = new Bitmap(w, h);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var pen = new Pen(Color.White, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLines(pen, new[]
            {
                new Point(4, h/2),
                new Point(w/2 - 2, h - 6),
                new Point(w - 4, 6)
            });

            return bmp;
        }
    }
}
