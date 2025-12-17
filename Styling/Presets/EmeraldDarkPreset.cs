using System.Drawing;

namespace StruxureGuard.Styling.Presets
{
    public static class EmeraldDarkPreset
    {
        public static ThemeSettings Create() => new ThemeSettings
        {
            Name = "EmeraldDark",

            AppBack = Color.FromArgb(18, 20, 22),
            Surface = Color.FromArgb(26, 29, 33),
            Text = Color.FromArgb(230, 233, 236),
            MutedText = Color.FromArgb(160, 165, 170),

            Accent = Color.FromArgb(0, 200, 140),
            Border = Color.FromArgb(58, 64, 70),

            InputBack = Color.FromArgb(34, 38, 43),
            InputText = Color.FromArgb(235, 238, 240),

            ButtonBack = Color.FromArgb(40, 46, 52),
            ButtonText = Color.FromArgb(235, 238, 240),
            ButtonDisabledBack = Color.FromArgb(33, 36, 40),
            ButtonDisabledText = Color.FromArgb(120, 125, 130),

            ListBack = Color.FromArgb(26, 29, 33),
            ListText = Color.FromArgb(235, 238, 240),
            SelectionBack = Color.FromArgb(0, 200, 140),
            SelectionText = Color.White,

            StripBack = Color.FromArgb(22, 25, 28),
            StripText = Color.FromArgb(235, 238, 240),
            StripBorder = Color.FromArgb(58, 64, 70),
            StripHover = Color.FromArgb(34, 38, 43),
            StripPressed = Color.FromArgb(30, 34, 38),

            ProgressTrack = Color.FromArgb(34, 38, 43),
            ProgressText = Color.White,

            FontFamily = "Segoe UI",
            FontSize = 9f
        };
    }
}
