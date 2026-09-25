using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
namespace ShotCab.App
{
    internal static class Localize
    {
        internal static bool English { get; private set; }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control,object> watched=new System.Runtime.CompilerServices.ConditionalWeakTable<Control,object>();
        internal static void SetLanguage(string language) { English=language=="en"; Thread.CurrentThread.CurrentUICulture=CultureInfo.GetCultureInfo(English ? "en" : "zh-CN"); }
        private static readonly Dictionary<string,string> words = new Dictionary<string,string>
        {
            ["近期"]="Recent",["历史"]="History",["今天"]="Today",["昨天"]="Yesterday",["还没有近期截图"]="No recent captures",["暂无历史截图"]="No history shown",["按日期浏览截图\n隐藏项见完整历史"]="Browse captures by date\nHidden items: full history",["按 {0} 截图，完成后自动加入近期"]="Press {0} to capture; it appears in Recent",["开始截图"]="Capture now",["打开完整历史"]="Open full history",["松开导入图片"]="Drop to import images",["也可拖入图片"]="Or drag images here",["设置"]="Settings",["截图"]="Capture",["外观"]="Style",["存储"]="Storage",["更多"]="More",["常规与快捷键"]="General & keys",["常规与\n快捷键"]="General\nHotkeys",["深色主题"]="Dark theme",["其他应用全屏时"]="When another app is full screen",["保持显示"]="Keep visible",["隐藏侧栏"]="Hide sidebar",["隐藏侧栏与贴图"]="Hide sidebar and pins",
            ["基本设置"]="Preferences",["保存截图历史"]="Keep screenshot history",["截图时自动隐藏面板"]="Hide sidebar during capture",["截图后直接编辑"]="Edit after capture",["截图后显示结果条"]="Show capture action bar",["显示放大镜与坐标"]="Show magnifier and coordinates",
            ["默认导出格式"]="Default export format",["保留天数"]="Retention (days)",["也可拖动侧栏内侧边缘调整"]="Or drag the inner edge to resize",["近期显示张数"]="Recent image count",["侧栏宽度"]="Sidebar width",
            ["停靠位置"]="Dock side",["靠左"]="Left",["靠右"]="Right",["拖出预览卡片按键"]="Drag-out card modifier",["不自动清理"]="Keep forever",["自定义"]="Custom",
            ["图片以独立文件保存，数据库只存索引。"]="Images are separate files. The database stores the index.",["打开图片文件夹"]="Open image folder",["详细设置…"]="Advanced settings…",["退出图柜"]="Quit ShotCab",["打开完整历史  ↗"]="Open full history  ↗",
            ["截图快捷键"]="Capture shortcut",["贴图快捷键"]="Pin shortcut",["侧栏快捷键"]="Sidebar shortcut",["OCR 快捷键"]="OCR shortcut",["按下快捷键组合"]="Press a key combination",["语言 / Language"]="Language",["开机启动"]="Start with Windows",
            ["复制图片"]="Copy image",["剪切导出文件（保留历史）"]="Cut exported file (keep history)",["修改"]="Edit",["拼接另一张图片"]="Combine with another image",["贴图"]="Pin image",["OCR 选字"]="Select text / OCR",["识别二维码"]="Read QR code",["导出"]="Export",["打开图片所在文件夹"]="Show image in folder",
            ["截图已完成"]="Capture complete",["已复制到剪贴板 · 已加入近期"]="Copied to clipboard · Added to Recent",["已复制；历史未保存"]="Copied · History not saved",["关闭结果条"]="Close action bar",["编辑图片"]="Edit image",["打开文件"]="Show file",
            ["取消收藏"]="Unfavorite",["★ 收藏（免于到期清理）"]="★ Favorite (keep indefinitely)",["恢复侧栏显示"]="Show in sidebar",["隐藏"]="Hide",["设置标签"]="Set tags",["删除到回收站"]="Move to recycle bin",
            ["原图"]="Original",["已编辑"]="Edited",["已打码"]="Redacted",["遮挡已固化"]="Redaction baked",["ShotCab · 图柜"]="ShotCab · Screenshot Cabinet",["ShotCab · 设置"]="ShotCab · Settings",["ShotCab · 操作未完成"]="ShotCab · Action incomplete",["ShotCab · 请确认"]="ShotCab · Confirm",
            ["全屏截图"]="Capture all screens",["当前窗口"]="Capture active window",["指定显示器"]="Choose display",["自由形状截图"]="Freehand capture",["延时 3 秒截图"]="Capture after 3 seconds",["上次区域"]="Last region",["重截上次区域"]="Recapture last region",["按上次的位置截取当前屏幕；仅在本次运行中保留。"]="Capture the current screen at the previous position; kept only for this session.",["滚动长截图"]="Scrolling capture",["取色器"]="Color picker",["导入图片"]="Import images",["导入剪贴板图片"]="Import clipboard image",["图片拼接"]="Combine images",["选择第一张图片"]="Choose the first image",["选择第二张图片"]="Choose the second image",["选择要拼接的另一张图片"]="Choose another image to combine",["选择拼接方向：是 = 横向，否 = 纵向。"]="Combine side by side? Choose No to stack vertically.",["从剪贴板贴图"]="Pin clipboard image",["识别截图文字"]="Capture text / OCR",["完整历史 / 存储管理"]="History / storage",["显示 / 隐藏侧栏"]="Toggle sidebar",["隐藏 / 恢复所有浮窗"]="Toggle floating windows",["关于 ShotCab"]="About ShotCab",["退出"]="Quit",
            ["ShotCab · 预览卡片"]="ShotCab · Preview card",["预览卡片"]="Preview card",["ShotCab · 贴图"]="ShotCab · Pinned image",["选字 / OCR"]="Select text / OCR",["标注 / 修改"]="Annotate / edit",["鼠标穿透（托盘可恢复全部）"]="Click-through (restore from tray)",["100% 大小"]="Actual size",["关闭贴图"]="Close image",
            ["完成并复制"]="Done & copy",["另存新截图"]="Save as new capture",["识别文字"]="Recognize text",["永久应用遮挡"]="Bake redactions",["遮挡/聚光样式"]="Redaction / spotlight style",["鼠标放大预览：开/关"]="Toggle cursor magnifier",
            ["确定"]="OK",["取消"]="Cancel",["保存设置"]="Save settings",["常规与快捷键"]="General & keys",["截图与导出"]="Capture & export",["标注默认样式"]="Annotation defaults",["OCR 与预览"]="OCR & preview",["历史与存储"]="History & storage",["侧栏与贴图"]="Sidebar & pins",
            ["截图时间"]="Captured",["状态"]="Status",["标签"]="Tags",["尺寸"]="Size",["占用"]="Storage",["收藏 / 隐藏"]="Favorite / hidden",["上一页"]="Previous",["下一页"]="Next",["选中本页"]="Select page",["收藏"]="Favorite",["恢复"]="Restore",["删除"]="Delete",["搜索"]="Search",["刷新"]="Refresh",["全部"]="All",["回收站"]="Recycle bin",["已隐藏"]="Hidden",
            ["复制文字"]="Copy text",["全选"]="Select all",["适应窗口"]="Fit to window",["复制所选文字"]="Copy selected text",["复制全部文字"]="Copy all text",["选择离线 OCR 组件"]="Choose offline OCR component",
            ["ShotCab · 完整历史与存储管理"]="ShotCab · History and storage",["全部历史"]="All history",["隐藏项目"]="Hidden",["全部状态"]="All states",["已识别文字"]="OCR available",["最新优先"]="Newest first",["最旧优先"]="Oldest first",["占用最大优先"]="Largest first",["匹配全部标签"]="Match all tags",["搜索文字"]="Search text",["筛选"]="Filter",["限制最大占用"]="Maximum size",["标签（逗号分隔）"]="Tags (comma separated)",["至"]="to",["至少 MB"]="Minimum MB",["MB（内部图片与标注合计）"]="MB (images and annotations)",["追加标签"]="Add tags",["移除标签"]="Remove tags",["管理标签"]="Manage tags",["取消隐藏"]="Unhide",["批量导出"]="Export selected",["立即释放空间"]="Permanently delete",["打开保存位置"]="Open storage folder",["清理最旧的未收藏截图…"]="Clean oldest nonfavorites…",["清空回收站…"]="Empty recycle bin…",["迁移数据目录…"]="Move data folder…",
            ["筛选记录"]="Filter records",["图片预览"]="Image preview",["选择一张截图以预览\n双击列表可编辑"]="Select a capture to preview\nDouble-click a row to edit",["存储管理"]="Storage",["收藏…"]="Favorite…",["标签…"]="Tags…",["显示…"]="Visibility…",["日期范围"]="Date range",["从"]="From",["收藏/隐藏"]="Flags",
            ["ShotCab · 图片选字与文字核对"]="ShotCab · Select and review text",["复制文字窗"]="Copy text pane",["全选图片文字"]="Select all image text",["取消识别"]="Cancel recognition",["正在加载离线识别组件…"]="Loading offline recognition…",["未识别到文字。可尝试更清晰、缩放更大的截图。"]="No text found. Try a clearer or larger image.",["在左侧图片拖选文字；右侧可修改后复制。识别进程已释放。"]="Select text in the image or edit it on the right. The OCR worker has exited.",["识别已取消，未修改历史文字。"]="Recognition cancelled; saved text is unchanged.",["ShotCab · 文字"]="ShotCab · Text",["复制选中 / 全部文字"]="Copy selected / all text",["已复制文字"]="Text copied"
        };
        internal static string T(string text) { if (text == null) return null; if (English) return words.TryGetValue(text,out var translated) ? translated : text; return words.FirstOrDefault(x => x.Value==text).Key ?? text; }
        internal static void Apply(Control control)
        {
            if (!(control is TextBoxBase) && !(control is NumericUpDown)) control.Text=T(control.Text);
            if(!(control is TextBoxBase) && !(control is NumericUpDown) && !watched.TryGetValue(control,out var marker)) { watched.Add(control,new object()); control.TextChanged += (s,e) => { string value=T(control.Text); if(value!=control.Text) control.Text=value; }; }
            if(control.AccessibleRole==AccessibleRole.PushButton && English && control.Text.Length>6) control.Font=Ui.Font(8.5f);
            if (control is ToolStrip strip) Apply(strip.Items);
            if (control is ListView list) foreach (ColumnHeader column in list.Columns) column.Text=T(column.Text);
            if (control is ComboBox combo) { int index=combo.SelectedIndex; for(int i=0;i<combo.Items.Count;i++) if(combo.Items[i] is string value) combo.Items[i]=T(value); combo.SelectedIndex=index; }
            foreach (Control child in control.Controls) Apply(child);
        }
        internal static void Apply(ToolStripItemCollection items) { foreach (ToolStripItem item in items) { item.Text=T(item.Text); if (item is ToolStripDropDownItem drop) Apply(drop.DropDownItems); } }
    }
}
