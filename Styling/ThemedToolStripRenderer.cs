using System.Drawing;
using System.Windows.Forms;

namespace StruxureGuard.Styling
{
    public sealed class ThemedToolStripRenderer : ToolStripProfessionalRenderer
    {
        private readonly ThemeSettings _t;

        public ThemedToolStripRenderer(ThemeSettings t) : base(new ProfessionalColorTable())
        {
            _t = t;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            => e.Graphics.Clear(_t.StripBack);

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var p = new Pen(_t.StripBorder);
            e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var r = new Rectangle(Point.Empty, e.Item.Size);
            Color back = _t.StripBack;
            if (e.Item.Pressed) back = _t.StripPressed;
            else if (e.Item.Selected) back = _t.StripHover;

            using var b = new SolidBrush(back);
            e.Graphics.FillRectangle(b, r);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = _t.StripText;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var p = new Pen(_t.StripBorder);
            int y = e.Item.ContentRectangle.Top + e.Item.ContentRectangle.Height / 2;
            e.Graphics.DrawLine(p, e.Item.ContentRectangle.Left + 4, y, e.Item.ContentRectangle.Right - 4, y);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = _t.StripText;
            base.OnRenderArrow(e);
        }
    }
}
