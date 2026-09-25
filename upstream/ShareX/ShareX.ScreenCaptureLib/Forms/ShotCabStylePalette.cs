// ShotCab drawing-style popover. Upstream GPL license applies.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace ShareX.ScreenCaptureLib
{
    internal sealed class ShotCabStylePalette : Form
    {
        private readonly bool hasFill, dark, english;
        private readonly Action<Color, int, Color> apply;
        private readonly List<ShotCabPaletteChoice> choices = new List<ShotCabPaletteChoice>();
        private ShotCabWidthEntry customWidth;
        private Color border, fill;
        private int width;

        internal ShotCabStylePalette(string toolName, bool hasFill, Color border, int width, Color fill,
            bool dark, bool english, Action<Color, int, Color> apply)
        {
            this.hasFill = hasFill; this.border = border; this.width = width; this.fill = fill;
            this.dark = dark; this.english = english; this.apply = apply;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = ShotCabSoftRenderer.SurfaceOf(dark);
            ForeColor = ShotCabSoftRenderer.ForegroundOf(dark);
            Font = new Font("Microsoft YaHei UI", 9f);
            ClientSize = new Size(320, hasFill ? 248 : 188);
            DoubleBuffered = true;
            AccessibleName = english ? "Drawing style" : "绘图样式";
            using (var rounded = ShotCabSoftRenderer.Rounded(new Rectangle(Point.Empty, Size), 14)) Region = new Region(rounded);

            AddText(english ? toolName + " style" : toolName + "样式", 16, 10, 250, 27, true);
            var close = AddChoice(english ? "Close" : "关闭", "×", 274, 10, 30, 30, null, false,
                () => Close());
            close.AccessibleName = english ? "Close style palette" : "关闭样式面板";
            AddText(english ? "Choose a color or width to apply it" : "点击颜色或粗细即可生效", 16, 37, 285, 19, false);

            AddText(english ? "Stroke color" : "边框 / 线条颜色", 16, 62, 270, 20, false);
            AddColorRow(true, 84);
            int widthY = hasFill ? 190 : 130;
            if (hasFill)
            {
                AddText(english ? "Fill color" : "内部填充", 16, 126, 270, 20, false);
                AddColorRow(false, 148);
            }
            AddText(english ? "Stroke width" : "线条粗细", 16, widthY, 270, 20, false);
            AddWidthRow(widthY + 23);

            RefreshChoices();
        }

        private void AddText(string text, int x, int y, int width, int height, bool strong)
        {
            var label = new Label
            {
                Text = text, Bounds = new Rectangle(x, y, width, height),
                Font = new Font("Microsoft YaHei UI", strong ? 10.5f : 8.6f, strong ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = strong ? (dark ? Color.White : Color.FromArgb(38, 44, 50)) : ForeColor,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(label);
        }

        private ShotCabPaletteChoice AddChoice(string accessible, string caption, int x, int y, int width, int height,
            Color? swatch, bool accent, Action action)
        {
            var button = new ShotCabPaletteChoice(dark, caption, swatch, accent)
            {
                Bounds = new Rectangle(x, y, width, height), AccessibleName = accessible,
                AccessibleRole = AccessibleRole.PushButton
            };
            button.Click += (sender, args) => action();
            Controls.Add(button);
            choices.Add(button);
            return button;
        }

        private void AddColorRow(bool stroke, int y)
        {
            Color[] colors = (stroke
                ? new[] { border, Color.FromArgb(53, 111, 220), Color.FromArgb(225, 75, 84),
                    Color.FromArgb(39, 154, 113), Color.FromArgb(239, 184, 74), Color.White, Color.Black }
                : new[] { Color.Transparent, fill.A == 0 ? Color.FromArgb(235, 238, 243) : fill,
                    Color.FromArgb(53, 111, 220), Color.FromArgb(225, 75, 84),
                    Color.FromArgb(39, 154, 113), Color.White, Color.Black })
                .GroupBy(color => color.ToArgb()).Select(group => group.First()).ToArray();
            for (int i = 0; i < colors.Length; i++)
            {
                Color chosen = colors[i];
                var choice = AddChoice(chosen.A == 0 ? (english ? "No fill" : "透明填充") : chosen.Name,
                    string.Empty, 16 + i * 38, y, 31, 31, chosen, false,
                    () => { if (stroke) border = chosen; else { fill = chosen; if (fill.A == 0 && width == 0) width = 2; } RefreshChoices(); ApplyNow(); });
                choice.Tag = stroke ? "stroke" : "fill";
            }
            var custom = AddChoice(english ? "Custom color" : "自定义颜色", "···", 282, y, 26, 31, null, false,
                () => PickCustomColor(stroke));
            custom.Tag = "custom";
        }

        private void AddWidthRow(int y)
        {
            int[] widths = { 0, 2, 4, 6, 10 };
            for (int i = 0; i < widths.Length; i++)
            {
                int value = widths[i];
                var choice = AddChoice(english ? value + " pixel stroke" : value + " 像素线条", value.ToString(),
                    16 + i * 47, y, 41, 29, null, false,
                    () => { width = value == 0 && hasFill && fill.A == 0 ? 2 : value; RefreshChoices(); ApplyNow(); });
                choice.Tag = value;
            }
            customWidth = new ShotCabWidthEntry(dark, width)
            {
                Bounds = new Rectangle(251, y, 57, 29),
                AccessibleName = english ? "Custom stroke width, 0 to 50 pixels" : "自定义线条粗细，0 到 50 像素"
            };
            customWidth.ValueChanged += value =>
            {
                width = value == 0 && hasFill && fill.A == 0 ? 2 : value;
                RefreshChoices(); ApplyNow();
            };
            Controls.Add(customWidth);
        }

        private void PickCustomColor(bool stroke)
        {
            using (var picker = new ColorDialog { Color = stroke ? border : fill, FullOpen = true, AnyColor = true })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                if (stroke) border = picker.Color;
                else fill = picker.Color;
                RefreshChoices();
                ApplyNow();
            }
        }

        private void ApplyNow() => apply(border, width, fill);

        private void RefreshChoices()
        {
            customWidth?.SetValue(width);
            foreach (var choice in choices)
            {
                if (choice.Tag is string kind && kind == "stroke") choice.Selected = choice.Swatch?.ToArgb() == border.ToArgb();
                else if (choice.Tag is string fillKind && fillKind == "fill") choice.Selected = choice.Swatch?.ToArgb() == fill.ToArgb();
                else if (choice.Tag is int value) choice.Selected = value == width;
                else choice.Selected = false;
                choice.Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(dark ? Color.FromArgb(93, 98, 106) : Color.FromArgb(185, 191, 199)))
            using (var path = ShotCabSoftRenderer.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 14))
                e.Graphics.DrawPath(pen, path);
        }
    }

    internal sealed class ShotCabWidthEntry : Control
    {
        private readonly bool dark;
        private readonly TextBox input;
        private readonly ToolTip hint = new ToolTip();
        private bool syncing;
        internal event Action<int> ValueChanged;

        internal ShotCabWidthEntry(bool dark, int value)
        {
            this.dark = dark;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            input = new TextBox
            {
                BorderStyle = System.Windows.Forms.BorderStyle.None, BackColor = ShotCabSoftRenderer.SurfaceOf(dark),
                ForeColor = ShotCabSoftRenderer.ForegroundOf(dark), TextAlign = HorizontalAlignment.Center,
                Font = new Font("Microsoft YaHei UI", 8.5f), MaxLength = 2, Text = value.ToString()
            };
            input.TextChanged += (sender, args) =>
            {
                if (!syncing && int.TryParse(input.Text, out int width) && width >= 0 && width <= 50)
                    ValueChanged?.Invoke(width);
            };
            input.KeyPress += (sender, args) => { if (!char.IsControl(args.KeyChar) && !char.IsDigit(args.KeyChar)) args.Handled = true; };
            input.Enter += (sender, args) => { input.SelectAll(); Invalidate(); };
            input.Leave += (sender, args) => Invalidate();
            Controls.Add(input);
            string description = System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "en"
                ? "Type a custom stroke width (0–50)" : "输入自定义线条粗细（0–50）";
            hint.SetToolTip(this, description); hint.SetToolTip(input, description);
            Cursor = Cursors.IBeam;
        }

        internal void SetValue(int value)
        {
            if (input.Text == value.ToString()) return;
            syncing = true; input.Text = value.ToString(); syncing = false;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            input.Bounds = new Rectangle(5, Math.Max(2, (Height - input.PreferredHeight) / 2), Math.Max(15, Width - 27), input.PreferredHeight);
        }

        protected override void OnMouseDown(MouseEventArgs e) { input.Focus(); base.OnMouseDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = ShotCabSoftRenderer.Rounded(new Rectangle(1, 1, Width - 3, Height - 3), 8))
            using (var fill = new SolidBrush(ShotCabSoftRenderer.SurfaceOf(dark)))
            using (var border = new Pen(input.Focused ? ShotCabSoftRenderer.AccentOf(dark) :
                dark ? Color.FromArgb(82, 87, 94) : Color.FromArgb(189, 196, 204)))
            { e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(border, path); }
            using (var pencil = new Pen(ShotCabSoftRenderer.ForegroundOf(dark), 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                e.Graphics.DrawLine(pencil, Width - 18, Height - 9, Width - 9, Height - 18);
                e.Graphics.DrawLine(pencil, Width - 19, Height - 8, Width - 15, Height - 8);
            }
        }
        protected override void Dispose(bool disposing) { if (disposing) hint.Dispose(); base.Dispose(disposing); }
    }

    internal sealed class ShotCabPaletteChoice : Control
    {
        private readonly bool dark, accent;
        private bool hot, selected;
        internal Color? Swatch { get; }
        internal bool Selected { get => selected; set { selected = value; Invalidate(); } }

        internal ShotCabPaletteChoice(bool dark, string text, Color? swatch, bool accent)
        {
            this.dark = dark; this.accent = accent; Swatch = swatch; Text = text;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(1, 1, Width - 3, Height - 3);
            if (bounds.Width < 3 || bounds.Height < 3) return;
            Color face = accent ? ShotCabSoftRenderer.AccentOf(dark) :
                hot || selected ? ShotCabSoftRenderer.ItemHotOf(dark) : ShotCabSoftRenderer.SurfaceOf(dark);
            using (var path = ShotCabSoftRenderer.Rounded(bounds, 8))
            using (var brush = new SolidBrush(face))
            using (var edge = new Pen(selected ? ShotCabSoftRenderer.AccentOf(dark) :
                dark ? Color.FromArgb(82, 87, 94) : Color.FromArgb(189, 196, 204), selected ? 2f : 1f))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(edge, path);
            }
            if (Swatch.HasValue)
            {
                Rectangle circle = new Rectangle((Width - 17) / 2, (Height - 17) / 2, 17, 17);
                if (Swatch.Value.A == 0)
                {
                    using (var pen = new Pen(ShotCabSoftRenderer.ForegroundOf(dark), 1.7f))
                    {
                        e.Graphics.DrawEllipse(pen, circle);
                        e.Graphics.DrawLine(pen, circle.Left + 2, circle.Bottom - 2, circle.Right - 2, circle.Top + 2);
                    }
                }
                else
                {
                    using (var brush = new SolidBrush(Swatch.Value)) e.Graphics.FillEllipse(brush, circle);
                    using (var pen = new Pen(Color.FromArgb(100, dark ? Color.White : Color.Black))) e.Graphics.DrawEllipse(pen, circle);
                }
            }
            else
            {
                TextRenderer.DrawText(e.Graphics, Text, Font, bounds,
                    accent ? Color.FromArgb(20, 42, 36) : ShotCabSoftRenderer.ForegroundOf(dark),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }
}
