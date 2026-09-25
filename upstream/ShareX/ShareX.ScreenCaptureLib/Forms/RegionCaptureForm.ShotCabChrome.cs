// ShotCab borderless editor window chrome. Upstream GPL license applies.
using ShareX.HelpersLib;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShareX.ScreenCaptureLib
{
    /// <summary>
    /// The windowed image editor has no native frame. This partial part draws a ShotCab
    /// caption bar (title on the left, window buttons on the right) and keeps the window
    /// draggable and resizable, using the same soft neutral style as the editor toolbar
    /// and the animated preview cards.
    /// </summary>
    public sealed partial class RegionCaptureForm
    {
        internal const int ShotCabCaptionBarHeight = 40;
        private const int ShotCabResizeBorderThickness = 7;
        private const int HitTestMessage = 0x0084;

        // Zero while the native title bar is used (fullscreen capture modes).
        internal int ShotCabCaptionHeight { get; private set; }

        private static bool ShotCabEnglish
        {
            get { return System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "en"; }
        }

        private Panel shotCabCaptionBar;
        private Label shotCabCaptionLabel;
        private ShotCabWindowButton shotCabMinimizeButton, shotCabMaximizeButton, shotCabCloseButton;
        private ToolTip shotCabChromeTips;
        private bool shotCabChromeDark = true, shotCabMaximized, shotCabMaximizeStateApplied;

        /// <summary>Removes the native frame; the ShotCab caption bar replaces it.</summary>
        private void ShotCabUseBorderlessChrome()
        {
            FormBorderStyle = FormBorderStyle.None;
            MaximizedBounds = CaptureHelpers.GetActiveScreenWorkingArea();
        }

        private void ShotCabCreateCaptionBar()
        {
            shotCabCaptionBar = new ShotCabBufferedSurface
            {
                Dock = DockStyle.Top,
                Height = ShotCabCaptionBarHeight,
                Padding = new Padding(4),
                BackColor = ShotCabSoftRenderer.SurfaceOf(shotCabChromeDark),
                TabStop = false,
                AccessibleRole = AccessibleRole.TitleBar,
                AccessibleName = ShotCabEnglish ? "Editor title bar" : "编辑器标题栏"
            };
            shotCabCaptionBar.Paint += ShotCabCaptionBar_Paint;

            shotCabCaptionLabel = new Label
            {
                Name = "ShotCabEditorCaption",
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 1, 8, 0),
                BackColor = ShotCabSoftRenderer.SurfaceOf(shotCabChromeDark),
                ForeColor = ShotCabSoftRenderer.ForegroundOf(shotCabChromeDark),
                Font = ShotCabSoftRenderer.LabelFont(),
                AccessibleRole = AccessibleRole.StaticText,
                AccessibleName = ShotCabEnglish ? "Editor title" : "编辑器标题"
            };
            shotCabCaptionLabel.MouseDown += ShotCabCaption_MouseDown;
            shotCabCaptionLabel.DoubleClick += (sender, e) => ShotCabToggleMaximize();
            shotCabCaptionBar.Controls.Add(shotCabCaptionLabel);

            shotCabMinimizeButton = new ShotCabWindowButton(ShotCabWindowGlyph.Minimize) { AccessibleName = ShotCabEnglish ? "Minimize" : "最小化" };
            shotCabMaximizeButton = new ShotCabWindowButton(ShotCabWindowGlyph.Maximize) { AccessibleName = ShotCabEnglish ? "Maximize" : "最大化" };
            shotCabCloseButton = new ShotCabWindowButton(ShotCabWindowGlyph.Close) { AccessibleName = ShotCabEnglish ? "Close" : "关闭窗口" };
            shotCabMinimizeButton.Click += (sender, e) => WindowState = FormWindowState.Minimized;
            shotCabMaximizeButton.Click += (sender, e) => ShotCabToggleMaximize();
            shotCabCloseButton.Click += (sender, e) => Close();

            // Editing actions live together in the bottom palette; the caption only moves
            // and manages the window.
            foreach (ShotCabWindowButton button in new[] { shotCabMinimizeButton, shotCabMaximizeButton, shotCabCloseButton })
            {
                button.Dock = DockStyle.Right;
                button.Width = 44;
                shotCabCaptionBar.Controls.Add(button);
            }

            shotCabChromeTips = new ToolTip { AutoPopDelay = 4000, InitialDelay = 400 };
            ShotCabSetChromeHint(shotCabMinimizeButton, ShotCabEnglish ? "Minimize window" : "最小化窗口");
            ShotCabSetChromeHint(shotCabCloseButton, ShotCabEnglish ? "Close editor" : "关闭编辑器");

            Controls.Add(shotCabCaptionBar);
            shotCabCaptionBar.BringToFront();
            ShotCabCaptionHeight = ShotCabCaptionBarHeight;
            ShotCabRefreshMaximizeButton();

            Resize += (sender, e) => ShotCabRefreshMaximizeButton();
        }

        private void ShotCabCaptionBar_Paint(object sender, PaintEventArgs e)
        {
            // Hairline separator, same idea as the toolbar's grouped sections.
            using (Pen pen = new Pen(Color.FromArgb(shotCabChromeDark ? 38 : 22, ShotCabSoftRenderer.ForegroundOf(shotCabChromeDark))))
            {
                e.Graphics.DrawLine(pen, 0, shotCabCaptionBar.Height - 1, shotCabCaptionBar.Width, shotCabCaptionBar.Height - 1);
            }
        }

        internal void ShotCabPrimeChrome()
        {
            if (shotCabCaptionBar == null || shotCabCaptionBar.IsDisposed || !IsHandleCreated) return;
            shotCabCaptionBar.PerformLayout();
            ShotCabUpdateCaptionText(Text);
            shotCabCaptionBar.Invalidate(true);
            shotCabCaptionBar.Update();
        }

        internal void ShotCabApplyChromeTheme(bool dark)
        {
            shotCabChromeDark = dark;
            if (shotCabCaptionBar == null) return;

            shotCabCaptionBar.BackColor = ShotCabSoftRenderer.SurfaceOf(dark);
            shotCabCaptionLabel.BackColor = shotCabCaptionBar.BackColor;
            shotCabCaptionLabel.ForeColor = ShotCabSoftRenderer.ForegroundOf(dark);
            shotCabMinimizeButton.Dark = shotCabMaximizeButton.Dark = shotCabCloseButton.Dark = dark;
            shotCabCaptionBar.Invalidate();
        }

        internal void ShotCabUpdateCaptionText(string text)
        {
            if (shotCabCaptionLabel == null) return;
            shotCabCaptionLabel.Text = Canvas == null ? text : string.Format(
                ShotCabEnglish ? "ShotCab / Edit image   {0} × {1} · {2}%" : "ShotCab / 编辑图片   {0} × {1} · {2}%",
                Canvas.Width, Canvas.Height, (int)Math.Round(ZoomFactor * 100));
        }


        internal void ShotCabRefreshMaximizeButton()
        {
            if (shotCabMaximizeButton == null) return;

            bool maximized = WindowState == FormWindowState.Maximized;
            // Resize fires often; only touch the button when the window state actually changed.
            if (shotCabMaximizeStateApplied && maximized == shotCabMaximized) return;
            shotCabMaximizeStateApplied = true; shotCabMaximized = maximized;

            shotCabMaximizeButton.Glyph = maximized ? ShotCabWindowGlyph.Restore : ShotCabWindowGlyph.Maximize;
            shotCabMaximizeButton.AccessibleName = maximized ? (ShotCabEnglish ? "Restore" : "还原窗口") : (ShotCabEnglish ? "Maximize" : "最大化");
            ShotCabSetChromeHint(shotCabMaximizeButton, maximized
                ? (ShotCabEnglish ? "Restore window" : "还原窗口")
                : (ShotCabEnglish ? "Maximize window" : "最大化窗口"));
        }

        private void ShotCabSetChromeHint(ShotCabWindowButton button, string text)
        {
            if (shotCabChromeTips != null && button != null) shotCabChromeTips.SetToolTip(button, text);
        }

        internal void ShotCabToggleMaximize()
        {
            if (shotCabCaptionBar == null) return;

            MaximizedBounds = Screen.FromControl(this).WorkingArea;
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
            ShotCabRefreshMaximizeButton();
        }

        private void ShotCabCaption_MouseDown(object sender, MouseEventArgs e)
        {
            // Dragging a maximized window would move it away from the working area.
            if (e.Button != MouseButtons.Left || WindowState == FormWindowState.Maximized) return;

            NativeMethods.ReleaseCapture();
            NativeMethods.DefWindowProc(Handle, (uint)WindowsMessages.SYSCOMMAND, (UIntPtr)NativeConstants.MOUSE_MOVE, IntPtr.Zero);
        }

        protected override void WndProc(ref Message m)
        {
            // FormBorderStyle.None removes the sizing border; answer the hit test ourselves
            // so the window stays resizable from every edge and corner.
            if (shotCabCaptionBar != null && m.Msg == HitTestMessage && WindowState == FormWindowState.Normal)
            {
                long value = m.LParam.ToInt64();
                Point client = PointToClient(new Point(unchecked((short)(value & 0xFFFF)), unchecked((short)((value >> 16) & 0xFFFF))));
                int border = (int)Math.Round(ShotCabResizeBorderThickness * DeviceDpi / 96.0);
                bool left = client.X < border, right = client.X >= ClientSize.Width - border;
                bool top = client.Y < border, bottom = client.Y >= ClientSize.Height - border;

                if (left || right || top || bottom)
                {
                    WindowHitTestRegions region = top
                        ? (left ? WindowHitTestRegions.HTTOPLEFT : right ? WindowHitTestRegions.HTTOPRIGHT : WindowHitTestRegions.HTTOP)
                        : bottom
                        ? (left ? WindowHitTestRegions.HTBOTTOMLEFT : right ? WindowHitTestRegions.HTBOTTOMRIGHT : WindowHitTestRegions.HTBOTTOM)
                        : left ? WindowHitTestRegions.HTLEFT : WindowHitTestRegions.HTRIGHT;

                    m.Result = (IntPtr)(int)region;
                    return;
                }
            }

            base.WndProc(ref m);
        }
    }

    internal enum ShotCabWindowGlyph
    {
        Minimize,
        Maximize,
        Restore,
        Close,
        Copy
    }

    /// <summary>
    /// Rounded soft window button. Derives from Control so no native rectangular button
    /// face is painted underneath the rounded surface.
    /// </summary>
    internal sealed class ShotCabWindowButton : Control
    {
        private bool hot, dark = true;
        private ShotCabWindowGlyph glyph;

        internal ShotCabWindowButton(ShotCabWindowGlyph glyph)
        {
            this.glyph = glyph;
            AccessibleRole = AccessibleRole.PushButton;
            TabStop = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        internal ShotCabWindowGlyph Glyph
        {
            get { return glyph; }
            set { if (glyph != value) { glyph = value; Invalidate(); } }
        }

        internal bool Dark
        {
            set { if (dark != value) { dark = value; Invalidate(); } }
        }

        protected override Size DefaultSize
        {
            get { return new Size(44, 30); }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hot = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hot = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle box = new Rectangle(1, 1, Width - 3, Height - 3);
            if (box.Width < 2 || box.Height < 2) return;

            bool close = glyph == ShotCabWindowGlyph.Close;
            Color face = hot
                ? close
                    ? (dark ? Color.FromArgb(178, 64, 70) : Color.FromArgb(248, 221, 223))
                    : ShotCabSoftRenderer.ItemHotOf(dark)
                : ShotCabSoftRenderer.ItemFillOf(dark);

            using (GraphicsPath path = ShotCabSoftRenderer.Rounded(box, 9))
            using (Brush brush = new LinearGradientBrush(box, ControlPaint.Light(face, dark ? .06f : .12f), face, 90f))
            using (Pen border = new Pen(Color.FromArgb(hot ? 150 : 60, dark ? Color.White : ShotCabSoftRenderer.AccentOf(false))))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            }

            Color ink = hot && close ? Color.White : ShotCabSoftRenderer.ForegroundOf(dark);
            using (Pen pen = new Pen(ink, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float centerX = Width / 2f, centerY = Height / 2f;
                switch (glyph)
                {
                    case ShotCabWindowGlyph.Minimize:
                        e.Graphics.DrawLine(pen, centerX - 6, centerY + 0.5f, centerX + 6, centerY + 0.5f);
                        break;
                    case ShotCabWindowGlyph.Maximize:
                        e.Graphics.DrawRectangle(pen, centerX - 6, centerY - 6, 12, 12);
                        break;
                    case ShotCabWindowGlyph.Restore:
                        e.Graphics.DrawRectangle(pen, centerX - 7, centerY - 3.5f, 10.5f, 10.5f);
                        e.Graphics.DrawLines(pen, new[]
                        {
                            new PointF(centerX - 4, centerY - 3.5f), new PointF(centerX - 4, centerY - 7.5f),
                            new PointF(centerX + 3, centerY - 7.5f), new PointF(centerX + 3, centerY - 3.5f)
                        });
                        break;
                    case ShotCabWindowGlyph.Close:
                        e.Graphics.DrawLine(pen, centerX - 5.5f, centerY - 5.5f, centerX + 5.5f, centerY + 5.5f);
                        e.Graphics.DrawLine(pen, centerX - 5.5f, centerY + 5.5f, centerX + 5.5f, centerY - 5.5f);
                        break;
                    case ShotCabWindowGlyph.Copy:
                        e.Graphics.DrawRectangle(pen, centerX - 4.5f, centerY - 4.5f, 9, 10);
                        e.Graphics.DrawLines(pen, new[] { new PointF(centerX - 7, centerY + 3), new PointF(centerX - 7, centerY - 7), new PointF(centerX + 2, centerY - 7) });
                        break;
                }
            }
        }
    }

}
