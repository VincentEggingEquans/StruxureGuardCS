using System.Drawing;

namespace StruxureGuard.Styling.Presets
{
    public static class SolarizedDarkPreset
    {
        public static ThemeSettings Create() => new ThemeSettings
        {
            Name = "SolarizedDark",

            AppBack = Color.FromArgb(0, 43, 54),
            Surface = Color.FromArgb(7, 54, 66),
            Text = Color.FromArgb(238, 232, 213),
            MutedText = Color.FromArgb(147, 161, 161),

            Accent = Color.FromArgb(38, 139, 210),
            Border = Color.FromArgb(88, 110, 117),

            InputBack = Color.FromArgb(0, 43, 54),
            InputText = Color.FromArgb(238, 232, 213),

            ButtonBack = Color.FromArgb(10, 66, 80),
            ButtonText = Color.FromArgb(238, 232, 213),
            ButtonDisabledBack = Color.FromArgb(6, 50, 60),
            ButtonDisabledText = Color.FromArgb(120, 135, 140),

            ListBack = Color.FromArgb(7, 54, 66),
            ListText = Color.FromArgb(238, 232, 213),
            SelectionBack = Color.FromArgb(38, 139, 210),
            SelectionText = Color.White,

            StripBack = Color.FromArgb(0, 43, 54),
            StripText = Color.FromArgb(238, 232, 213),
            StripBorder = Color.FromArgb(88, 110, 117),
            StripHover = Color.FromArgb(10, 66, 80),
            StripPressed = Color.FromArgb(6, 50, 60),

            ProgressTrack = Color.FromArgb(10, 66, 80),
            ProgressText = Color.White,

            FontFamily = "Segoe UI",
            FontSize = 9f
        };
    }
}
