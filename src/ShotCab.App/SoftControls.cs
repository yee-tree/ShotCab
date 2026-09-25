using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace ShotCab.App
{
    internal sealed class SoftEntry : Panel
    {
        internal TextBox Input { get; } = new TextBox { BorderStyle=BorderStyle.None };
        internal SoftEntry() { SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); BackColor=Color.Transparent; Size=new Size(180,38); Input.BackColor=Ui.Surface; Input.ForeColor=Ui.Text; Controls.Add(Input); }
        protected override void OnLayout(LayoutEventArgs e) { Input.SetBounds(12,Math.Max(4,(Height-Input.PreferredHeight)/2),Math.Max(20,Width-24),Input.PreferredHeight); base.OnLayout(e); }
        protected override void OnPaint(PaintEventArgs e) { e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; using(var path=Ui.Rounded(new Rectangle(1,1,Width-3,Height-3),10)) using(var brush=new SolidBrush(Ui.Surface)) using(var pen=new Pen(Color.FromArgb(80,Ui.Muted))) { e.Graphics.FillPath(brush,path); e.Graphics.DrawPath(pen,path); } base.OnPaint(e); }
    }
    internal sealed class SoftToggle : Control
    {
        private bool value;
        private readonly ToolTip helpTip = new ToolTip { InitialDelay = 300, AutoPopDelay = 12000 };
        private bool helpHovered;
        private string helpText;
        public string HelpText { get => helpText; set { helpText = value; AccessibleDescription = value; Invalidate(); } }
        private Rectangle HelpBounds => new Rectangle(Width - 21, MaximumSize.Width > 0 && MaximumSize.Width < 160 ? 2 : (Height - 18) / 2, 18, 18);
        public event EventHandler CheckedChanged;
        public bool Checked { get => value; set { if (this.value == value) return; this.value = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); } }
        public SoftToggle() { SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); BackColor = Color.Transparent; TabStop = true; Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.CheckButton; Margin = new Padding(3,7,3,7); }
        public override Size GetPreferredSize(Size proposedSize) { int width = MaximumSize.Width > 0 ? MaximumSize.Width : 280; int help = string.IsNullOrEmpty(HelpText) ? 0 : 24; if(width<160) { var compactText=TextRenderer.MeasureText(Text,Font,new Size(width-4-help,0),TextFormatFlags.WordBreak); return new Size(width,Math.Max(58,compactText.Height+29)); } var size = TextRenderer.MeasureText(Text, Font, new Size(Math.Max(80,width-49-help),0), TextFormatFlags.WordBreak); return new Size(Math.Min(width,size.Width+49+help), Math.Max(30,size.Height+6)); }
        protected override void OnClick(EventArgs e) { if (!string.IsNullOrEmpty(HelpText) && HelpBounds.Contains(PointToClient(Cursor.Position))) return; Checked = !Checked; base.OnClick(e); }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hover = !string.IsNullOrEmpty(HelpText) && HelpBounds.Contains(e.Location);
            if (hover != helpHovered) { helpHovered = hover; if (hover) helpTip.Show(HelpText, this, HelpBounds.Left, HelpBounds.Bottom + 3, 12000); else helpTip.Hide(this); Invalidate(HelpBounds); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { helpHovered = false; helpTip.Hide(this); Invalidate(HelpBounds); base.OnMouseLeave(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { Checked = !Checked; e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            bool compact=MaximumSize.Width>0 && MaximumSize.Width<160;
            var track = compact ? new Rectangle(Width-38,Height-24,36,22) : new Rectangle(1, (Height-22)/2, 36,22);
            var trackColor = Checked ? Color.FromArgb(38,96,187) : Ui.Dark ? Color.FromArgb(75,75,80) : Color.FromArgb(193,198,208);
            using (var path = Ui.Rounded(track,11)) using (var brush = new SolidBrush(trackColor)) e.Graphics.FillPath(brush,path);
            using (var thumb = new SolidBrush(Color.White))
                e.Graphics.FillEllipse(thumb, track.Left+(Checked ? 17 : 3),track.Top+3,16,16);
            int helpWidth = string.IsNullOrEmpty(HelpText) ? 0 : 24;
            var textRect=compact ? new Rectangle(0,0,Math.Max(1,Width-helpWidth),Math.Max(1,Height-29)) : new Rectangle(47,0,Math.Max(1,Width-47-helpWidth),Height);
            TextRenderer.DrawText(e.Graphics, Text,Font,textRect,Ui.Text,(compact ? TextFormatFlags.Top : TextFormatFlags.VerticalCenter) | TextFormatFlags.WordBreak);
            if (helpWidth > 0)
            {
                var help = HelpBounds;
                using (var pen = new Pen(helpHovered ? Ui.Text : Ui.Muted, 1.2f)) e.Graphics.DrawEllipse(pen, help);
                TextRenderer.DrawText(e.Graphics, "i", Font, help, helpHovered ? Ui.Text : Ui.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            if (Focused && ShowFocusCues) using (var p = Ui.Rounded(track,11)) using (var pen = new Pen(Ui.Text)) e.Graphics.DrawPath(pen,p);
        }
        protected override void Dispose(bool disposing) { if (disposing) helpTip.Dispose(); base.Dispose(disposing); }
    }
    internal sealed class SoftChoice : Control
    {
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        public ArrayList Items { get; } = new ArrayList();
        private int selected = -1;
        public event EventHandler SelectedIndexChanged;
        public int SelectedIndex { get => selected; set { if (value < -1 || value >= Items.Count) throw new ArgumentOutOfRangeException(); if (selected == value) return; selected = value; Invalidate(); SelectedIndexChanged?.Invoke(this, EventArgs.Empty); } }
        public SoftChoice() { SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); BackColor = Color.Transparent; Size = new Size(150,36); TabStop = true; Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.ComboBox; }
        protected override void OnClick(EventArgs e)
        {
            menu.Close(); while(menu.Items.Count>0) menu.Items[0].Dispose();
            for (int i=0;i<Items.Count;i++) { int index=i; var entry = new ToolStripMenuItem(Localize.T(Convert.ToString(Items[i]))) { Checked = i == selected }; entry.Click += (s,a) => SelectedIndex = index; menu.Items.Add(entry); }
            Ui.StyleMenu(menu);
            menu.Show(this,new Point(0,Height)); base.OnClick(e);
        }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { OnClick(EventArgs.Empty); e.Handled=true; } else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up) { SelectedIndex = Math.Max(0,Math.Min(Items.Count-1,selected+(e.KeyCode == Keys.Down ? 1:-1))); e.Handled=true; } base.OnKeyDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Ui.Rounded(new Rectangle(1,1,Width-3,Height-3),10)) using (var brush = new SolidBrush(Ui.Surface)) using (var pen = new Pen(Focused ? Ui.Accent : Color.FromArgb(90,Ui.Muted))) { e.Graphics.FillPath(brush,path); e.Graphics.DrawPath(pen,path); }
            if (selected>=0) TextRenderer.DrawText(e.Graphics,Localize.T(Convert.ToString(Items[selected])),Font,new Rectangle(10,0,Math.Max(1,Width-34),Height),Ui.Text,TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (var pen = new Pen(Ui.Muted,1.5f)) e.Graphics.DrawLines(pen,new[] { new Point(Width-20,Height/2-2),new Point(Width-15,Height/2+3),new Point(Width-10,Height/2-2) });
        }
        protected override void Dispose(bool disposing) { if(disposing) menu.Dispose(); base.Dispose(disposing); }
    }
}
