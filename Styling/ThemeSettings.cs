using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text.Json.Serialization;

namespace StruxureGuard.Styling
{
    public sealed class ThemeSettings
    {
        public string Name { get; set; } = "Unnamed";

        // Base
        public Color AppBack { get; set; }
        public Color Surface { get; set; }
        public Color Text { get; set; }
        public Color MutedText { get; set; }

        // Accent / borders
        public Color Accent { get; set; }
        public Color Border { get; set; }

        // Inputs
        public Color InputBack { get; set; }
        public Color InputText { get; set; }

        // Buttons
        public Color ButtonBack { get; set; }
        public Color ButtonText { get; set; }
        public Color ButtonDisabledBack { get; set; }
        public Color ButtonDisabledText { get; set; }

        // Lists/grids
        public Color ListBack { get; set; }
        public Color ListText { get; set; }
        public Color SelectionBack { get; set; }
        public Color SelectionText { get; set; }

        // Strips
        public Color StripBack { get; set; }
        public Color StripText { get; set; }
        public Color StripBorder { get; set; }
        public Color StripHover { get; set; }
        public Color StripPressed { get; set; }

        // Progress
        public Color ProgressTrack { get; set; }
        public Color ProgressText { get; set; }

        // Font
        public string FontFamily { get; set; } = "Segoe UI";
        public float FontSize { get; set; } = 9f;

        /// <summary>
        /// Per-control overrides.
        /// Key = ThemeKey.ForControl(control) (preferred) OR legacy key = Control.Name.
        /// Stored case-insensitively.
        /// </summary>
        [JsonInclude]
        public Dictionary<string, ControlOverride> Overrides { get; private set; }
            = new(StringComparer.OrdinalIgnoreCase);

        public ThemeSettings Clone()
        {
            var copy = (ThemeSettings)MemberwiseClone();

            // Deep-copy overrides
            copy.Overrides = new Dictionary<string, ControlOverride>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in Overrides)
                copy.Overrides[kv.Key] = kv.Value.Clone();

            return copy;
        }

        public sealed class ControlOverride
        {
            [Category("Base")]
            public Color? BackColor { get; set; }

            [Category("Base")]
            public Color? ForeColor { get; set; }

            [Category("Base")]
            public string? FontFamily { get; set; }

            [Category("Base")]
            public float? FontSize { get; set; }

            [Category("Buttons")]
            public Color? BorderColor { get; set; }

            [Category("Buttons")]
            public Color? ButtonBack { get; set; }

            [Category("Buttons")]
            public Color? ButtonText { get; set; }

            [Category("Progress")]
            public Color? ProgressTrack { get; set; }

            [Category("Progress")]
            public Color? ProgressBar { get; set; }

            [Category("Progress")]
            public Color? ProgressText { get; set; }

            public ControlOverride Clone() => (ControlOverride)MemberwiseClone();
        }
    }
}
