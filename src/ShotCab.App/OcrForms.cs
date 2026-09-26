using Newtonsoft.Json;
using ShotCab.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShotCab.App
{
    internal sealed class OcrDocument { public int Version { get; set; } public string Text { get; set; } public List<OcrLine> Lines { get; set; } = new List<OcrLine>(); }
    internal sealed class OcrLine { public string Text { get; set; } public Point[] Points { get; set; } public List<OcrCharacter> Chars { get; set; } = new List<OcrCharacter>(); }
    internal sealed class OcrCharacter { public string Text { get; set; } public float Score { get; set; } public Point[] Points { get; set; } }
    internal static class OcrService
    {
        private sealed class PointConverter : JsonConverter<Point>
        {
            public override bool CanWrite => false;
            public override Point ReadJson(JsonReader reader, Type objectType, Point existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var value = Newtonsoft.Json.Linq.JObject.Load(reader);
                if (value["X"]?.Type != Newtonsoft.Json.Linq.JTokenType.Integer || value["Y"]?.Type != Newtonsoft.Json.Linq.JTokenType.Integer) throw new JsonSerializationException("OCR坐标必须为整数像素。");
                return new Point((int)value["X"], (int)value["Y"]);
            }
            public override void WriteJson(JsonWriter writer, Point value, JsonSerializer serializer) => throw new NotSupportedException();
        }
        public static async Task<OcrDocument> Recognize(CabinetContext app, Bitmap image, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var executable = app.Settings.OcrExecutablePath;
            if (string.IsNullOrWhiteSpace(executable)) executable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocr", "ShotCab.Ocr.exe");
            if (!File.Exists(executable)) throw new FileNotFoundException("未安装离线 OCR。安装版可重新运行安装程序并勾选“离线 OCR”；便携版可在设置 → OCR/其他中选择完整 OCR 组件的 ShotCab.Ocr.exe。");
            var directory = Path.Combine(app.Store.RootPath, "ocr-jobs", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            var input = Path.Combine(directory, "input.png"); var output = Path.Combine(directory, "result.json");
            try
            {
                image.Save(input, System.Drawing.Imaging.ImageFormat.Png);
                cancellation.ThrowIfCancellationRequested();
                using (var process = new Process { StartInfo = new ProcessStartInfo(executable, "\"" + input + "\" \"" + output + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, WorkingDirectory = Path.GetDirectoryName(executable) } })
                {
                    process.Start(); var errors = process.StandardError.ReadToEndAsync(); var standard = process.StandardOutput.ReadToEndAsync();
                    using (cancellation.Register(() => { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } }))
                    {
                        await Task.Run(() => process.WaitForExit()); cancellation.ThrowIfCancellationRequested();
                        var errorText = await errors; await standard;
                        if (process.ExitCode != 0 || !File.Exists(output)) throw new InvalidOperationException("离线识别失败：" + errorText);
                    }
                }
                var document = JsonConvert.DeserializeObject<OcrDocument>(File.ReadAllText(output), new PointConverter());
                if (document?.Version != 1 || document.Lines == null) throw new InvalidDataException("OCR组件返回了不支持的结果。");
                NormalizeReadingOrder(document);
                return document;
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
        internal static void NormalizeReadingOrder(OcrDocument document)
        {
            var rows = new List<List<OcrLine>>();
            foreach (var line in document.Lines.Where(l => !string.IsNullOrEmpty(l.Text) && l.Points?.Length >= 3).OrderBy(l => l.Points.Min(p => p.Y)))
            {
                int top = line.Points.Min(p => p.Y), bottom = line.Points.Max(p => p.Y);
                var row = rows.LastOrDefault(r =>
                {
                    int rt = r.Min(l => l.Points.Min(p => p.Y)), rb = r.Max(l => l.Points.Max(p => p.Y));
                    return Math.Min(bottom, rb) - Math.Max(top, rt) >= Math.Min(bottom - top, rb - rt) * .6;
                });
                if (row == null) { row = new List<OcrLine>(); rows.Add(row); }
                row.Add(line);
            }
            var output = new List<OcrLine>();
            foreach (var row in rows)
            {
                var blocks = row.OrderBy(l => l.Points.Min(p => p.X)).ToArray();
                string text = ""; var chars = new List<OcrCharacter>(); OcrLine previous = null;
                foreach (var block in blocks)
                {
                    string part = block.Text;
                    var blockChars = (block.Chars ?? new List<OcrCharacter>()).ToList();
                    if (chars.Count > 0 && blockChars.Count > 0 && chars.Last().Text == blockChars[0].Text)
                    {
                        var a = chars.Last().Points; var b = blockChars[0].Points;
                        int overlap = Math.Min(a.Max(p => p.X), b.Max(p => p.X)) - Math.Max(a.Min(p => p.X), b.Min(p => p.X));
                        int width = Math.Min(a.Max(p => p.X) - a.Min(p => p.X), b.Max(p => p.X) - b.Min(p => p.X));
                        if (overlap > width * .4 && part.StartsWith(blockChars[0].Text, StringComparison.Ordinal)) { part = part.Substring(blockChars[0].Text.Length); blockChars.RemoveAt(0); }
                    }
                    if (previous != null)
                    {
                        int gap = block.Points.Min(p => p.X) - previous.Points.Max(p => p.X);
                        double width = previous.Chars?.Count > 0 ? previous.Chars.Average(c => c.Points.Max(p => p.X) - c.Points.Min(p => p.X)) : 16;
                        if (gap > width * 2) text += "\t";
                        else if (gap > width * .35 && !text.EndsWith(" ") && !block.Text.StartsWith(" ")) text += " ";
                    }
                    text += part; chars.AddRange(blockChars); previous = block;
                }
                int left = blocks.Min(b => b.Points.Min(p => p.X)), right = blocks.Max(b => b.Points.Max(p => p.X));
                int top = blocks.Min(b => b.Points.Min(p => p.Y)), bottom = blocks.Max(b => b.Points.Max(p => p.Y));
                output.Add(new OcrLine { Text = text, Chars = chars, Points = new[] { new Point(left, top), new Point(right, top), new Point(right, bottom), new Point(left, bottom) } });
            }
            document.Lines = output; document.Text = string.Join(Environment.NewLine, output.Select(l => l.Text));
        }
    }

    internal sealed class OcrCanvas : Control
    {
        private sealed class Glyph { public int Line, Start, End; public RectangleF Box; public Point[] Polygon; }
        private readonly Bitmap image;
        private readonly List<Glyph> glyphs = new List<Glyph>();
        private OcrDocument document;
        private int anchor = -1, end = -1;
        public event Action SelectionChanged;
        public event Action<string> CopyRequested;
        public event Action<int> ZoomRequested;
        public OcrCanvas(Bitmap image) { this.image = new Bitmap(image); DoubleBuffered = true; Cursor = Cursors.IBeam; TabStop = true; Size = image.Size; }
        public void SetDocument(OcrDocument result)
        {
            document = result; glyphs.Clear();
            for (int lineIndex = 0; lineIndex < result.Lines.Count; lineIndex++)
            {
                var line = result.Lines[lineIndex]; int start = 0;
                foreach (var ch in line.Chars ?? new List<OcrCharacter>())
                {
                    if (ch.Points == null || ch.Points.Length < 3 || string.IsNullOrEmpty(ch.Text)) continue;
                    int found = line.Text.IndexOf(ch.Text, start, StringComparison.Ordinal); if (found < 0) found = start;
                    glyphs.Add(new Glyph { Line = lineIndex, Start = found, End = Math.Min(line.Text.Length, found + ch.Text.Length), Polygon = ch.Points, Box = RectangleF.FromLTRB(ch.Points.Min(p => p.X), ch.Points.Min(p => p.Y), ch.Points.Max(p => p.X), ch.Points.Max(p => p.Y)) }); start = Math.Min(line.Text.Length, found + ch.Text.Length);
                }
                if (line.Chars == null || line.Chars.Count == 0)
                {
                    var p = line.Points; if (p != null && p.Length >= 3) glyphs.Add(new Glyph { Line = lineIndex, Start = 0, End = line.Text.Length, Polygon = p, Box = RectangleF.FromLTRB(p.Min(v => v.X), p.Min(v => v.Y), p.Max(v => v.X), p.Max(v => v.Y)) });
                }
            }
            Invalidate();
        }
        public string SelectedText
        {
            get
            {
                if (anchor < 0 || end < 0 || document == null) return "";
                var chosen = glyphs.Skip(Math.Min(anchor, end)).Take(Math.Abs(end - anchor) + 1).GroupBy(g => g.Line);
                return string.Join(Environment.NewLine, chosen.Select(group => document.Lines[group.Key].Text.Substring(group.Min(g => g.Start), group.Max(g => g.End) - group.Min(g => g.Start))));
            }
        }
        public void SelectAllText() { anchor = glyphs.Count > 0 ? 0 : -1; end = glyphs.Count - 1; Invalidate(); SelectionChanged?.Invoke(); }
        public void Zoom(float factor) { Size = new Size(Math.Max(1, (int)(image.Width * factor)), Math.Max(1, (int)(image.Height * factor))); Invalidate(); }
        private int Hit(Point point)
        {
            if (glyphs.Count == 0) return -1;
            var p = new PointF(point.X * image.Width / (float)Width, point.Y * image.Height / (float)Height);
            int closest = 0; double distance = double.MaxValue;
            for (int i = 0; i < glyphs.Count; i++) { var b = glyphs[i].Box; if (b.Contains(p)) return i; double dx = p.X - Math.Max(b.Left, Math.Min(b.Right, p.X)), dy = p.Y - Math.Max(b.Top, Math.Min(b.Bottom, p.Y)); double d = dx * dx + dy * dy; if (d < distance) { distance = d; closest = i; } }
            return closest;
        }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Focus(); Capture = true; anchor = end = Hit(e.Location); Invalidate(); SelectionChanged?.Invoke(); } base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (Capture && e.Button == MouseButtons.Left) { end = Hit(e.Location); Invalidate(); SelectionChanged?.Invoke(); } base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { Capture = false; base.OnMouseUp(e); }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.A)) { SelectAllText(); return true; }
            if (keyData == (Keys.Control | Keys.C)) { CopyRequested?.Invoke(SelectedText); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) == Keys.Control && ZoomRequested != null)
                ZoomRequested(e.Delta);
            else base.OnMouseWheel(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic; e.Graphics.DrawImage(image, ClientRectangle);
            float sx = Width / (float)image.Width, sy = Height / (float)image.Height;
            e.Graphics.ScaleTransform(sx, sy);
            using (var selected = new SolidBrush(Color.FromArgb(95, 60, 141, 255)))
            using (var outline = new Pen(Color.FromArgb(90, 34, 126, 209), 1 / sx))
                for (int i = 0; i < glyphs.Count; i++) { var points = glyphs[i].Polygon.Select(p => new PointF(p.X, p.Y)).ToArray(); if (anchor >= 0 && i >= Math.Min(anchor, end) && i <= Math.Max(anchor, end)) e.Graphics.FillPolygon(selected, points); else e.Graphics.DrawPolygon(outline, points); }
        }
        protected override void Dispose(bool disposing) { if (disposing) image.Dispose(); base.Dispose(disposing); }
    }

    internal sealed class OcrForm : Form
    {
        private readonly CabinetContext app; private readonly Bitmap image; private readonly HistoryItem item;
        private readonly OcrCanvas canvas; private readonly TextBox text; private readonly Label status;
        private readonly CancellationTokenSource cancel = new CancellationTokenSource();
        private bool released;
        public OcrForm(CabinetContext app, Bitmap image, HistoryItem item)
        {
            this.app = app; this.image = new Bitmap(image); this.item = item; Ui.Style(this); Text = "ShotCab · 图片选字与文字核对"; Size = new Size(1050, 690);
            var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(1020, 580), SplitterDistance = 650, Panel2MinSize = 240 };
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Ui.Surface };
            canvas = new OcrCanvas(image); scroll.Controls.Add(canvas); split.Panel1.Controls.Add(scroll);
            text = new TextBox { Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = true, AcceptsTab = true, ScrollBars = ScrollBars.Both, WordWrap = true, BackColor = Ui.Surface, ForeColor = Ui.Text, BorderStyle = BorderStyle.None };
            split.Panel2.Controls.Add(text); canvas.SelectionChanged += () => { if (canvas.SelectedText.Length > 0) status.Text = "已选择 " + canvas.SelectedText.Length + " 个字符，可按 Ctrl+C 复制"; };
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 49, Padding = new Padding(5) };
            bar.Controls.Add(Ui.Button("复制所选文字", () => Copy(canvas.SelectedText)));
            bar.Controls.Add(Ui.Button("复制文字窗", () => Copy(text.SelectionLength > 0 ? text.SelectedText : text.Text)));
            bar.Controls.Add(Ui.Button("全选图片文字", canvas.SelectAllText));
            bar.Controls.Add(Ui.Button("适应窗口", () => canvas.Zoom((float)Math.Min(1, Math.Min((scroll.ClientSize.Width - 20d) / this.image.Width, (scroll.ClientSize.Height - 20d) / this.image.Height)))));
            bar.Controls.Add(Ui.Button("100%", () => canvas.Zoom(1))); bar.Controls.Add(Ui.Button("取消识别", () => cancel.Cancel()));
            status = new Label { Dock = DockStyle.Bottom, Height = 36, ForeColor = Ui.Muted, Padding = new Padding(12, 8, 0, 0), Text = "正在加载离线识别组件…" };
            Controls.Add(split); Controls.Add(bar); Controls.Add(status); KeyPreview = true;
            KeyDown += (s, e) => { if (canvas.Focused && e.Control && e.KeyCode == Keys.A) { canvas.SelectAllText(); e.SuppressKeyPress = true; } if (canvas.Focused && e.Control && e.KeyCode == Keys.C) { Copy(canvas.SelectedText); e.SuppressKeyPress = true; } };
            Shown += async (s, e) =>
            {
                canvas.Zoom((float)Math.Min(1, (scroll.ClientSize.Width - 20d) / this.image.Width));
                try
                {
                    var result = await OcrService.Recognize(app, this.image, cancel.Token); if (IsDisposed) return;
                    canvas.SetDocument(result); text.Text = result.Text;
                    status.Text = result.Lines.Count == 0 ? "未识别到文字。可尝试更清晰、缩放更大的截图。" : "在左侧图片拖选文字；右侧可修改后复制。识别进程已释放。";
                    if (item != null) app.Store.SetOcr(item.Id, item.UpdatedUtc, result.Text);
                    app.Refresh();
                }
                catch (OperationCanceledException) { if (!IsDisposed) status.Text = "识别已取消，未修改历史文字。"; }
                catch (Exception ex) { if (!IsDisposed) status.Text = ex.Message; }
            };
            FormClosing += (s, e) => cancel.Cancel();
            app.AcquireRecord(item?.Id);
        }
        private void Copy(string value) { if (string.IsNullOrEmpty(value)) return; app.Safe(() => { app.Clipboard.CopyText(value); if (app.Settings.OcrPreviewSeconds > 0) new TextPreview(value, app.Settings.OcrPreviewSeconds).Show(); }); }
        protected override void Dispose(bool disposing) { if (disposing && !released) { released = true; cancel.Cancel(); image.Dispose(); app.ReleaseRecord(item?.Id); } base.Dispose(disposing); }
    }

    internal sealed class TextResultForm : Form
    {
        public TextResultForm(CabinetContext app, string value)
        {
            Ui.Style(this); Text = "ShotCab · 文字"; Size = new Size(650, 430);
            var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, Text = value, ScrollBars = ScrollBars.Both, ForeColor = Ui.Text, BackColor = Ui.Surface };
            var copy = Ui.Button("复制选中 / 全部文字", () => app.Safe(() => { var result = text.SelectionLength > 0 ? text.SelectedText : text.Text; if (result.Length == 0) return; app.Clipboard.CopyText(result); if (app.Settings.OcrPreviewSeconds > 0) new TextPreview(result, app.Settings.OcrPreviewSeconds).Show(); })); copy.Dock = DockStyle.Bottom;
            Controls.Add(text); Controls.Add(copy);
        }
    }
    internal sealed class TextPreview : Form
    {
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 150 };
        private int remaining;
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x80; return p; } }
        public TextPreview(string text, int seconds)
        {
            Ui.Style(this); Text = "已复制文字"; FormBorderStyle = FormBorderStyle.FixedToolWindow; TopMost = true; ShowInTaskbar = false; ClientSize = new Size(380, 160);
            StartPosition = FormStartPosition.Manual; var area = Screen.FromPoint(Cursor.Position).WorkingArea; Location = new Point(Math.Max(area.Left, Math.Min(area.Right - Width, Cursor.Position.X + 18)), Math.Max(area.Top, Math.Min(area.Bottom - Height, Cursor.Position.Y + 20)));
            Controls.Add(new Label { Dock = DockStyle.Fill, Padding = new Padding(12), Text = "已复制\n\n" + text, AutoEllipsis = true }); remaining = seconds * 1000;
            timer.Tick += (s, e) => { if (!Bounds.Contains(Cursor.Position)) remaining -= timer.Interval; if (remaining <= 0) Close(); }; timer.Start();
        }
        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }
}
