using System.Drawing;

namespace StruxureGuard.Styling.Presets
{
    public static class FlatDarkPreset
    {
        public static ThemeSettings Create() => new ThemeSettings
        {
            Name = "FlatDark",

            AppBack = Color.FromArgb(30, 30, 30),
            Surface = Color.FromArgb(37, 37, 38),
            Text = Color.Gainsboro,
            MutedText = Color.FromArgb(160, 160, 160),

            Accent = Color.FromArgb(0, 122, 204),
            Border = Color.FromArgb(70, 70, 74),

            InputBack = Color.FromArgb(45, 45, 48),
            InputText = Color.Gainsboro,

            ButtonBack = Color.FromArgb(63, 63, 70),
            ButtonText = Color.Gainsboro,
            ButtonDisabledBack = Color.FromArgb(50, 50, 52),
            ButtonDisabledText = Color.FromArgb(120, 120, 120),

            ListBack = Color.FromArgb(37, 37, 38),
            ListText = Color.Gainsboro,
            SelectionBack = Color.FromArgb(0, 122, 204),
            SelectionText = Color.White,

            StripBack = Color.FromArgb(45, 45, 48),
            StripText = Color.Gainsboro,
            StripBorder = Color.FromArgb(70, 70, 74),
            StripHover = Color.FromArgb(62, 62, 66),
            StripPressed = Color.FromArgb(55, 55, 58),

            ProgressTrack = Color.FromArgb(45, 45, 48),
            ProgressText = Color.White,

            FontFamily = "Segoe UI",
            FontSize = 9f
        };
    }
}
