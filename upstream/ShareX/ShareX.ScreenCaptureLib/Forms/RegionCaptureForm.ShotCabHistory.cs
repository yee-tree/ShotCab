// ShotCab editor history panel. Upstream GPL license applies.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShareX.ScreenCaptureLib
{
    public sealed partial class RegionCaptureForm
    {
        private const int ShotCabHistoryWidth = 238;
        private Panel shotCabHistoryPanel;
        private FlowLayoutPanel shotCabHistoryRows;
        private bool shotCabHistoryChanging;
        private float shotCabHistoryPreviousZoom, shotCabHistoryAutoFitZoom;
        private int shotCabHistoryRenderedCount = -1, shotCabHistoryRenderedPosition = -1, shotCabHistoryRenderedVersion = -1;

        internal bool ShotCabHistoryVisible => shotCabHistoryPanel != null && shotCabHistoryPanel.Visible;

        internal void ShotCabToggleHistoryPanel()
        {
            if (!IsEditorMode) return;
            if (!ShotCabHistoryVisible && ShotCabOcrVisible) ShotCabCloseOcrPanel();
            if (shotCabHistoryPanel == null) ShotCabCreateHistoryPanel();
            // Populate the timeline before showing the surface. Otherwise the first
            // click can briefly expose its unpainted native child area.
            if (!shotCabHistoryPanel.Visible)
            {
                ShotCabUpdateHistoryBounds();
                ShotCabRefreshHistoryPanel(true);
            }
            shotCabHistoryPanel.Visible = !shotCabHistoryPanel.Visible;
            ShotCabUpdateHistoryBounds();
            UpdateCoordinates();
            if (shotCabHistoryPanel.Visible)
            {
                shotCabHistoryPreviousZoom = ZoomFactor;
                float fit = Math.Min((ClientArea.Width - 32f) / CanvasRectangle.Width,
                    (ClientArea.Height - ToolbarHeight - 24f) / CanvasRectangle.Height);
                if (fit > 0 && fit < ZoomFactor)
                {
                    ZoomFactor = fit;
                    shotCabHistoryAutoFitZoom = ZoomFactor;
                    UpdateTitle();
                }
                else shotCabHistoryAutoFitZoom = 0;
            }
            else if (shotCabHistoryAutoFitZoom > 0)
            {
                // Restore the former zoom only if the user did not change it while
                // browsing history.
                if (Math.Abs(ZoomFactor - shotCabHistoryAutoFitZoom) < .001f)
                {
                    ZoomFactor = shotCabHistoryPreviousZoom;
                    UpdateTitle();
                }
                shotCabHistoryAutoFitZoom = 0;
            }
            CenterCanvas();
            if (shotCabHistoryPanel.Visible)
            {
                ShotCabRefreshHistoryPanel();
                shotCabHistoryPanel.Invalidate(true);
                shotCabHistoryPanel.Update();
            }
            ShapeManager?.ShotCabSetHistoryButtonState(ShotCabHistoryVisible);
            ShotCabPrimeChrome();
            Invalidate();
        }

        private void ShotCabCreateHistoryPanel()
        {
            bool english = System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "en";
            var surface = ShotCabSoftRenderer.SurfaceOf(shotCabChromeDark);
            var ink = ShotCabSoftRenderer.ForegroundOf(shotCabChromeDark);
            shotCabHistoryPanel = new ShotCabBufferedSurface
            {
                Name = "ShotCabEditorHistory", BackColor = surface, Width = ShotCabHistoryWidth,
                Visible = false, AccessibleName = english ? "Editor history" : "编辑历史"
            };
            var header = new ShotCabBufferedSurface { Dock = DockStyle.Top, Height = 52, BackColor = surface };
            var heading = new Label
            {
                Dock = DockStyle.Fill, Text = english ? "History" : "编辑历史", ForeColor = ink,
                Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0), BackColor = surface
            };
            var close = new ShotCabWindowButton(ShotCabWindowGlyph.Close)
            {
                Dock = DockStyle.Right, Width = 42,
                AccessibleName = english ? "Collapse history" : "收起编辑历史"
            };
            close.Dark = shotCabChromeDark;
            close.Click += (sender, args) => ShotCabToggleHistoryPanel();
            var hint = new ToolTip { AutoPopDelay = 4000 };
            hint.SetToolTip(close, close.AccessibleName);
            shotCabHistoryPanel.Disposed += (sender, args) => hint.Dispose();
            header.Controls.Add(heading);
            header.Controls.Add(close);
            shotCabHistoryRows = new ShotCabBufferedFlow
            {
                Name = "ShotCabHistorySteps", Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, BackColor = surface,
                Padding = new Padding(10, 6, 10, 10)
            };
            shotCabHistoryPanel.Controls.Add(shotCabHistoryRows);
            shotCabHistoryPanel.Controls.Add(header);
            Controls.Add(shotCabHistoryPanel);
            shotCabHistoryPanel.BringToFront();
            shotCabCaptionBar?.BringToFront();
            Resize += (sender, args) => ShotCabUpdateHistoryBounds();
            ShotCabUpdateHistoryBounds();
        }

        internal void ShotCabUpdateHistoryBounds()
        {
            if (shotCabHistoryPanel == null) return;
            shotCabHistoryPanel.Bounds = new Rectangle(
                Math.Max(0, ClientSize.Width - ShotCabHistoryWidth), ShotCabCaptionHeight,
                ShotCabHistoryWidth, Math.Max(0, ClientSize.Height - ToolbarHeight));
            if (shotCabHistoryRows != null)
                foreach (Control row in shotCabHistoryRows.Controls) row.Width = Math.Max(120, shotCabHistoryRows.ClientSize.Width - 24);
        }

        internal void ShotCabRefreshHistoryPanel(bool prewarm = false)
        {
            if ((!ShotCabHistoryVisible && !prewarm) || shotCabHistoryChanging || ShapeManager == null) return;
            int count = ShapeManager.ShotCabHistoryCount;
            int position = ShapeManager.ShotCabHistoryPosition;
            int version = ShapeManager.ShotCabHistoryVersion;
            if (count == shotCabHistoryRenderedCount && position == shotCabHistoryRenderedPosition &&
                version == shotCabHistoryRenderedVersion) return;
            shotCabHistoryChanging = true;
            try
            {
                shotCabHistoryRows.SuspendLayout();
                while (shotCabHistoryRows.Controls.Count > 0)
                {
                    Control old = shotCabHistoryRows.Controls[0];
                    shotCabHistoryRows.Controls.RemoveAt(0);
                    old.Dispose();
                }
                for (int step = 0; step < count; step++)
                {
                    int selected = step;
                    var row = new ShotCabHistoryStepRow(step, ShapeManager.ShotCabHistoryTitle(step), step == position,
                        step > position, shotCabChromeDark)
                    {
                        Width = Math.Max(120, shotCabHistoryRows.ClientSize.Width - 24), Height = 44,
                        Margin = new Padding(0, 0, 0, 5)
                    };
                    row.Click += (sender, args) =>
                    {
                        ShapeManager.ShotCabJumpToHistory(selected);
                        ShotCabRefreshHistoryPanel();
                        Invalidate();
                    };
                    shotCabHistoryRows.Controls.Add(row);
                }
                shotCabHistoryRenderedCount = count;
                shotCabHistoryRenderedPosition = position;
                shotCabHistoryRenderedVersion = version;
            }
            finally
            {
                shotCabHistoryRows.ResumeLayout(true);
                shotCabHistoryChanging = false;
            }
            shotCabHistoryRows.Invalidate(true);
            shotCabHistoryRows.Update();
        }
    }

    internal sealed class ShotCabHistoryStepRow : Control
    {
        private readonly int step;
        private readonly bool active, future, dark;
        private bool hot;

        internal ShotCabHistoryStepRow(int step, string title, bool active, bool future, bool dark)
        {
            this.step = step; this.active = active; this.future = future; this.dark = dark;
            Text = title; Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.ListItem;
            AccessibleName = (step + 1) + ". " + title;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
            if (bounds.Width < 5 || bounds.Height < 5) return;
            Color fill = active || hot ? ShotCabSoftRenderer.ItemHotOf(dark) : ShotCabSoftRenderer.SurfaceOf(dark);
            using (var path = ShotCabSoftRenderer.Rounded(bounds, 9))
            using (var brush = new SolidBrush(fill)) e.Graphics.FillPath(brush, path);
            if (active)
                using (var brush = new SolidBrush(ShotCabSoftRenderer.AccentOf(dark)))
                    e.Graphics.FillRectangle(brush, 3, 11, 3, Height - 22);
            Color ink = ShotCabSoftRenderer.ForegroundOf(dark);
            if (future) ink = Color.FromArgb(120, ink);
            using (var numberFont = new Font("Segoe UI", 9))
                TextRenderer.DrawText(e.Graphics, step.ToString("00"), numberFont,
                    new Rectangle(15, 0, 32, Height), ink, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            TextRenderer.DrawText(e.Graphics, Text, Font ?? SystemFonts.MessageBoxFont,
                new Rectangle(48, 0, Width - 58, Height), ink,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }
    }
}
