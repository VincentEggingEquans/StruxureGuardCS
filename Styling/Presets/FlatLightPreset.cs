using System.Drawing;

namespace StruxureGuard.Styling.Presets
{
    public static class FlatLightPreset
    {
        public static ThemeSettings Create() => new ThemeSettings
        {
            Name = "FlatLight",

            AppBack = Color.White,
            Surface = Color.FromArgb(245, 245, 245),
            Text = Color.FromArgb(25, 25, 25),
            MutedText = Color.FromArgb(90, 90, 90),

            Accent = Color.FromArgb(0, 122, 204),
            Border = Color.FromArgb(210, 210, 210),

            InputBack = Color.White,
            InputText = Color.FromArgb(25, 25, 25),

            ButtonBack = Color.FromArgb(235, 235, 235),
            ButtonText = Color.FromArgb(25, 25, 25),
            ButtonDisabledBack = Color.FromArgb(240, 240, 240),
            ButtonDisabledText = Color.FromArgb(150, 150, 150),

            ListBack = Color.White,
            ListText = Color.FromArgb(25, 25, 25),
            SelectionBack = Color.FromArgb(0, 122, 204),
            SelectionText = Color.White,

            StripBack = Color.FromArgb(250, 250, 250),
            StripText = Color.FromArgb(25, 25, 25),
            StripBorder = Color.FromArgb(210, 210, 210),
            StripHover = Color.FromArgb(235, 235, 235),
            StripPressed = Color.FromArgb(220, 220, 220),

            ProgressTrack = Color.FromArgb(235, 235, 235),
            ProgressText = Color.FromArgb(25, 25, 25),

            FontFamily = "Segoe UI",
            FontSize = 9f
        };
    }
}
