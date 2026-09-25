// ShotCab drawing style controls. Upstream GPL license applies.
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ShareX.ScreenCaptureLib
{
    internal partial class ShapeManager
    {
        private ShotCabStylePalette shotCabStylePalette;

        private static bool ShotCabStyleHasFill(ShapeType type)
        {
            return type == ShapeType.DrawingRectangle ||
                type == ShapeType.DrawingEllipse || type == ShapeType.DrawingTextBackground ||
                type == ShapeType.DrawingSpeechBalloon || type == ShapeType.DrawingStep ||
                type == ShapeType.DrawingMagnify;
        }

        private static bool ShotCabStyleSupported(ShapeType type)
        {
            return ShotCabStyleHasFill(type) || type == ShapeType.DrawingFreehand ||
                type == ShapeType.DrawingFreehandArrow || type == ShapeType.DrawingLine ||
                type == ShapeType.DrawingArrow || type == ShapeType.DrawingTextOutline;
        }

        private ShapeType ShotCabCurrentStyleTool()
        {
            ShapeType type = CurrentShapeTool;
            return ShotCabStyleSupported(type) ? type : ShapeType.DrawingRectangle;
        }

        private void ShotCabReadDrawingStyle(ShapeType type, out Color border, out int width, out Color fill)
        {
            if (CurrentShape is BaseDrawingShape drawing && CurrentShape.ShapeType == type)
            {
                border = drawing.BorderColor;
                width = drawing.BorderSize;
                fill = drawing.FillColor;
                return;
            }
            if (type == ShapeType.DrawingSpeechBalloon || type == ShapeType.DrawingTextBackground)
            {
                border = AnnotationOptions.TextBorderColor;
                width = AnnotationOptions.TextBorderSize;
                fill = AnnotationOptions.TextFillColor;
            }
            else if (type == ShapeType.DrawingStep)
            {
                border = AnnotationOptions.StepBorderColor;
                width = AnnotationOptions.StepBorderSize;
                fill = AnnotationOptions.StepFillColor;
            }
            else if (type == ShapeType.DrawingTextOutline)
            {
                border = AnnotationOptions.TextOutlineBorderColor;
                width = AnnotationOptions.TextOutlineBorderSize;
                fill = Color.Transparent;
            }
            else
            {
                border = AnnotationOptions.BorderColor;
                width = AnnotationOptions.BorderSize;
                fill = AnnotationOptions.FillColor;
            }
        }

        internal void ShotCabApplyDrawingStyle(ShapeType type, Color border, int width, Color fill)
        {
            if (!ShotCabStyleSupported(type)) throw new ArgumentOutOfRangeException(nameof(type));
            width = Math.Max(0, Math.Min(50, width));
            if (ShotCabStyleHasFill(type) && fill.A == 0 && width == 0) width = 2;
            if (type == ShapeType.DrawingSpeechBalloon || type == ShapeType.DrawingTextBackground)
            {
                AnnotationOptions.TextBorderColor = border;
                AnnotationOptions.TextBorderSize = width;
                AnnotationOptions.TextFillColor = fill;
            }
            else if (type == ShapeType.DrawingStep)
            {
                AnnotationOptions.StepBorderColor = border;
                AnnotationOptions.StepBorderSize = width;
                AnnotationOptions.StepFillColor = fill;
            }
            else if (type == ShapeType.DrawingTextOutline)
            {
                AnnotationOptions.TextOutlineBorderColor = border;
                AnnotationOptions.TextOutlineBorderSize = width;
            }
            else
            {
                AnnotationOptions.BorderColor = border;
                AnnotationOptions.BorderSize = width;
                if (ShotCabStyleHasFill(type)) AnnotationOptions.FillColor = fill;
            }

            if (CurrentShape is BaseDrawingShape selected && CurrentShape.ShapeType == type)
            {
                bool changed = selected.BorderColor.ToArgb() != border.ToArgb() || selected.BorderSize != width ||
                    (ShotCabStyleHasFill(type) && selected.FillColor.ToArgb() != fill.ToArgb());
                if (changed)
                {
                    history.CreateShapesMemento();
                    selected.BorderColor = border;
                    selected.BorderSize = width;
                    if (ShotCabStyleHasFill(type)) selected.FillColor = fill;
                    selected.OnMoved();
                    OnImageModified();
                }
            }
            UpdateMenu();
            Form.Resume();
            Form.Invalidate();
        }

        private void ShotCabOpenStylePalette(ShapeType type, ToolStripItem anchor)
        {
            if (!ShotCabStyleSupported(type)) return;
            shotCabStylePalette?.Close();
            ShotCabReadDrawingStyle(type, out Color border, out int width, out Color fill);
            bool english = System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "en";
            string title = CurrentTool == ShapeType.ToolSelect && CurrentShape == null && type == ShapeType.DrawingRectangle
                ? (english ? "Annotation" : "标注") : ShotCabToolLabel(type, english);
            shotCabStylePalette = new ShotCabStylePalette(
                title, ShotCabStyleHasFill(type), border, width, fill,
                ((ShotCabSoftRenderer)tsMain.Renderer).Surface.R < 100, english,
                (newBorder, newWidth, newFill) => ShotCabApplyDrawingStyle(type, newBorder, newWidth, newFill));
            var palette = shotCabStylePalette;
            palette.FormClosed += (sender, args) => { if (ReferenceEquals(shotCabStylePalette, palette)) shotCabStylePalette = null; palette.Dispose(); };
            Point anchorScreen = anchor.Owner.PointToScreen(anchor.Bounds.Location);
            Rectangle screen = Screen.FromPoint(anchorScreen).WorkingArea;
            int x = anchorScreen.X + anchor.Width / 2 - palette.Width / 2;
            int y = anchorScreen.Y - palette.Height - 8;
            if (screen.Contains(anchorScreen))
            {
                x = Math.Max(screen.Left + 8, Math.Min(x, screen.Right - palette.Width - 8));
                if (y < screen.Top + 8) y = anchorScreen.Y + anchor.Height + 8;
            }
            palette.Location = new Point(x, y);
            palette.Show(Form);
            palette.BringToFront();
        }

        private void ShotCabCloseStylePalette()
        {
            if (shotCabStylePalette != null && !shotCabStylePalette.IsDisposed)
                shotCabStylePalette.Close();
        }
    }
}
