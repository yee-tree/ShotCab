using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShotCab.App
{
    // A single quiet navigation surface; each cell remains keyboard accessible.
    internal sealed class SettingsNavigation : Panel
    {
        public event Action<int> CategoryChanged;
        private int selected;
        internal int SelectedCategory { get => selected; set { selected=value; foreach(Control child in Controls) child.Invalidate(); } }
        internal static string Title(int index) => (Localize.English
            ? new[] { "Capture", "History", "Appearance", "General & hotkeys", "Storage", "OCR/More" }
            : new[] { "截图", "历史", "外观", "常规与快捷键", "存储", "OCR/其他" })[index];
        internal static string Description(int index) => (Localize.English
            ? new[] { "Choose what happens when you capture.", "Keep recent moments within reach.", "Sidebar layout and annotation styles.", "Your language, shortcuts and everyday habits.", "Your images, stored as ordinary files.", "Offline text recognition and app controls." }
            : new[] { "设定截图时与完成后的操作", "让近期截图随时可用", "侧栏布局与默认标注样式", "语言、快捷键与日常使用习惯", "独立图片文件，方便查找与管理", "离线识字与应用管理" })[index];
        internal SettingsNavigation()
        {
            Height=108; DoubleBuffered=true; BackColor=Color.Transparent;
            for(int i=0;i<6;i++) { var cell=new SectionCell(this,i); Controls.Add(cell); }
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            for(int i=0;i<Controls.Count;i++)
            {
                int column=i%3, row=i/3;
                int left=Width*column/3, right=Width*(column+1)/3;
                Controls[i].Bounds=new Rectangle(left,row*54,right-left,54);
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=Ui.Rounded(new Rectangle(0,0,Width-1,Height-1),16)) using(var brush=new SolidBrush(Ui.Surface)) e.Graphics.FillPath(brush,path);
        }
        private sealed class SectionCell : Control
        {
            private readonly SettingsNavigation owner; private readonly int index; private bool hover;
            internal SectionCell(SettingsNavigation owner,int index) { this.owner=owner;this.index=index; SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer,true); BackColor=Color.Transparent; TabStop=true; Cursor=Cursors.Hand; AccessibleRole=AccessibleRole.PageTab; AccessibleName=Title(index); }
            protected override void OnClick(EventArgs e) { owner.CategoryChanged?.Invoke(index); base.OnClick(e); }
            protected override void OnKeyDown(KeyEventArgs e) { if(e.KeyCode==Keys.Enter || e.KeyCode==Keys.Space) { OnClick(EventArgs.Empty); e.Handled=true; e.SuppressKeyPress=true; } base.OnKeyDown(e); }
            protected override void OnMouseEnter(EventArgs e) { hover=true;Invalidate();base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { hover=false;Invalidate();base.OnMouseLeave(e); }
            protected override void OnGotFocus(EventArgs e) { Invalidate();base.OnGotFocus(e); }
            protected override void OnLostFocus(EventArgs e) { Invalidate();base.OnLostFocus(e); }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
                bool active=owner.selected==index;
                var bounds=new Rectangle(1,1,Math.Max(0,Width-2),Math.Max(0,Height-2));
                if(active || hover || Focused) using(var path=Ui.Rounded(bounds,9)) using(var brush=new SolidBrush(active ? Ui.Accent : Ui.Dark ? Color.FromArgb(54,54,57) : Color.FromArgb(228,235,246))) e.Graphics.FillPath(brush,path);
                string title;
                if(owner.Width<200) title=Localize.English ? new[] { "Shot", "Hist", "Style", "Keys", "Files", "More" }[index] : new[] { "截图", "历史", "外观", "常规", "存储", "更多" }[index];
                else { title=Localize.English && index==2 ? "Style" : Title(index); if(index==3) title=Localize.English ? "General\nHotkeys" : "常规与快捷键"; }
                bool compact=owner.Width<200;
                using(var font=Ui.Font(compact ? 7.5f : 9f)) TextRenderer.DrawText(e.Graphics,title,font,new Rectangle(2,3,Width-4,Height-6),active ? Ui.Background : hover ? Ui.Text : Ui.Muted,TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | (compact ? TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding : TextFormatFlags.WordBreak));
            }
        }
    }
}
