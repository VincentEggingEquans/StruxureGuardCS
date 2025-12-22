using System.Drawing;

namespace StruxureGuard.Styling;

public sealed class ThemeControlOverride
{
    public Color? BackColor { get; set; }
    public Color? ForeColor { get; set; }
    public Color? BorderColor { get; set; }

    public string? FontFamily { get; set; }
    public float? FontSize { get; set; }
    public FontStyle? FontStyle { get; set; }

    public void Clear()
    {
        BackColor = null;
        ForeColor = null;
        BorderColor = null;
        FontFamily = null;
        FontSize = null;
        FontStyle = null;
    }
}
