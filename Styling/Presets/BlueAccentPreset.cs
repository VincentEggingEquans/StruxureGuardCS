using System.Drawing;

namespace StruxureGuard.Styling.Presets
{
    public static class BlueAccentPreset
    {
        public static ThemeSettings Create()
        {
            var baseDark = FlatDarkPreset.Create();
            return new ThemeSettings
            {
                Name = "BlueAccent",
                AppBack = baseDark.AppBack,
                Surface = baseDark.Surface,
                Text = baseDark.Text,
                MutedText = baseDark.MutedText,

                Accent = Color.FromArgb(0, 153, 255),
                Border = baseDark.Border,

                InputBack = baseDark.InputBack,
                InputText = baseDark.InputText,

                ButtonBack = baseDark.ButtonBack,
                ButtonText = baseDark.ButtonText,
                ButtonDisabledBack = baseDark.ButtonDisabledBack,
                ButtonDisabledText = baseDark.ButtonDisabledText,

                ListBack = baseDark.ListBack,
                ListText = baseDark.ListText,
                SelectionBack = Color.FromArgb(0, 153, 255),
                SelectionText = baseDark.SelectionText,

                StripBack = baseDark.StripBack,
                StripText = baseDark.StripText,
                StripBorder = baseDark.StripBorder,
                StripHover = baseDark.StripHover,
                StripPressed = baseDark.StripPressed,

                ProgressTrack = baseDark.ProgressTrack,
                ProgressText = baseDark.ProgressText,

                FontFamily = baseDark.FontFamily,
                FontSize = baseDark.FontSize
            };
        }
    }
}
