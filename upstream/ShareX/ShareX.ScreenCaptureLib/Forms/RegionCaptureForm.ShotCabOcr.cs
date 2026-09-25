// ShotCab in-editor OCR surface. Recognition stays in the application layer.
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace ShareX.ScreenCaptureLib
{
    public sealed partial class RegionCaptureForm
    {
        private const int ShotCabOcrWidth = 300;
        private Panel shotCabOcrPanel;
        private TextBox shotCabOcrText;
        private Label shotCabOcrCount;
        private Label shotCabOcrStatus;
        private Control shotCabOcrOverlay;
        private string shotCabOcrSelectedText;
        private float shotCabOcrPreviousZoom, shotCabOcrAutoFitZoom;

        public event Action<string> ShotCabOcrCopyRequested;
        public event Action ShotCabOcrClosed;
        internal bool ShotCabOcrVisible => shotCabOcrPanel != null && shotCabOcrPanel.Visible;
        internal string ShotCabOcrText => shotCabOcrText?.Text;

        public void ShotCabShowOcrPanel()
        {
            if (!IsEditorMode) return;
            if (ShotCabHistoryVisible) ShotCabToggleHistoryPanel();
            if (shotCabOcrPanel == null) ShotCabCreateOcrPanel();
            ShotCabDetachOcrOverlay();
            shotCabOcrSelectedText = null;
            bool opening = !ShotCabOcrVisible;
            shotCabOcrText.Clear();
            shotCabOcrStatus.Text = ShotCabEnglish ? "Recognizing text locally…" : "正在本地识别文字…";
            shotCabOcrPanel.Visible = true;
            ShotCabUpdateOcrBounds();
            UpdateCoordinates();
            if (opening)
            {
                shotCabOcrPreviousZoom = ZoomFactor;
                float fit = Math.Min((ClientArea.Width - 32f) / CanvasRectangle.Width,
                    (ClientArea.Height - ToolbarHeight - 24f) / CanvasRectangle.Height);
                if (fit > 0 && fit < ZoomFactor)
                {
                    ZoomFactor = fit;
                    shotCabOcrAutoFitZoom = fit;
                    UpdateTitle();
                }
                else shotCabOcrAutoFitZoom = 0;
                CenterCanvas();
            }
            shotCabOcrPanel.Invalidate(true);
            shotCabOcrPanel.Update();
            ShotCabPrimeChrome();
            Invalidate();
        }

        public void ShotCabSetOcrResult(string text, string status)
        {
            if (shotCabOcrPanel == null || shotCabOcrPanel.IsDisposed) return;
            shotCabOcrText.Text = text ?? string.Empty;
            shotCabOcrStatus.Text = status ?? string.Empty;
            shotCabOcrPanel.Invalidate(true);
        }

        public void ShotCabSetOcrSelectionStatus(string status)
        {
            if (shotCabOcrStatus != null && !shotCabOcrStatus.IsDisposed)
                shotCabOcrStatus.Text = status ?? string.Empty;
        }

        public void ShotCabSetOcrSelectedText(string text)
        {
            shotCabOcrSelectedText = text;
        }

        public void ShotCabCloseOcrPanel()
        {
            if (!ShotCabOcrVisible) return;
            ShotCabDetachOcrOverlay();
            shotCabOcrSelectedText = null;
            shotCabOcrPanel.Visible = false;
            if (shotCabOcrAutoFitZoom > 0 && Math.Abs(ZoomFactor - shotCabOcrAutoFitZoom) < .001f)
            {
                ZoomFactor = shotCabOcrPreviousZoom;
                UpdateTitle();
            }
            shotCabOcrAutoFitZoom = 0;
            UpdateCoordinates();
            CenterCanvas();
            ShotCabOcrClosed?.Invoke();
            ShotCabPrimeChrome();
            Invalidate();
        }

        public void ShotCabAttachOcrOverlay(Control overlay)
        {
            if (overlay == null) throw new ArgumentNullException(nameof(overlay));
            ShotCabDetachOcrOverlay();
            if (!ShotCabOcrVisible) { overlay.Dispose(); return; }
            shotCabOcrOverlay = overlay;
            overlay.Name = "ShotCabOcrImageSelection";
            Controls.Add(overlay);
            ShotCabUpdateOcrOverlayBounds();
            overlay.BringToFront();
            shotCabOcrPanel.BringToFront();
            shotCabCaptionBar?.BringToFront();
            overlay.Invalidate();
        }

        public void ShotCabZoomOcrImage(int wheelDelta)
        {
            if (!ShotCabOcrVisible || wheelDelta == 0) return;
            Zoom(wheelDelta > 0, false);
            ShotCabUpdateOcrOverlayBounds();
            Invalidate();
        }

        private void ShotCabDetachOcrOverlay()
        {
            if (shotCabOcrOverlay == null) return;
            Control old = shotCabOcrOverlay;
            shotCabOcrOverlay = null;
            Controls.Remove(old);
            old.Dispose();
        }

        private void ShotCabUpdateOcrOverlayBounds()
        {
            if (shotCabOcrOverlay == null || shotCabOcrOverlay.IsDisposed) return;
            var canvas = CanvasRectangle;
            var bounds = Rectangle.Round(new RectangleF(canvas.X * ZoomFactor, canvas.Y * ZoomFactor,
                canvas.Width * ZoomFactor, canvas.Height * ZoomFactor));
            if (shotCabOcrOverlay.Bounds != bounds) shotCabOcrOverlay.Bounds = bounds;
        }

        private void ShotCabCreateOcrPanel()
        {
            Color surface = ShotCabSoftRenderer.SurfaceOf(shotCabChromeDark);
            Color ink = ShotCabSoftRenderer.ForegroundOf(shotCabChromeDark);
            shotCabOcrPanel = new ShotCabBufferedSurface
            {
                Name = "ShotCabEditorOcr", BackColor = surface, Width = ShotCabOcrWidth,
                Visible = false, AccessibleName = ShotCabEnglish ? "Recognized text" : "识别文字"
            };
            var header = new ShotCabBufferedSurface { Dock = DockStyle.Top, Height = 52, BackColor = surface };
            var heading = new Label
            {
                Dock = DockStyle.Fill, Text = ShotCabEnglish ? "Recognized text" : "识别文字", ForeColor = ink,
                Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0), BackColor = surface
            };
            shotCabOcrCount = new Label
            {
                Name = "ShotCabOcrCount", Dock = DockStyle.Right, Width = 78,
                Text = ShotCabEnglish ? "0 chars" : "0 字", TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Microsoft YaHei UI", 8.5f), ForeColor = Color.FromArgb(150, ink),
                BackColor = surface, Padding = new Padding(0, 0, 5, 0)
            };
            var close = new ShotCabWindowButton(ShotCabWindowGlyph.Close)
            {
                Dock = DockStyle.Right, Width = 42,
                AccessibleName = ShotCabEnglish ? "Close OCR panel" : "收起文字识别"
            };
            var copy = new ShotCabWindowButton(ShotCabWindowGlyph.Copy)
            {
                Name = "ShotCabOcrCopy",
                Dock = DockStyle.Right, Width = 42,
                AccessibleName = ShotCabEnglish ? "Copy selected or all text" : "复制所选或全部文字"
            };
            close.Dark = copy.Dark = shotCabChromeDark;
            close.Click += (sender, args) => ShotCabCloseOcrPanel();
            copy.Click += (sender, args) => ShotCabOcrCopyRequested?.Invoke(
                !string.IsNullOrEmpty(shotCabOcrSelectedText) ? shotCabOcrSelectedText :
                shotCabOcrText.SelectionLength > 0 ? shotCabOcrText.SelectedText : shotCabOcrText.Text);
            var hints = new ToolTip { AutoPopDelay = 4000 };
            hints.SetToolTip(close, close.AccessibleName);
            hints.SetToolTip(copy, copy.AccessibleName);
            shotCabOcrPanel.Disposed += (sender, args) => hints.Dispose();
            header.Controls.Add(heading);
            header.Controls.Add(shotCabOcrCount);
            header.Controls.Add(close);
            header.Controls.Add(copy);

            var textHolder = new ShotCabBufferedSurface { Dock = DockStyle.Fill, BackColor = surface, Padding = new Padding(14, 8, 14, 8) };
            shotCabOcrText = new TextBox
            {
                Name = "ShotCabOcrText", Dock = DockStyle.Fill, Multiline = true,
                AcceptsReturn = true, ScrollBars = ScrollBars.None, BorderStyle = System.Windows.Forms.BorderStyle.None,
                BackColor = surface, ForeColor = ink,
                Font = new Font("Microsoft YaHei UI", 10f),
                AccessibleName = ShotCabEnglish ? "Recognized text, editable" : "识别文字，可编辑"
            };
            textHolder.Controls.Add(shotCabOcrText);
            shotCabOcrText.TextChanged += (sender, args) => { shotCabOcrSelectedText = null; ShotCabUpdateOcrCount(); };
            shotCabOcrText.Enter += (sender, args) => shotCabOcrSelectedText = null;
            shotCabOcrStatus = new Label
            {
                Name = "ShotCabOcrStatus", Dock = DockStyle.Bottom, Height = 84,
                Padding = new Padding(14, 7, 14, 5), BackColor = surface,
                ForeColor = Color.FromArgb(150, ink),
                Font = new Font("Microsoft YaHei UI", 8.5f),
                Text = ShotCabEnglish ? "Recognizing text locally…" : "正在本地识别文字…"
            };
            shotCabOcrPanel.Controls.Add(textHolder);
            shotCabOcrPanel.Controls.Add(shotCabOcrStatus);
            shotCabOcrPanel.Controls.Add(header);
            Controls.Add(shotCabOcrPanel);
            shotCabOcrPanel.BringToFront();
            shotCabCaptionBar?.BringToFront();
            Resize += (sender, args) => ShotCabUpdateOcrBounds();
            ShotCabUpdateOcrBounds();
        }

        private void ShotCabUpdateOcrCount()
        {
            int count = 0;
            var elements = StringInfo.GetTextElementEnumerator(shotCabOcrText.Text);
            while (elements.MoveNext())
                if (!string.IsNullOrWhiteSpace(elements.GetTextElement())) count++;
            shotCabOcrCount.Text = ShotCabEnglish ? count + " chars" : count + " 字";
        }

        internal void ShotCabUpdateOcrBounds()
        {
            if (shotCabOcrPanel == null) return;
            shotCabOcrPanel.Bounds = new Rectangle(
                Math.Max(0, ClientSize.Width - ShotCabOcrWidth), ShotCabCaptionHeight,
                ShotCabOcrWidth, Math.Max(0, ClientSize.Height - ToolbarHeight));
        }
    }

    internal sealed class ShotCabBufferedSurface : Panel
    {
        internal ShotCabBufferedSurface()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
    }

    internal sealed class ShotCabBufferedFlow : FlowLayoutPanel
    {
        internal ShotCabBufferedFlow()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
    }
}
