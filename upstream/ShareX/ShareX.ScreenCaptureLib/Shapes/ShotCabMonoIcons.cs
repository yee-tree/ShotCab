// ShotCab additions, upstream GPL license applies.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShareX.ScreenCaptureLib
{
    internal static class ShotCabMonoIcons
    {
        internal static Bitmap Tool(ShapeType type, Color ink)
        {
            switch (type)
            {
                case ShapeType.ToolSelect: return Draw("select", ink);
                case ShapeType.DrawingRectangle: return Draw("rectangle", ink);
                case ShapeType.DrawingEllipse: return Draw("ellipse", ink);
                case ShapeType.DrawingArrow: return Draw("arrow", ink);
                case ShapeType.DrawingFreehandArrow: return Draw("curveArrow", ink);
                case ShapeType.DrawingLine: return Draw("line", ink);
                case ShapeType.DrawingFreehand: return Draw("pen", ink);
                case ShapeType.DrawingTextOutline: return Draw("text", ink);
                case ShapeType.DrawingTextBackground: return Draw("textBox", ink);
                case ShapeType.DrawingSpeechBalloon: return Draw("balloon", ink);
                case ShapeType.DrawingStep: return Draw("step", ink);
                case ShapeType.DrawingMagnify: return Draw("magnify", ink);
                case ShapeType.DrawingImage: return Draw("image", ink);
                case ShapeType.DrawingImageScreen: return Draw("screen", ink);
                case ShapeType.DrawingSticker: return Draw("star", ink);
                case ShapeType.DrawingCursor: return Draw("cursor", ink);
                case ShapeType.DrawingSmartEraser: return Draw("eraser", ink);
                case ShapeType.EffectBlur: return Draw("blur", ink);
                case ShapeType.EffectPixelate: return Draw("pixelate", ink);
                case ShapeType.EffectHighlight: return Draw("highlight", ink);
                case ShapeType.EffectSolidMask: return Draw("mask", ink);
                case ShapeType.EffectSpotlight: return Draw("spotlight", ink);
                case ShapeType.ToolCrop: return Draw("crop", ink);
                case ShapeType.ToolCutOut: return Draw("cutout", ink);
                default: return Draw("more", ink);
            }
        }

        internal static Bitmap Action(string name, Color ink) => Draw(name, ink);

        private static Bitmap Draw(string name, Color ink)
        {
            var bitmap = new Bitmap(24, 24);
            using (var g = Graphics.FromImage(bitmap))
            using (var pen = new Pen(ink, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                switch (name)
                {
                    case "select":
                    case "cursor":
                        g.DrawLines(pen, new[] { P(5, 3), P(5, 19), P(9, 15), P(12, 21), P(15, 19), P(12, 14), P(18, 13), P(5, 3) }); break;
                    case "rectangle": g.DrawRectangle(pen, 4, 5, 16, 14); break;
                    case "ellipse": g.DrawEllipse(pen, 4, 4, 16, 16); break;
                    case "arrow":
                        g.DrawLine(pen, 4, 19, 19, 5);
                        g.DrawLines(pen, new[] { P(13, 5), P(19, 5), P(19, 11) }); break;
                    case "curveArrow":
                        g.DrawBezier(pen, P(3, 18), P(7, 5), P(15, 18), P(20, 5));
                        g.DrawLines(pen, new[] { P(14, 7), P(20, 5), P(18, 11) }); break;
                    case "line": g.DrawLine(pen, 4, 18, 20, 6); break;
                    case "pen":
                        g.DrawLine(pen, 5, 18, 17, 5); g.DrawLines(pen, new[] { P(17, 5), P(20, 8), P(8, 21), P(4, 21), P(5, 18) }); break;
                    case "text":
                        g.DrawLine(pen, 4, 6, 20, 6); g.DrawLine(pen, 12, 6, 12, 19); g.DrawLine(pen, 8, 19, 16, 19); break;
                    case "textBox":
                        g.DrawRectangle(pen, 3, 4, 18, 16); g.DrawLine(pen, 7, 8, 17, 8); g.DrawLine(pen, 12, 8, 12, 17); break;
                    case "balloon":
                        g.DrawRoundedRectangleCompat(pen, 3, 4, 18, 14, 4);
                        g.DrawLines(pen, new[] { P(8, 18), P(7, 22), P(12, 18) }); break;
                    case "step":
                        g.DrawEllipse(pen, 3, 3, 18, 18);
                        // A font glyph carries asymmetric side bearings at this size. Draw
                        // the numeral on the circle's own centre line instead.
                        using (var numeral = new Pen(ink, 1.9f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                        {
                            g.DrawLines(numeral, new[] { P(9.5f, 9.5f), P(12, 7.5f), P(12, 16.5f) });
                            g.DrawLine(numeral, 9.5f, 16.5f, 14.5f, 16.5f);
                        }
                        break;
                    case "magnify":
                        g.DrawEllipse(pen, 4, 4, 12, 12); g.DrawLine(pen, 15, 15, 21, 21); g.DrawLine(pen, 10, 7, 10, 13); g.DrawLine(pen, 7, 10, 13, 10); break;
                    case "image":
                        g.DrawRectangle(pen, 3, 4, 18, 16); g.DrawEllipse(pen, 6, 7, 3, 3);
                        g.DrawLines(pen, new[] { P(4, 17), P(9, 12), P(12, 15), P(16, 10), P(21, 16) }); break;
                    case "screen":
                        g.DrawRectangle(pen, 3, 4, 18, 14); g.DrawLine(pen, 12, 18, 12, 21); g.DrawLine(pen, 8, 21, 16, 21); break;
                    case "star":
                        g.DrawLines(pen, new[] { P(12, 2), P(14, 9), P(21, 9), P(16, 14), P(18, 21), P(12, 17), P(6, 21), P(8, 14), P(3, 9), P(10, 9), P(12, 2) }); break;
                    case "eraser":
                        g.DrawLines(pen, new[] { P(3, 15), P(12, 4), P(21, 12), P(12, 21), P(8, 21), P(3, 15) }); g.DrawLine(pen, 8, 10, 17, 18); break;
                    case "blur":
                        g.DrawEllipse(pen, 7, 7, 10, 10); g.DrawEllipse(pen, 4, 4, 16, 16); break;
                    case "pixelate":
                        for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) g.DrawRectangle(pen, 4 + x * 6, 4 + y * 6, 4, 4); break;
                    case "highlight":
                        g.DrawLine(pen, 5, 17, 17, 5); g.DrawLine(pen, 7, 19, 19, 7); g.DrawLine(pen, 4, 21, 20, 21); break;
                    case "mask":
                        using (var brush = new SolidBrush(ink)) g.FillRectangle(brush, 4, 7, 16, 10); break;
                    case "spotlight":
                        g.DrawEllipse(pen, 8, 8, 8, 8); g.DrawLine(pen, 12, 2, 12, 5); g.DrawLine(pen, 12, 19, 12, 22);
                        g.DrawLine(pen, 2, 12, 5, 12); g.DrawLine(pen, 19, 12, 22, 12); break;
                    case "crop":
                        g.DrawLines(pen, new[] { P(7, 3), P(7, 17), P(21, 17) }); g.DrawLines(pen, new[] { P(3, 7), P(17, 7), P(17, 21) }); break;
                    case "cutout":
                        g.DrawRectangle(pen, 3, 5, 18, 14); g.DrawLine(pen, 8, 4, 8, 20); g.DrawLine(pen, 16, 4, 16, 20); break;
                    case "undo": Arrow(g, pen, true); break;
                    case "redo": Arrow(g, pen, false); break;
                    case "history":
                        g.DrawArc(pen, 4, 4, 16, 16, 35, 305);
                        g.DrawLines(pen, new[] { P(5, 5), P(5, 10), P(10, 10) });
                        g.DrawLine(pen, 12, 7, 12, 12);
                        g.DrawLine(pen, 12, 12, 16, 14);
                        break;
                    case "done": g.DrawLines(pen, new[] { P(3, 12), P(9, 18), P(21, 5) }); break;
                    case "close": g.DrawLine(pen, 5, 5, 19, 19); g.DrawLine(pen, 19, 5, 5, 19); break;
                    case "color": g.DrawEllipse(pen, 5, 5, 14, 14); g.DrawEllipse(pen, 9, 9, 6, 6); break;
                    case "style": g.DrawLine(pen, 4, 6, 20, 6); g.DrawLine(pen, 4, 12, 20, 12); g.DrawLine(pen, 4, 18, 20, 18);
                        g.DrawEllipse(pen, 7, 4, 4, 4); g.DrawEllipse(pen, 14, 10, 4, 4); g.DrawEllipse(pen, 9, 16, 4, 4); break;
                    case "files": g.DrawRectangle(pen, 5, 3, 14, 18); g.DrawLine(pen, 9, 8, 16, 8); g.DrawLine(pen, 9, 12, 16, 12); break;
                    case "combine":
                        g.DrawRectangle(pen, 3, 4, 11, 11);
                        g.DrawRectangle(pen, 10, 10, 11, 11);
                        g.DrawLine(pen, 5, 12, 9, 8);
                        g.DrawLine(pen, 13, 18, 18, 13);
                        break;
                    case "ocr":
                        g.DrawLines(pen, new[] { P(8, 3), P(3, 3), P(3, 8) });
                        g.DrawLines(pen, new[] { P(16, 3), P(21, 3), P(21, 8) });
                        g.DrawLines(pen, new[] { P(3, 16), P(3, 21), P(8, 21) });
                        g.DrawLines(pen, new[] { P(16, 21), P(21, 21), P(21, 16) });
                        g.DrawLine(pen, 7, 10, 17, 10);
                        g.DrawLine(pen, 7, 14, 14, 14);
                        break;
                    case "more":
                        for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) g.DrawRectangle(pen, 5 + x * 9, 5 + y * 9, 5, 5); break;
                    case "actions":
                        using (var brush = new SolidBrush(ink)) for (int x = 0; x < 3; x++) g.FillEllipse(brush, 4 + x * 7, 10, 3, 3); break;
                    default: g.DrawEllipse(pen, 5, 5, 14, 14); break;
                }
            }
            return bitmap;
        }

        private static PointF P(float x, float y) => new PointF(x, y);
        private static void Arrow(Graphics g, Pen pen, bool left)
        {
            if (left)
            {
                g.DrawArc(pen, 6, 6, 15, 12, 190, 250);
                g.DrawLines(pen, new[] { P(8, 5), P(3, 10), P(9, 12) });
            }
            else
            {
                g.DrawArc(pen, 3, 6, 15, 12, 100, 250);
                g.DrawLines(pen, new[] { P(16, 5), P(21, 10), P(15, 12) });
            }
        }
        private static void DrawRoundedRectangleCompat(this Graphics g, Pen pen, float x, float y, float width, float height, float radius)
        {
            using (var path = ShotCabSoftRenderer.Rounded(new Rectangle((int)x, (int)y, (int)width, (int)height), (int)radius)) g.DrawPath(pen, path);
        }
    }
}
