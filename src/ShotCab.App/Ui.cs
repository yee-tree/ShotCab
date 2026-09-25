using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShotCab.App
{
    internal static class Ui
    {
        public static bool Dark = true;
        private static readonly Lazy<Icon> icon = new Lazy<Icon>(() =>
        {
            using (var stream = typeof(Ui).Assembly.GetManifestResourceStream("ShotCab.App.Icon"))
            {
                if (stream == null) return SystemIcons.Application;
                using (var source = new Icon(stream)) return (Icon)source.Clone();
            }
        });
        public static Icon AppIcon => icon.Value;
        public static Color Background => Dark ? Color.FromArgb(18, 18, 19) : Color.FromArgb(245, 247, 251);
        public static Color Surface => Dark ? Color.FromArgb(34, 34, 36) : Color.White;
        public static Color Accent => Dark ? Color.FromArgb(239, 239, 241) : Color.FromArgb(38, 96, 187);
        public static Color Text => Dark ? Color.FromArgb(243, 243, 245) : Color.FromArgb(30, 40, 58);
        public static Color Muted => Dark ? Color.FromArgb(169, 169, 174) : Color.FromArgb(95, 105, 122);
        public static Font Font(float size = 10, FontStyle style = FontStyle.Regular) => new Font("Microsoft YaHei UI", size, style);
        public static void Style(Form form) { form.Font = Font(); form.BackColor = Background; form.ForeColor = Text; form.Icon = AppIcon; form.StartPosition = FormStartPosition.CenterScreen; form.Load += (s, e) => { ThemeInputs(form); Localize.Apply(form); }; }
        public static void ThemeInputs(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is TextBoxBase || control is ComboBox || control is NumericUpDown || control is ListView || control is CheckedListBox)
                { control.BackColor = Surface; control.ForeColor = Text; }
                if (control is ComboBox combo && combo.DrawMode != DrawMode.OwnerDrawFixed)
                {
                    combo.DrawMode = DrawMode.OwnerDrawFixed;
                    combo.DrawItem += (sender, args) =>
                    {
                        using (var brush = new SolidBrush(Surface)) args.Graphics.FillRectangle(brush, args.Bounds);
                        int index = args.Index >= 0 ? args.Index : combo.SelectedIndex;
                        if (index >= 0) TextRenderer.DrawText(args.Graphics, combo.GetItemText(combo.Items[index]), combo.Font, args.Bounds, Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                        args.DrawFocusRectangle();
                    };
                }
                ThemeInputs(control);
            }
        }
        public static Button Button(string text, Action action) { var b = new Button { Text = text, AutoSize = true, Height = 34, MinimumSize = new Size(65, 32), FlatStyle = FlatStyle.Flat, BackColor = Surface, ForeColor = Text, Margin = new Padding(4), Padding = new Padding(5) }; b.FlatAppearance.BorderColor = Dark ? Color.FromArgb(76, 76, 79) : Color.FromArgb(61, 70, 85); b.Click += (s, e) => action(); return b; }
        public static Control GlassButton(string text, Action action)
        {
            var button = new FrostedButton { Text = text, AutoSize = true, Height = 36, MinimumSize = new Size(65, 34), BackColor = Surface, ForeColor = Text, Margin = new Padding(4), Padding = new Padding(8, 5, 8, 5) };
            button.Click += (s,e) => action(); return button;
        }
        private sealed class FrostedButton : Control
        {
            private bool hover;
            public FrostedButton() { AccessibleRole = AccessibleRole.PushButton; TabStop = true; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
            public override Size GetPreferredSize(Size proposedSize) { var size = TextRenderer.MeasureText(Text, Font); return new Size(Math.Max(MinimumSize.Width, size.Width + Padding.Horizontal + 12), Math.Max(MinimumSize.Height, size.Height + Padding.Vertical + 4)); }
            protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }
            protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnPaintBackground(PaintEventArgs e)
            {
                // ButtonBase paints a native rectangular background even with a custom OnPaint.
                // Paint the parent's surface at the correct offset, then draw only our rounded face.
                if (Parent == null) { e.Graphics.Clear(Background); return; }
                var state = e.Graphics.Save();
                try { e.Graphics.TranslateTransform(-Left, -Top); using (var args = new PaintEventArgs(e.Graphics, Bounds)) { InvokePaintBackground(Parent, args); InvokePaint(Parent, args); } }
                finally { e.Graphics.Restore(state); }
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var box = new Rectangle(1, 1, Width - 3, Height - 3);
                var fill = BackColor.A == 0 ? Surface : BackColor;
                using (var path = Rounded(box, 9))
                using (var brush = new LinearGradientBrush(box, ControlPaint.Light(fill, hover ? .25f : .12f), fill, 90f))
                using (var border = new Pen(Color.FromArgb(hover ? 155 : 65, Dark ? Color.White : Accent)))
                { e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(border, path); }
                TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.Inflate(ClientRectangle,-3,-2), ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                if (Focused && ShowFocusCues) using (var path = Rounded(Rectangle.Inflate(box, -3, -3), 7)) using (var pen = new Pen(Accent)) e.Graphics.DrawPath(pen, path);
            }
        }
        public static void StyleMenu(ContextMenuStrip menu)
        {
            Localize.Apply(menu.Items);
            menu.Font = Font(); menu.BackColor = Surface; menu.ForeColor = Text;
            menu.ShowImageMargin = false; menu.Padding = new Padding(7);
            menu.Renderer = new CabinetMenuRenderer();
            foreach (ToolStripItem item in menu.Items) { item.ForeColor = Text; item.Padding = new Padding(10, 5, 10, 5); }
        }
        private sealed class CabinetMenuRenderer : ToolStripProfessionalRenderer
        {
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            { TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, new Rectangle(12, 0, e.Item.Width - 24, e.Item.Height), Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis); }
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { e.Graphics.Clear(Surface); }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { using (var pen = new Pen(Muted)) e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1); }
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (!e.Item.Selected) return;
                var box = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
                using (var path = Rounded(box, 7)) using (var brush = new SolidBrush(Dark ? Color.FromArgb(65, 65, 68) : Color.FromArgb(221, 234, 252))) e.Graphics.FillPath(brush, path);
            }
            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) { using (var pen = new Pen(Color.FromArgb(65, Muted))) e.Graphics.DrawLine(pen, 10, e.Item.Height / 2, e.Item.Width - 10, e.Item.Height / 2); }
        }
        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var p = new GraphicsPath(); int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
        }
        public static string Prompt(string title, string label, string value = "")
        {
            using (var f = new Form { Text = title, ClientSize = new Size(430, 156), MinimizeBox = false, MaximizeBox = false, FormBorderStyle = FormBorderStyle.FixedDialog })
            {
                Style(f); var l = new Label { Text = label, AutoSize = false, Bounds = new Rectangle(16, 12, 395, 40) };
                var t = new TextBox { Text = value, Bounds = new Rectangle(16, 55, 395, 28) };
                var ok = Button("确定", () => { f.DialogResult = DialogResult.OK; f.Close(); }); ok.SetBounds(326, 105, 85, 32);
                f.Controls.AddRange(new Control[] { l, t, ok }); f.AcceptButton = ok;
                return f.ShowDialog() == DialogResult.OK ? t.Text : null;
            }
        }
        public static void Error(Exception ex) { Program.Trace(ex.ToString()); MessageBox.Show(Localize.T(ex.Message), Localize.T("ShotCab · 操作未完成"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        public static bool Confirm(string text) => MessageBox.Show(Localize.T(text), Localize.T("ShotCab · 请确认"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK;
    }
}
