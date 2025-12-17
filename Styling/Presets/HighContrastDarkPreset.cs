using System.Drawing;

namespace StruxureGuard.Styling.Presets
{
    public static class HighContrastDarkPreset
    {
        public static ThemeSettings Create() => new ThemeSettings
        {
            Name = "HighContrastDark",

            AppBack = Color.Black,
            Surface = Color.FromArgb(18, 18, 18),
            Text = Color.White,
            MutedText = Color.FromArgb(200, 200, 200),

            Accent = Color.Yellow,
            Border = Color.White,

            InputBack = Color.Black,
            InputText = Color.White,

            ButtonBack = Color.Black,
            ButtonText = Color.White,
            ButtonDisabledBack = Color.FromArgb(30, 30, 30),
            ButtonDisabledText = Color.FromArgb(160, 160, 160),

            ListBack = Color.Black,
            ListText = Color.White,
            SelectionBack = Color.Yellow,
            SelectionText = Color.Black,

            StripBack = Color.Black,
            StripText = Color.White,
            StripBorder = Color.White,
            StripHover = Color.FromArgb(35, 35, 35),
            StripPressed = Color.FromArgb(55, 55, 55),

            ProgressTrack = Color.FromArgb(35, 35, 35),
            ProgressText = Color.Black,

            FontFamily = "Segoe UI",
            FontSize = 9f
        };
    }
}
