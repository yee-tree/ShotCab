using ShotCab.Core;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace ShotCab.App
{
    internal sealed class CaptureResultBar : Form
    {
        private readonly CabinetContext app;
        private readonly HistoryItem record;
        private readonly Bitmap thumbnail;
        private readonly Timer expiry = new Timer { Interval = 5000 };
        private readonly FlowLayoutPanel actions;
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams { get { var value = base.CreateParams; value.ExStyle |= 0x80 | 0x08000000; return value; } }

        internal CaptureResultBar(CabinetContext app, HistoryItem record, Bitmap image)
        {
            this.app = app;
            this.record = record;
            thumbnail = new Bitmap(62, 56);
            using (var graphics = Graphics.FromImage(thumbnail))
            {
                graphics.Clear(Ui.Dark ? Color.FromArgb(45, 52, 66) : Color.FromArgb(235, 241, 251));
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                double scale = Math.Min(62d / image.Width, 56d / image.Height);
                int width = Math.Max(1, (int)(image.Width * scale)), height = Math.Max(1, (int)(image.Height * scale));
                graphics.DrawImage(image, new Rectangle((62 - width) / 2, (56 - height) / 2, width, height));
            }
            Ui.Style(this);
            Text = Localize.T("截图已完成");
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(516, 142);
            BackColor = Ui.Surface;
            DoubleBuffered = true;

            var preview = new PictureBox { Image = thumbnail, SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(17, 19, 62, 56), BackColor = Color.Transparent };
            Controls.Add(preview);
            var title = new Label { Text = Localize.T("截图已完成"), Font = Ui.Font(12, FontStyle.Bold), ForeColor = Ui.Text, Bounds = new Rectangle(93, 17, 350, 27), BackColor = Color.Transparent };
            Controls.Add(title);
            var detail = new Label { Text = Localize.T(record == null ? "已复制；历史未保存" : "已复制到剪贴板 · 已加入近期"), Font = Ui.Font(9), ForeColor = Ui.Muted, Bounds = new Rectangle(94, 48, 355, 26), BackColor = Color.Transparent };
            Controls.Add(detail);
            var close = Ui.GlassButton("×", Close);
            close.AccessibleName = Localize.T("关闭结果条"); close.AutoSize = false; close.MinimumSize = Size.Empty; close.Bounds = new Rectangle(466, 15, 34, 32); Controls.Add(close);

            actions = new FlowLayoutPanel { Bounds = new Rectangle(17, 89, 482, 42), FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent };
            AddAction("编辑图片", () =>
            {
                if (record != null) app.Edit(Images.Load(record.OriginalPath), record);
                else app.Edit(app.LastCaptureImage());
            });
            AddAction("贴图", () =>
            {
                if (record != null) using (var current = Images.Load(record.CurrentPath)) app.Pin(current, record.Id);
                else using (var current = app.LastCaptureImage()) app.Pin(current, null);
            });
            AddAction("识别文字", () =>
            {
                if (record != null) using (var current = Images.Load(record.CurrentPath)) new OcrForm(app, current, record).Show();
                else using (var current = app.LastCaptureImage()) new OcrForm(app, current, null).Show();
            });
            if (record != null) AddAction("打开文件", () => Process.Start(new ProcessStartInfo(record.CurrentPath) { UseShellExecute = true }));
            Controls.Add(actions);
            expiry.Tick += (sender, args) => Close();
            MouseEnter += (sender, args) => expiry.Stop();
            MouseLeave += (sender, args) => RestartIfOutside();
            foreach (Control child in Controls) { child.MouseEnter += (sender, args) => expiry.Stop(); child.MouseLeave += (sender, args) => RestartIfOutside(); }
            Shown += (sender, args) => { using (var shape = Ui.Rounded(new Rectangle(0, 0, Width, Height), 17)) Region = new Region(shape); expiry.Start(); };
        }

        private void AddAction(string label, Action action)
        {
            var button = Ui.GlassButton(Localize.T(label), () => { Close(); app.Safe(action); });
            button.AutoSize = false; button.MinimumSize = Size.Empty;
            button.Size = new Size(label == "识别文字" ? 104 : 96, 37);
            button.Margin = new Padding(0, 0, 7, 0);
            actions.Controls.Add(button);
        }

        internal void PlaceNear(Screen display, SidebarForm sidebar)
        {
            Rectangle area = display.WorkingArea;
            int rightInset = sidebar != null && sidebar.Visible && sidebar.Bounds.IntersectsWith(area) && app.Settings.SidebarSide == SidebarDockSide.靠右 ? sidebar.Width + 18 : 18;
            Location = new Point(Math.Max(area.Left + 10, area.Right - rightInset - Width), area.Bottom - Height - 18);
        }

        private void RestartIfOutside()
        {
            if (!Bounds.Contains(Cursor.Position)) { expiry.Stop(); expiry.Start(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(90, Ui.Accent))) e.Graphics.DrawLine(pen, 94, 81, Width - 19, 81);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { expiry.Dispose(); thumbnail.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
