// ShotCab additions; distributed under the ShareX GPL terms.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShareX.ScreenCaptureLib
{
    public sealed class SolidMaskEffectShape : BaseEffectShape
    {
        public override ShapeType ShapeType => ShapeType.EffectSolidMask;
        public override string OverlayText => "实心遮挡";
        public Color MaskColor { get; set; } = Color.Black;
        private Color OpaqueColor => Color.FromArgb(255, MaskColor.R, MaskColor.G, MaskColor.B);
        public override void OnConfigLoad() => MaskColor = AnnotationOptions.SolidMaskColor;
        public override void OnConfigSave() => AnnotationOptions.SolidMaskColor = OpaqueColor;
        public override void ApplyEffect(Bitmap bmp) { using (var g = Graphics.FromImage(bmp)) g.Clear(OpaqueColor); }
        public override void OnDraw(Graphics g) { using (var brush = new SolidBrush(OpaqueColor)) g.FillRectangle(brush, Rectangle); }
        public override void OnDrawFinal(Graphics g, Bitmap bmp)
        {
            // Always opaque; antialiased edges must not leave source pixels in the chosen area.
            var covered = System.Drawing.Rectangle.FromLTRB((int)Math.Floor(Rectangle.Left), (int)Math.Floor(Rectangle.Top), (int)Math.Ceiling(Rectangle.Right), (int)Math.Ceiling(Rectangle.Bottom));
            var area = System.Drawing.Rectangle.Intersect(new Rectangle(Point.Empty, bmp.Size), covered);
            if (!area.IsEmpty) using (var brush = new SolidBrush(OpaqueColor)) g.FillRectangle(brush, area);
        }
    }

    public sealed class SpotlightEffectShape : BaseEffectShape
    {
        public override ShapeType ShapeType => ShapeType.EffectSpotlight;
        public override string OverlayText => "聚光灯";
        public int Darkness { get; set; } = 150;
        public override void OnConfigLoad() => Darkness = Math.Max(0, Math.Min(255, AnnotationOptions.SpotlightDarkness));
        public override void OnConfigSave() => AnnotationOptions.SpotlightDarkness = Math.Max(0, Math.Min(255, Darkness));
        public override void ApplyEffect(Bitmap bmp) { }
        private void DrawShade(Graphics g, RectangleF canvas)
        {
            using (var region = new Region(canvas))
            using (var hole = new GraphicsPath())
            using (var brush = new SolidBrush(Color.FromArgb(Math.Max(0, Math.Min(255, Darkness)), Color.Black)))
            {
                hole.AddEllipse(Rectangle); region.Exclude(hole); g.FillRegion(brush, region);
            }
        }
        public override void OnDraw(Graphics g) => DrawShade(g, Manager.Form.CanvasRectangle);
        public override void OnDrawFinal(Graphics g, Bitmap bmp) => DrawShade(g, new Rectangle(Point.Empty, bmp.Size));
    }
}
