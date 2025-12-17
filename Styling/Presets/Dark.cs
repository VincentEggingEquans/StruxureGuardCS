using System.Drawing;

namespace StruxureGuard.Styling.Presets
{
    public static class Dark
    {
        public static ThemeSettings Create() => new ThemeSettings
        {            
            Name = "Dark",
            AppBack = Color.FromArgb(32, 32, 32),
            Surface = Color.FromArgb(40, 40, 40),
            Text = Color.White,
            MutedText = Color.LightGray,
            Accent = Color.DeepSkyBlue,
            Border = Color.FromArgb(70, 70, 70),

            InputBack = Color.FromArgb(50, 50, 50),
            InputText = Color.White,

            ButtonBack = Color.FromArgb(60, 60, 60),
            ButtonText = Color.White,
            ButtonDisabledBack = Color.FromArgb(45, 45, 45),
            ButtonDisabledText = Color.Gray,

            ListBack = Color.FromArgb(45, 45, 45),
            ListText = Color.White,
            SelectionBack = Color.DeepSkyBlue,
            SelectionText = Color.Black,

            StripBack = Color.FromArgb(28, 28, 28),
            StripText = Color.White,
            StripBorder = Color.FromArgb(70, 70, 70),
            StripHover = Color.FromArgb(55, 55, 55),
            StripPressed = Color.FromArgb(65, 65, 65),

            ProgressTrack = Color.FromArgb(80, 80, 80),
            ProgressText = Color.White,

            FontFamily = "Segoe UI",
            FontSize = 9f
        };
    }
}
