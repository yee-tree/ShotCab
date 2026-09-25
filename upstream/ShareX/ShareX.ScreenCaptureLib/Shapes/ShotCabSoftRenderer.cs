// ShotCab UI additions; upstream GPL license applies.
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace ShareX.ScreenCaptureLib
{
    internal sealed class ShotCabSoftRenderer : ToolStripProfessionalRenderer
    {
        private readonly bool dark;

        // Shared design tokens for the editor chrome (toolbar surface, caption bar, window buttons).
        internal static Color SurfaceOf(bool dark) => dark ? Color.FromArgb(35, 36, 38) : Color.FromArgb(247, 248, 249);
        internal static Color ForegroundOf(bool dark) => dark ? Color.FromArgb(190, 193, 197) : Color.FromArgb(65, 70, 78);
        internal static Color ItemFillOf(bool dark) => SurfaceOf(dark);
        internal static Color ItemHotOf(bool dark) => dark ? Color.FromArgb(57, 61, 64) : Color.FromArgb(226, 230, 233);
        internal static Color AccentOf(bool dark) => dark ? Color.FromArgb(70, 206, 165) : Color.FromArgb(22, 137, 106);
        internal static Font LabelFont() => new Font("Microsoft YaHei UI", 10f);

        internal Color Surface => SurfaceOf(dark);
        internal Color Foreground => ForegroundOf(dark);
        internal Color Accent => AccentOf(dark);
        internal ShotCabSoftRenderer(bool dark) { this.dark = dark; }
        internal static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var path = new GraphicsPath(); int d = System.Math.Min(radius * 2, System.Math.Min(r.Width, r.Height));
            path.AddArc(r.Left,r.Top,d,d,180,90); path.AddArc(r.Right-d,r.Top,d,d,270,90); path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); path.AddArc(r.Left,r.Bottom-d,d,d,90,90); path.CloseFigure(); return path;
        }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { e.Graphics.Clear(Surface); }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
        private void PaintItem(ToolStripItemRenderEventArgs e)
        {
            var bounds = new Rectangle(1,1,e.Item.Width-2,e.Item.Height-2); if (bounds.Width < 1 || bounds.Height < 1) return;
            bool selected = e.Item.Selected || (e.Item is ToolStripButton button && button.Checked);
            var color = selected ? ItemHotOf(dark) : ItemFillOf(dark);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (selected) using (var path = Rounded(bounds, 9)) using (var brush = new SolidBrush(color)) e.Graphics.FillPath(brush,path);
        }
        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e) => PaintItem(e);
        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e) => PaintItem(e);
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) { if (e.Item.Selected) PaintItem(e); }
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { using (var brush = new SolidBrush(Surface)) e.Graphics.FillRectangle(brush, e.AffectedBounds); }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (var pen = new Pen(Color.FromArgb(dark ? 72 : 178, Foreground)))
            {
                int middle = e.Item.Width / 2;
                e.Graphics.DrawLine(pen, middle, 8, middle, e.Item.Height - 8);
            }
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = Foreground; base.OnRenderItemText(e); }
    }
}
