using System;
using System.Drawing;
using System.Windows.Forms;

namespace ShotCab.App
{
    internal sealed class PinForm : Form
    {
        private readonly CabinetContext app;
        private readonly Bitmap image;
        private Point drag;
        private bool dragging, through;
        private readonly bool card;
        public string ItemId { get; }
        public PinForm(CabinetContext app, Bitmap source, string id, bool previewCard = false)
        {
            card = previewCard;
            this.app = app; image = new Bitmap(source); ItemId = id;
            Ui.Style(this); Text = "ShotCab · 贴图"; TopMost = true; ShowInTaskbar = false; FormBorderStyle = FormBorderStyle.SizableToolWindow;
            DoubleBuffered = true; MinimumSize = new Size(100, 80);
            double scale = Math.Min(1, Math.Min(800d / image.Width, 650d / image.Height)); ClientSize = new Size((int)(image.Width * scale), (int)(image.Height * scale));
            StartPosition = FormStartPosition.Manual; Location = new Point(Screen.PrimaryScreen.WorkingArea.Left + 60, Screen.PrimaryScreen.WorkingArea.Top + 70);
            if (card)
            {
                Text = "ShotCab · 预览卡片"; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = true;
                MinimumSize = new Size(240, 160); ClientSize = new Size(420, 320);
                MaximizedBounds = Screen.FromPoint(Cursor.Position).WorkingArea;
                var bar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Ui.Surface, Padding = new Padding(4) };
                var title = new Label { Text = "预览卡片", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
                title.MouseDown += (s,e) => { if (e.Button == MouseButtons.Left) { Native.ReleaseCapture(); Native.SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero); } };
                title.DoubleClick += (s,e) => ToggleMaximize(); bar.Controls.Add(title);
                foreach (var spec in new[] { Tuple.Create("—", (Action)(() => WindowState = FormWindowState.Minimized), "最小化"), Tuple.Create("□", (Action)ToggleMaximize, "最大化或还原"), Tuple.Create("×", (Action)Close, "关闭") })
                {
                    var button = Ui.GlassButton(spec.Item1, spec.Item2); button.AccessibleName = spec.Item3; button.AutoSize = false; button.MinimumSize = Size.Empty; button.Width = 36; button.Dock = DockStyle.Right; bar.Controls.Add(button);
                }
                Controls.Add(bar); SetStyle(ControlStyles.ResizeRedraw, true);
            }
            var menu = new ContextMenuStrip();
            menu.Items.Add("复制图片", null, (s, e) => app.Safe(() => app.Clipboard.Copy(image, ItemId)));
            menu.Items.Add("选字 / OCR", null, (s, e) => new OcrForm(app, image, id == null ? null : app.Store.Get(id)).Show());
            menu.Items.Add("标注 / 修改", null, (s, e) => { var item = id == null ? null : app.Store.Get(id); var copy = item == null ? new Bitmap(image) : Images.Load(item.OriginalPath); Close(); app.Edit(copy, item); });
            menu.Items.Add("鼠标穿透（托盘可恢复全部）", null, (s, e) => SetClickThrough(!through));
            menu.Items.Add("100% 大小", null, (s, e) => ClientSize = image.Size);
            menu.Items.Add("导出", null, (s, e) => app.Safe(() => app.Export(image)));
            menu.Items.Add("关闭贴图", null, (s, e) => Close()); Ui.StyleMenu(menu); ContextMenuStrip = menu;
            MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { drag = e.Location; dragging = true; Capture = true; } };
            MouseMove += (s, e) => { if (dragging) Location = new Point(Location.X + e.X - drag.X, Location.Y + e.Y - drag.Y); };
            MouseUp += (s, e) => { dragging = false; Capture = false; };
            MouseWheel += (s, e) => { if ((ModifierKeys & Keys.Control) != 0) Opacity = Math.Max(.15, Math.Min(1, Opacity + (e.Delta > 0 ? .05 : -.05))); else { double factor = e.Delta > 0 ? 1.1 : 1 / 1.1; ClientSize = new Size(Math.Max(80, Math.Min(5000, (int)(ClientSize.Width * factor))), Math.Max(60, Math.Min(5000, (int)(ClientSize.Height * factor)))); } };
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); if (e.Control && e.KeyCode == Keys.C) app.Safe(() => app.Clipboard.Copy(image, ItemId)); };
        }
        internal void ToggleMaximize() { MaximizedBounds = Screen.FromControl(this).WorkingArea; WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; }
        protected override void WndProc(ref Message m)
        {
            if (card && m.Msg == 0x84 && WindowState == FormWindowState.Normal)
            {
                var p = PointToClient(new Point(unchecked((short)(long)m.LParam), unchecked((short)((long)m.LParam >> 16))));
                bool left = p.X < 7, right = p.X >= ClientSize.Width - 7, top = p.Y < 7, bottom = p.Y >= ClientSize.Height - 7;
                int hit = top ? (left ? 13 : right ? 14 : 12) : bottom ? (left ? 16 : right ? 17 : 15) : left ? 10 : right ? 11 : 0;
                if (hit != 0) { m.Result = new IntPtr(hit); return; }
            }
            base.WndProc(ref m);
        }
        public void SetClickThrough(bool enabled) { through = enabled; int style = Native.GetWindowLong(Handle, -20); Native.SetWindowLong(Handle, -20, enabled ? style | 0x20 | 0x80000 : style & ~0x20); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Ui.Background); e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            int header = card ? 38 : 0;
            double scale = Math.Min((double)ClientSize.Width / image.Width, (double)(ClientSize.Height - header) / image.Height);
            int width = Math.Max(1, (int)(image.Width * scale)), height = Math.Max(1, (int)(image.Height * scale));
            e.Graphics.DrawImage(image, new Rectangle((ClientSize.Width - width) / 2, header + (ClientSize.Height - header - height) / 2, width, height));
        }
        protected override void Dispose(bool disposing) { if (disposing) { image.Dispose(); ContextMenuStrip?.Dispose(); } base.Dispose(disposing); }
    }
}
