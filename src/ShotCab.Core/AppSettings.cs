using System.ComponentModel;

namespace ShotCab.Core
{
    public enum SidebarDockSide { 靠右, 靠左 }
    public enum CardDragModifier { Alt, Ctrl, Shift }
    public sealed class AppSettings
    {
        public const int SidebarMinimumWidth = 180;
        public const int SidebarMaximumWidth = 640;
        [Category("常规"), DisplayName("语言 / Language")]
        public string Language { get; set; } = "zh-CN";
        [Category("常规"), DisplayName("深色主题"), Description("关闭后使用浅色主题。")]
        public bool DarkTheme { get; set; } = true;
        [Category("历史记录"), DisplayName("启用历史记录"), Description("保存截图和可编辑文档到历史记录。")]
        public bool HistoryEnabled { get; set; } = true;

        [Category("历史记录"), DisplayName("保留天数"), Description("普通历史记录保留天数，0 表示永久保留。")]
        public int RetentionDays { get; set; } = 7;

        [Category("历史记录"), DisplayName("最近项目数量"), Description("最近项目列表显示的最大数量。")]
        public int RecentCount { get; set; } = 20;

        [Category("历史记录"), DisplayName("回收站保留天数"), Description("回收站中的项目自动清理前保留的天数。")]
        public int RecycleDays { get; set; } = 3;

        [Category("历史记录"), DisplayName("容量警告 (GB)"), Description("历史记录达到此容量时显示警告。")]
        public double CapacityWarningGB { get; set; } = 2d;

        [Category("侧边栏"), DisplayName("启用侧边栏"), Description("显示历史记录侧边栏。")]
        public bool SidebarEnabled { get; set; } = true;
        [Category("侧边栏"), DisplayName("截图时自动隐藏面板")]
        public bool HideSidebarDuringCapture { get; set; } = true;
        [Category("输出"), DisplayName("截图后直接编辑")]
        public bool EditAfterCapture { get; set; }
        [Category("输出"), DisplayName("截图后显示结果条"), Description("截图已经复制并加入近期；结果条只提供后续操作，几秒后自动隐藏。")]
        public bool ShowCaptureResult { get; set; } = true;
        [Category("输出"), DisplayName("截图/编辑鼠标放大与坐标预览")]
        public bool EditorCursorPreview { get; set; } = false;

        [Category("侧边栏"), DisplayName("宽度"), Description("侧边栏宽度（逻辑像素），随显示缩放调整。")]
        public int SidebarWidth { get; set; } = 320;

        [Category("侧边栏"), DisplayName("停靠位置"), Description("选择屏幕左侧或右侧；拖动朝向桌面的边缘可调整宽度。")]
        public SidebarDockSide SidebarSide { get; set; } = SidebarDockSide.靠右;
        [Category("侧边栏"), DisplayName("拖出预览卡片按键")]
        public CardDragModifier PreviewDragKey { get; set; } = CardDragModifier.Alt;

        [Browsable(false)]
        public double SidebarOpacity { get; set; } = .95d;

        [Category("侧边栏"), DisplayName("预留屏幕空间"), Description("为侧边栏预留桌面工作区。")]
        public bool SidebarReserveSpace { get; set; }

        [Category("侧边栏"), DisplayName("显示器"), Description("侧边栏所在显示器的索引。")]
        public int SidebarScreen { get; set; }

        [Category("侧边栏"), DisplayName("自动折叠"), Description("不使用时自动折叠侧边栏。")]
        public bool SidebarAutoFold { get; set; }

        [Category("常规"), DisplayName("其他应用全屏时"), Description("0：保持显示；1：隐藏侧栏；2：隐藏侧栏与贴图。退出全屏后恢复。")]
        public int FullscreenMode { get; set; }

        [Category("输出"), DisplayName("导出格式"), Description("默认图像导出格式。")]
        public string ExportFormat { get; set; } = "png";

        [Category("输出"), DisplayName("JPEG 质量"), Description("JPEG 导出质量，范围 1 到 100。")]
        public int JpegQuality { get; set; } = 90;

        [Category("输出"), DisplayName("编辑为新项目"), Description("编辑历史项目时默认创建新项目。")]
        public bool EditAsNew { get; set; }

        [Category("OCR"), DisplayName("OCR 程序路径"), Description("外部 OCR 程序的完整路径；留空使用应用默认配置。")]
        public string OcrExecutablePath { get; set; } = string.Empty;

        [Category("OCR"), DisplayName("进入编辑器时自动识别文字"), Description("打开图片编辑器后自动运行本地 OCR；关闭后仍可点击编辑器中的识别按钮。")]
        public bool AutoOcrOnEdit { get; set; }

        [Category("OCR"), DisplayName("预览秒数"), Description("OCR 结果预览持续时间（秒）。")]
        public int OcrPreviewSeconds { get; set; } = 3;

        [Category("快捷键"), DisplayName("截图"), Description("开始截图的全局快捷键。")]
        public string CaptureShortcut { get; set; } = "F6";

        [Category("快捷键"), DisplayName("贴图"), Description("创建贴图的全局快捷键。")]
        public string PinShortcut { get; set; } = "F7";

        [Category("快捷键"), DisplayName("侧边栏"), Description("显示或隐藏侧边栏的全局快捷键。")]
        public string SidebarShortcut { get; set; } = "F8";

        [Category("快捷键"), DisplayName("OCR"), Description("执行 OCR 的全局快捷键。")]
        public string OcrShortcut { get; set; } = "F9";

        [Category("常规"), DisplayName("开机启动"), Description("登录 Windows 时启动 ShotCab。")]
        public bool StartWithWindows { get; set; }
    }
}
