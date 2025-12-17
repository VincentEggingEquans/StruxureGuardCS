using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StruxureGuard.UI.Controls
{
    public class ThemedProgressBar : Control
    {
        private int _min = 0, _max = 100, _value = 0;

        [Browsable(true)] public Color TrackColor { get; set; } = Color.FromArgb(45, 45, 48);
        [Browsable(true)] public Color BarColor { get; set; } = Color.FromArgb(0, 122, 204);
        [Browsable(true)] public Color BorderColor { get; set; } = Color.FromArgb(70, 70, 74);
        [Browsable(true)] public Color TextColor { get; set; } = Color.White;

        [Browsable(true)] public bool ShowPercentText { get; set; } = true;
        [Browsable(true)] public int CornerRadius { get; set; } = 6;
        [Browsable(true)] public int BorderThickness { get; set; } = 1;

        public int Minimum { get => _min; set { _min = value; if (_max < _min) _max = _min; Invalidate(); } }
        public int Maximum { get => _max; set { _max = Math.Max(value, _min); if (_value > _max) _value = _max; Invalidate(); } }
        public int Value { get => _value; set { _value = Math.Max(_min, Math.Min(_max, value)); Invalidate(); } }

        public int Percent => (_max <= _min) ? 0 : (int)Math.Round((Value - Minimum) * 100.0 / (Maximum - Minimum));

        public ThemedProgressBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);

            Size = new Size(220, 18);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            using var borderPath = RoundedRect(rect, CornerRadius);
            using var borderPen = new Pen(BorderColor, BorderThickness);

            var inner = Rectangle.Inflate(rect, -BorderThickness, -BorderThickness);
            if (inner.Width <= 0 || inner.Height <= 0) return;

            using var trackPath = RoundedRect(inner, Math.Max(0, CornerRadius - BorderThickness));
            using var trackBrush = new SolidBrush(TrackColor);
            e.Graphics.FillPath(trackBrush, trackPath);

            float pct = (_max <= _min) ? 0f : (float)(Value - Minimum) / (Maximum - Minimum);
            int fillW = (int)Math.Round(inner.Width * pct);

            if (fillW > 0)
            {
                var fillRect = new Rectangle(inner.X, inner.Y, fillW, inner.Height);
                using var fillPath = RoundedRect(fillRect, Math.Max(0, CornerRadius - BorderThickness),
                    leftRounded: true, rightRounded: pct >= 0.999f);
                using var barBrush = new SolidBrush(BarColor);
                e.Graphics.FillPath(barBrush, fillPath);
            }

            e.Graphics.DrawPath(borderPen, borderPath);

            if (ShowPercentText)
            {
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                using var tb = new SolidBrush(TextColor);
                e.Graphics.DrawString($"{Percent}%", Font, tb, inner, sf);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius, bool leftRounded = true, bool rightRounded = true)
        {
            var path = new GraphicsPath();
            int d = radius * 2;

            if (radius <= 0)
            {
                path.AddRectangle(rect);
                path.CloseFigure();
                return path;
            }

            int x = rect.X, y = rect.Y, w = rect.Width, h = rect.Height;

            if (leftRounded) path.AddArc(x, y, d, d, 180, 90);
            else path.AddLine(x, y, x, y);

            path.AddLine(x + (leftRounded ? radius : 0), y, x + w - (rightRounded ? radius : 0), y);

            if (rightRounded) path.AddArc(x + w - d, y, d, d, 270, 90);
            else path.AddLine(x + w, y, x + w, y);

            path.AddLine(x + w, y + (rightRounded ? radius : 0), x + w, y + h - (rightRounded ? radius : 0));

            if (rightRounded) path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            else path.AddLine(x + w, y + h, x + w, y + h);

            path.AddLine(x + w - (rightRounded ? radius : 0), y + h, x + (leftRounded ? radius : 0), y + h);

            if (leftRounded) path.AddArc(x, y + h - d, d, d, 90, 90);
            else path.AddLine(x, y + h, x, y + h);

            path.AddLine(x, y + h - (leftRounded ? radius : 0), x, y + (leftRounded ? radius : 0));

            path.CloseFigure();
            return path;
        }
    }
}
