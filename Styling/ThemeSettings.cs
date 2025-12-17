using System.Drawing;

namespace StruxureGuard.Styling
{
    public sealed class ThemeSettings
    {
        public string Name { get; init; } = "Unnamed";

        // Base
        public Color AppBack { get; init; }
        public Color Surface { get; init; }
        public Color Text { get; init; }
        public Color MutedText { get; init; }

        // Accent / borders
        public Color Accent { get; init; }
        public Color Border { get; init; }

        // Inputs
        public Color InputBack { get; init; }
        public Color InputText { get; init; }

        // Buttons
        public Color ButtonBack { get; init; }
        public Color ButtonText { get; init; }
        public Color ButtonDisabledBack { get; init; }
        public Color ButtonDisabledText { get; init; }

        // Lists/grids
        public Color ListBack { get; init; }
        public Color ListText { get; init; }
        public Color SelectionBack { get; init; }
        public Color SelectionText { get; init; }

        // Strips
        public Color StripBack { get; init; }
        public Color StripText { get; init; }
        public Color StripBorder { get; init; }
        public Color StripHover { get; init; }
        public Color StripPressed { get; init; }

        // Progress
        public Color ProgressTrack { get; init; }
        public Color ProgressText { get; init; }

        // Font
        public string FontFamily { get; init; } = "Segoe UI";
        public float FontSize { get; init; } = 9f;
    }
}
