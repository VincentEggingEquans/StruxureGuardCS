using System.Drawing;

namespace StruxureGuard.Styling.Presets
{
    public static class NordPreset
    {
        public static ThemeSettings Create() => new ThemeSettings
        {
            Name = "Nord",

            AppBack = Color.FromArgb(33, 38, 46),
            Surface = Color.FromArgb(40, 46, 58),
            Text = Color.FromArgb(236, 239, 244),
            MutedText = Color.FromArgb(180, 186, 198),

            Accent = Color.FromArgb(136, 192, 208),
            Border = Color.FromArgb(59, 66, 82),

            InputBack = Color.FromArgb(47, 54, 68),
            InputText = Color.FromArgb(236, 239, 244),

            ButtonBack = Color.FromArgb(59, 66, 82),
            ButtonText = Color.FromArgb(236, 239, 244),
            ButtonDisabledBack = Color.FromArgb(50, 56, 70),
            ButtonDisabledText = Color.FromArgb(140, 145, 155),

            ListBack = Color.FromArgb(40, 46, 58),
            ListText = Color.FromArgb(236, 239, 244),
            SelectionBack = Color.FromArgb(136, 192, 208),
            SelectionText = Color.FromArgb(33, 38, 46),

            StripBack = Color.FromArgb(46, 52, 64),
            StripText = Color.FromArgb(236, 239, 244),
            StripBorder = Color.FromArgb(59, 66, 82),
            StripHover = Color.FromArgb(55, 62, 77),
            StripPressed = Color.FromArgb(50, 56, 70),

            ProgressTrack = Color.FromArgb(47, 54, 68),
            ProgressText = Color.White,

            FontFamily = "Segoe UI",
            FontSize = 9f
        };
    }
}
