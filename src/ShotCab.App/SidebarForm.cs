using ShotCab.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ShotCab.App
{
    internal sealed partial class SidebarForm : Form
    {
        private readonly CabinetContext app;
        private readonly PhotoStrip strip;
        private readonly Label footer;
        private readonly FlowLayoutPanel basicSettings;
        private bool fittingBasicSettings;
        private bool settingsVisible;
        private int settingsCategory;
        internal int? ActiveSettingsCategory => settingsVisible ? settingsCategory : (int?)null;
        private readonly Control recentTab, historyTab, settingsTab;
        private readonly Panel resizeGrip;
        private bool resizing;
        private int resizeStartX, resizeStartWidth;
        private bool? compactHeader;
        private readonly System.Diagnostics.Stopwatch resizeClock = new System.Diagnostics.Stopwatch();
        private long lastResizeFrame;
        private readonly Timer timer = new Timer { Interval = 700 };
        private bool history, reserved, repositioning, fullscreenHidden, collapsed;
        private uint callback;
        protected override bool ShowWithoutActivation => !settingsVisible;
        protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x80; if (!settingsVisible) p.ExStyle |= 0x08000000; return p; } }
        public SidebarForm(CabinetContext app)
        {
            this.app = app; Ui.Style(this); Text = "ShotCab · 图柜";
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            var header = new Panel { Dock = DockStyle.Top, Height = 110, Padding = new Padding(14, 12, 14, 8), BackColor = Color.Transparent };
            var titleRow = new Panel { Dock = DockStyle.Top, Height = 37, BackColor = Color.Transparent };
            var brand = new Label { Text = "ShotCab", Font = Ui.Font(16, FontStyle.Bold), ForeColor = Ui.Text, Dock = DockStyle.Fill };
            titleRow.Controls.Add(brand);
            var close = Ui.GlassButton("×", app.ToggleSidebar); close.Name = "CloseSidebar"; close.AutoSize = false; close.MinimumSize = Size.Empty; close.Width = 36; close.Dock = DockStyle.Right;
            titleRow.Controls.Add(close); header.Controls.Add(titleRow);
            var nav = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 44, ColumnCount = 3, RowCount = 1 };
            recentTab = Ui.GlassButton("近期", () => { history = false; ShowBasicSettings(false); RefreshItems(); });
            historyTab = Ui.GlassButton("历史", () => { history = true; ShowBasicSettings(false); RefreshItems(); });
            settingsTab = Ui.GlassButton("设置", () => ShowBasicSettings(true));
            foreach (var tab in new[] { recentTab, historyTab, settingsTab })
            {
                nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
                tab.Font=Ui.Font(9);
                tab.AutoSize = false; tab.MinimumSize = Size.Empty; tab.Dock = DockStyle.Fill; nav.Controls.Add(tab);
            }
            header.Controls.Add(nav); UpdateNavigation();
            SizeChanged += (s,e) =>
            {
                bool compact = Width < 230;
                if (compactHeader == compact) return;
                compactHeader = compact;
                brand.Text = compact ? "图柜" : "ShotCab";
                brand.Font = Ui.Font(compact ? 15 : 16, FontStyle.Bold);
                foreach (var tab in new[] { recentTab, historyTab, settingsTab }) { tab.Font=Ui.Font(compact ? 8 : 9); tab.Margin=compact ? new Padding(1) : new Padding(4); }
            };
            footer = new Label { Dock = DockStyle.Bottom, Height = 35, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Ui.Muted, Cursor = Cursors.Hand };
            strip = new PhotoStrip(app) { Dock = DockStyle.Fill };
            footer.Click += (s, e) =>
            {
                if (strip.SelectedCount > 1) app.BatchItemMenu(strip.SelectedItems).Show(footer, new Point(0, 0));
                else app.ShowHistory();
            };
            strip.SelectionChanged += UpdateFooter;
            foreach (Control tab in new[] { recentTab, historyTab })
            {
                tab.AllowDrop = true;
                tab.DragEnter += (s,e) => e.Effect = (e.AllowedEffect & DragDropEffects.Copy) != 0 && strip.CanImport(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
                tab.DragOver += (s,e) => e.Effect = (e.AllowedEffect & DragDropEffects.Copy) != 0 && strip.CanImport(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
                tab.DragDrop += (s,e) =>
                {
                    if (!strip.CanImport(e.Data)) return;
                    if (ReferenceEquals(s, recentTab)) SelectRecentTab(); else SelectHistoryTab();
                    strip.ImportData(e.Data);
                };
            }
            basicSettings = new BufferedSettingsPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(12), Visible = false };
            basicSettings.SizeChanged += (s,e) => FitBasicSettings();
            Controls.Add(strip); Controls.Add(basicSettings); Controls.Add(footer); Controls.Add(header);
            header.BackColor = titleRow.BackColor = nav.BackColor = footer.BackColor = basicSettings.BackColor = Color.Transparent;
            basicSettings.BackColor = Ui.Background;
            resizeGrip = new Panel { Dock = DockStyle.Left, Width = 7, Cursor = Cursors.SizeWE, BackColor = Ui.Surface, AccessibleName = "拖动调整侧栏宽度" };
            Controls.Add(resizeGrip);
            resizeGrip.MouseDown += (s,e) => { if (e.Button != MouseButtons.Left) return; resizing = true; resizeStartX = Cursor.Position.X; resizeStartWidth = Width; lastResizeFrame = -16; resizeClock.Restart(); resizeGrip.Capture = true; };
            resizeGrip.MouseMove += (s,e) => { if (resizing && resizeClock.ElapsedMilliseconds-lastResizeFrame >= 16) { lastResizeFrame=resizeClock.ElapsedMilliseconds; ResizeFromDrag(Cursor.Position.X); } };
            resizeGrip.MouseUp += (s,e) => FinishResize();
            resizeGrip.MouseCaptureChanged += (s,e) => { if (resizing && !resizeGrip.Capture) FinishResize(); };
            timer.Tick += (s, e) => CheckState(); timer.Start();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
        }
        internal void ShowBasicSettings(bool show)
        {
            bool changed = settingsVisible != show;
            settingsVisible = show;
            if (changed && IsHandleCreated)
            {
                int style = Native.GetWindowLong(Handle, -20);
                Native.SetWindowLong(Handle, -20, show ? style & ~0x08000000 : style | 0x08000000);
            }
            if (changed) UpdateStyles(); UpdateNavigation();
            basicSettings.Visible = false;
            if (!show) { strip.Visible = true; if (!app.Settings.SidebarEnabled || app.SidebarManuallyHidden || app.ManuallyHidden) Hide(); return; }
            // Build off screen as one layout operation, rather than scrolling/repainting after each Add.
            basicSettings.SuspendLayout();
            try
            {
            basicSettings.AutoScrollPosition = Point.Empty;
            foreach (Control control in basicSettings.Controls.Cast<Control>().ToArray()) control.Dispose();
            basicSettings.Controls.Clear();
            basicSettings.Controls.Add(new Label { Text = "基本设置", AutoSize = true, Font = Ui.Font(13, FontStyle.Bold), Margin = new Padding(3, 8, 3, 14) });
            var enabled = new SoftToggle { Text = "保存截图历史", AutoSize = true, Checked = app.Settings.HistoryEnabled,
                HelpText = Localize.English ? "Off: new captures are not saved to history. Existing items remain subject to retention." : "关闭后，新截图不会写入历史；已有记录仍按保留规则清理。" };
            enabled.CheckedChanged += (s,e) => { app.Settings.HistoryEnabled = enabled.Checked; app.Safe(app.SaveBasicPreferences); }; basicSettings.Controls.Add(enabled);
            var hideCapture = new SoftToggle { Text = "截图时自动隐藏面板", Name = "HideSidebarDuringCapture", AutoSize = true, Checked = app.Settings.HideSidebarDuringCapture };
            hideCapture.CheckedChanged += (s,e) => { app.Settings.HideSidebarDuringCapture = hideCapture.Checked; app.Safe(app.SaveBasicPreferences); }; basicSettings.Controls.Add(hideCapture);
            var editCapture = new SoftToggle { Text = "截图后直接编辑", AutoSize = true, Checked = app.Settings.EditAfterCapture };
            editCapture.CheckedChanged += (s,e) => { app.Settings.EditAfterCapture = editCapture.Checked; app.Safe(app.SaveBasicPreferences); }; basicSettings.Controls.Add(editCapture);
            var resultBar = new SoftToggle { Text = "截图后显示结果条", Name = "CaptureSetting", AutoSize = true, Checked = app.Settings.ShowCaptureResult,
                HelpText = Localize.English ? "The capture is already copied and saved. This bar only offers follow-up actions such as edit and pin." : "截图已复制并加入近期；结果条只提供编辑、贴图等后续操作。" };
            resultBar.CheckedChanged += (s,e) => { app.Settings.ShowCaptureResult = resultBar.Checked; app.Safe(app.SaveBasicPreferences); }; basicSettings.Controls.Add(resultBar);
            var cursorPreview = new SoftToggle { Text = "显示放大镜与坐标", AutoSize = true, Checked = app.Settings.EditorCursorPreview };
            cursorPreview.CheckedChanged += (s,e) => { app.Settings.EditorCursorPreview = cursorPreview.Checked; app.Safe(app.SaveBasicPreferences); }; basicSettings.Controls.Add(cursorPreview);
            AddNumber("保留天数", app.Settings.RetentionDays, 0, 36500, n => app.Settings.RetentionDays = n);
            AddNumber("近期显示张数", app.Settings.RecentCount, 1, 1000, n => app.Settings.RecentCount = n);
            AddNumber("侧栏宽度", Math.Max(AppSettings.SidebarMinimumWidth, app.Settings.SidebarWidth), AppSettings.SidebarMinimumWidth, AppSettings.SidebarMaximumWidth, n => app.Settings.SidebarWidth = n, true);
            basicSettings.Controls.Add(new Label { Name="AppearanceSetting", Text="也可拖动侧栏内侧边缘调整", ForeColor=Ui.Muted, AutoSize=true, Margin=new Padding(3,0,3,10) });
            basicSettings.Controls.Add(new Label { Name="AppearanceSetting", Text="停靠位置", AutoSize=true, Margin=new Padding(3,14,3,4) });
            var side = new SoftChoice { Name="AppearanceSetting", Width=112 };
            side.Items.AddRange(new object[] { "靠右", "靠左" }); side.SelectedIndex=(int)app.Settings.SidebarSide;
            side.SelectedIndexChanged += (s,e) => { app.Settings.SidebarSide=(SidebarDockSide)side.SelectedIndex; app.Safe(app.ApplySettings); };
            basicSettings.Controls.Add(side);
            basicSettings.Controls.Add(new Label { Text = "拖出预览卡片按键", AutoSize = true, Margin = new Padding(3, 12, 3, 4) });
            var cardKey = new SoftChoice { Width = 160 };
            cardKey.Items.AddRange(new object[] { "Alt", "Ctrl", "Shift" }); cardKey.SelectedIndex = (int)app.Settings.PreviewDragKey;
            cardKey.SelectedIndexChanged += (s,e) => { app.Settings.PreviewDragKey = (CardDragModifier)cardKey.SelectedIndex; app.Safe(app.SaveBasicPreferences); }; basicSettings.Controls.Add(cardKey);
            basicSettings.Controls.Add(new Label { Name="GeneralSetting", Text="语言 / Language", AutoSize=true, Margin=new Padding(3,14,3,4) });
            var language=new SoftChoice { Name="GeneralSetting",Width=190 }; language.Items.AddRange(new object[] { "简体中文", "English" }); language.SelectedIndex=app.Settings.Language=="en" ? 1 : 0;
            language.SelectedIndexChanged += (s,e) => { app.ChangeLanguage(language.SelectedIndex==1 ? "en" : "zh-CN"); BeginInvoke((Action)(() => ShowBasicSettings(true))); }; basicSettings.Controls.Add(language);
            AddShortcut("截图快捷键","CaptureShortcut"); AddShortcut("贴图快捷键","PinShortcut"); AddShortcut("侧栏快捷键","SidebarShortcut"); AddShortcut("OCR 快捷键","OcrShortcut");
            var startup=new SoftToggle { Name="GeneralSetting",Text="开机启动",AutoSize=true,Checked=app.Settings.StartWithWindows };
            startup.CheckedChanged += (s,e) => { app.Settings.StartWithWindows=startup.Checked; app.Safe(app.ApplySettings); }; basicSettings.Controls.Add(startup);
            var dark=new SoftToggle { Name="AppearanceSetting",Text="深色主题",AutoSize=true,Checked=app.Settings.DarkTheme };
            dark.CheckedChanged += (s,e) => { app.Settings.DarkTheme=dark.Checked; app.Safe(app.ApplySettings); }; basicSettings.Controls.Add(dark);
            basicSettings.Controls.Add(new Label { Name="GeneralSetting",Text="其他应用全屏时",AutoSize=true,Margin=new Padding(3,12,3,4) });
            var fullscreen=new SoftChoice { Name="GeneralSetting",Width=220 }; fullscreen.Items.AddRange(new object[] { "保持显示", "隐藏侧栏", "隐藏侧栏与贴图" }); fullscreen.SelectedIndex=app.Settings.FullscreenMode;
            fullscreen.SelectedIndexChanged += (s,e) => { app.Settings.FullscreenMode=fullscreen.SelectedIndex; app.Safe(app.SaveBasicPreferences); }; basicSettings.Controls.Add(fullscreen);
            basicSettings.Controls.Add(new Label { Name="CaptureSetting",Text="默认导出格式",AutoSize=true,Margin=new Padding(3,14,3,4) });
            var exportFormat=new SoftChoice { Name="CaptureSetting",Width=140 };
            exportFormat.Items.AddRange(new object[] { "PNG", "JPG" }); exportFormat.SelectedIndex=app.Settings.ExportFormat=="jpg" ? 1 : 0;
            exportFormat.SelectedIndexChanged += (s,e) => { app.Settings.ExportFormat=exportFormat.SelectedIndex==1 ? "jpg" : "png"; app.Safe(app.SaveBasicPreferences); };
            basicSettings.Controls.Add(exportFormat);
            basicSettings.Controls.Add(new Label { Text = "图片以独立文件保存，数据库只存索引。", AutoSize = true, MaximumSize = new Size(260, 0), Margin = new Padding(3, 18, 3, 6) });
            basicSettings.Controls.Add(Ui.GlassButton("打开图片文件夹", () => app.Safe(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.IO.Path.Combine(app.Store.RootPath, "items")) { UseShellExecute = true }))));
            AddMigratedSettings();
            basicSettings.Controls.Add(Ui.GlassButton("退出图柜", app.ExitThread));
            Ui.ThemeInputs(basicSettings);
            BuildSettingsCategories();
            Localize.Apply(basicSettings);
            FitBasicSettings();
            }
            finally { basicSettings.ResumeLayout(true); }
            strip.Visible = false; basicSettings.Visible = true; basicSettings.AutoScrollPosition = Point.Empty;
            basicSettings.Invalidate(true);
            if (changed && Visible) Activate();
        }
        private void AddShortcut(string label, string property)
        {
            basicSettings.Controls.Add(new Label { Name="GeneralSetting",Text=label,AutoSize=true,Margin=new Padding(3,12,3,4) });
            var field=new SoftEntry { Name="GeneralSetting",Width=190 };
            var input=field.Input; input.Text=(string)typeof(AppSettings).GetProperty(property).GetValue(app.Settings); input.ReadOnly=true; input.AccessibleName=label;
            input.KeyDown += (s,e) =>
            {
                e.SuppressKeyPress=true;
                if(e.KeyCode==Keys.ControlKey || e.KeyCode==Keys.ShiftKey || e.KeyCode==Keys.Menu) return;
                string keys=new KeysConverter().ConvertToString(e.KeyData);
                if(app.ChangeShortcut(property,keys)) { input.Text=keys; input.BackColor=Ui.Surface; }
                else { input.BackColor=Ui.Dark ? Color.FromArgb(94,46,46) : Color.MistyRose; app.Notify(Localize.English ? "Shortcut unavailable. The previous binding is unchanged." : "快捷键重复或已被占用，原快捷键保持不变。"); }
            }; basicSettings.Controls.Add(field);
        }
        private void FitBasicSettings()
        {
            if (fittingBasicSettings || basicSettings.IsDisposed) return;
            fittingBasicSettings = true;
            basicSettings.SuspendLayout();
            try
            {
            int width = Math.Max(120, basicSettings.ClientSize.Width - basicSettings.Padding.Horizontal - (basicSettings.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0) - 2);
            foreach (Control control in basicSettings.Controls)
            {
                if (control is NumericUpDown) control.Width = width;
                if (control is TableLayoutPanel || control is SettingsNavigation || control is FlowLayoutPanel) control.Width = width;
                if (control.Name == "PresetValue" && control is FlowLayoutPanel presetRow)
                {
                    var custom = (TextBox)presetRow.Controls[1];
                    presetRow.Height = custom.Visible && width < 200 ? 78 : 44;
                    presetRow.PerformLayout();
                }
                if (control.Name.StartsWith("Setting_Number_", StringComparison.Ordinal) && control is FlowLayoutPanel numberRow)
                    numberRow.Height = numberRow.Controls[1].Visible && width < 210 ? 82 : 44;
                if (control is SoftChoice || control is SoftEntry) control.Width = Math.Min(control.Width, width);
                else if (control is Label || control is SoftToggle) control.MaximumSize = new Size(width, 0);
                if (control.Name == "Setting_AnnotationDefaults" && control is FlowLayoutPanel annotations)
                    FitAnnotationSettings(annotations, width);
                var background = control is NumericUpDown || control is TextBoxBase ? Ui.Surface : Color.Transparent;
                if (control.BackColor != background) control.BackColor = background;
            }
            }
            finally { basicSettings.ResumeLayout(true); fittingBasicSettings = false; }
        }
        private void BuildSettingsCategories()
        {
            foreach (Control control in basicSettings.Controls)
            {
                if (control.Name.StartsWith("Setting_", StringComparison.Ordinal)) continue;
                string text = control.Text;
                int category = 0;
                if (text == "基本设置") category = -2;
                else if (text.Contains("保存截图历史") || text.Contains("保留天数") || text.Contains("近期显示")) category = 1;
                else if (text.Contains("侧栏宽度") || (control is FlowLayoutPanel && control.Name != "PresetValue")) category = 2;
                else if (text.Contains("拖出预览") || control is SoftChoice) category = 3;
                else if (text.Contains("图片以独立") || text.Contains("打开图片文件夹")) category = 4;
                else if (text.Contains("详细设置") || text.Contains("退出图柜")) category = 5;
                if (control.Name == "PresetValue") category = (int)control.Tag;
                if (control.Name == "GeneralSetting") category=3;
                if (control.Name == "AppearanceSetting") category=2;
                if (control.Name == "CaptureSetting") category=0;
                control.Tag = category;
            }
            var categories = new SettingsNavigation { Width=240, Tag=-1, Margin=new Padding(0,0,0,12) };
            categories.CategoryChanged += index => SelectBasicCategory(index);
            basicSettings.Controls.Add(categories); basicSettings.Controls.SetChildIndex(categories, 0);
            var heading = new Label { Name="CategoryHeading",Tag=-1,UseMnemonic=false,AutoSize=true,Font=Ui.Font(12,FontStyle.Bold),Margin=new Padding(3,8,3,4) };
            var description = new Label { Name="CategoryDescription",Tag=-1,AutoSize=true,ForeColor=Ui.Muted,Font=Ui.Font(9),Margin=new Padding(3,0,3,16) };
            basicSettings.Controls.Add(heading); basicSettings.Controls.SetChildIndex(heading,1);
            basicSettings.Controls.Add(description); basicSettings.Controls.SetChildIndex(description,2);
            SelectBasicCategory(settingsCategory);
        }
        internal void SelectBasicCategory(int category)
        {
            settingsCategory = category; basicSettings.SuspendLayout();
            foreach (Control control in basicSettings.Controls)
            {
                control.Visible = ((int)control.Tag == -1 || (int)control.Tag == category) &&
                    (control.Name != "Setting_AnnotationDefaults" || annotationExpanded);
                if (control is SettingsNavigation navigation) navigation.SelectedCategory=category;
                if (control.Name == "CategoryHeading") control.Text=SettingsNavigation.Title(category);
                if (control.Name == "CategoryDescription") control.Text=SettingsNavigation.Description(category);
            }
            basicSettings.ResumeLayout(true); FitBasicSettings(); basicSettings.AutoScrollPosition = Point.Empty;
        }
        private void UpdateNavigation()
        {
            var active = settingsVisible ? settingsTab : history ? historyTab : recentTab;
            foreach (var tab in new[] { recentTab, historyTab, settingsTab })
            {
                tab.BackColor = tab == active ? Ui.Accent : Ui.Surface;
                tab.ForeColor = tab == active ? Ui.Background : Ui.Text;
                tab.AccessibleDescription = tab == active ? "当前页面" : "切换页面";
            }
        }
        private void AddNumber(string title, int value, int min, int max, Action<int> apply, bool updateLayout = false)
        {
            basicSettings.Controls.Add(new Label { Text = title, AutoSize = true, Margin = new Padding(3, 14, 3, 3) });
            var row = new FlowLayoutPanel { Name = "PresetValue", Margin=new Padding(0,3,0,3), Tag = updateLayout ? 2 : 1, WrapContents = true, Height = 44, Width = 220 };
            int[] presets = updateLayout ? new[] { 180, 220, 280, 320, 400, 480, 640 } : min == 0 ? new[] { 1, 3, 7, 30, 0 } : new[] { 10, 20, 50, 100 };
            var preset = new SoftChoice { Width = 112 };
            foreach (int n in presets) preset.Items.Add(n == 0 ? "不自动清理" : n.ToString()); preset.Items.Add("自定义");
            int selected = Array.IndexOf(presets, value); preset.SelectedIndex = selected >= 0 ? selected : presets.Length;
            var custom = new TextBox { Width = 68, Text = value.ToString(), Visible = selected < 0, BorderStyle = BorderStyle.FixedSingle, AccessibleName = title + "自定义值" };
            Action<int> save = n => { apply(n); app.Safe(() => { if (updateLayout) app.ApplySettings(); else app.SaveBasicPreferences(); }); };
            preset.SelectedIndexChanged += (s,e) => { custom.Visible = preset.SelectedIndex == presets.Length; if (!custom.Visible) { custom.Text = presets[preset.SelectedIndex].ToString(); save(presets[preset.SelectedIndex]); } FitBasicSettings(); };
            Action commit = () => { if (!custom.Visible) return; if (int.TryParse(custom.Text, out int n) && n >= min && n <= max) { custom.BackColor = Ui.Surface; save(n); } else { custom.BackColor = Ui.Dark ? Color.FromArgb(93, 43, 43) : Color.MistyRose; } };
            custom.Leave += (s,e) => commit(); custom.KeyDown += (s,e) => { if (e.KeyCode == Keys.Enter) { commit(); e.SuppressKeyPress = true; } };
            row.Controls.Add(preset); row.Controls.Add(custom); basicSettings.Controls.Add(row);
        }
        protected override void OnShown(EventArgs e) { base.OnShown(e); PositionBar(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); callback = Native.RegisterWindowMessage("ShotCab.AppBar"); }
        private void DisplayChanged(object s, EventArgs e) { if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)ApplySettings); }
        public void RefreshItems()
        {
            strip.SetItems(app.Store.Query(new HistoryQuery { Limit = history ? 50 : app.Settings.RecentCount, ExcludeHidden = true }).ToList(), history);
            UpdateFooter(); strip.Invalidate();
        }
        private void UpdateFooter() => footer.Text = strip.SelectedCount > 1
            ? string.Format(Localize.T("已选 {0} 张 · 批量操作"), strip.SelectedCount)
            : Localize.T("打开完整历史  ↗");
        public void RefreshClipboard() => strip.Invalidate();
        internal void SelectHistoryTab() { history = true; ShowBasicSettings(false); RefreshItems(); }
        internal void SelectRecentTab() { history = false; ShowBasicSettings(false); RefreshItems(); }
        public void ApplySettings()
        {
            if (IsDisposed) return;
            RemoveReservation(); collapsed = false;
            // Keep text fully opaque. The frosted surface is drawn independently below.
            Opacity = 1;
            resizeGrip.Dock = app.Settings.SidebarSide == SidebarDockSide.靠左 ? DockStyle.Right : DockStyle.Left;
            if ((!app.Settings.SidebarEnabled || app.SidebarManuallyHidden || app.ManuallyHidden) && !settingsVisible) { Hide(); return; }
            PositionBar(); Show(); RefreshItems();
        }
        private void PositionBar(bool updateReservation = true)
        {
            if (repositioning || IsDisposed) return;
            repositioning = true;
            try
            {
                var screen = Screen.AllScreens[Math.Max(0, Math.Min(Screen.AllScreens.Length - 1, app.Settings.SidebarScreen))];
                int width = (int)Math.Round((collapsed ? 8 : Math.Max(AppSettings.SidebarMinimumWidth, Math.Min(AppSettings.SidebarMaximumWidth, app.Settings.SidebarWidth))) * DeviceDpi / 96d);
                bool left = app.Settings.SidebarSide == SidebarDockSide.靠左;
                var area = screen.WorkingArea;
                if (!updateReservation && app.Settings.SidebarReserveSpace && reserved)
                    Bounds = new Rectangle(left ? screen.Bounds.Left : screen.Bounds.Right-width, screen.Bounds.Top, width, screen.Bounds.Height);
                else if (app.Settings.SidebarReserveSpace && app.Settings.SidebarEnabled &&
                    !app.SidebarManuallyHidden && !app.ManuallyHidden && !collapsed)
                {
                    var data = Data();
                    if (!reserved) { Native.SHAppBarMessage(0, ref data); reserved = true; }
                    data.Rect = new Native.Rect { Left = left ? screen.Bounds.Left : screen.Bounds.Right - width, Right = left ? screen.Bounds.Left + width : screen.Bounds.Right, Top = screen.Bounds.Top, Bottom = screen.Bounds.Bottom };
                    Native.SHAppBarMessage(2, ref data); if (left) data.Rect.Right = data.Rect.Left + width; else data.Rect.Left = data.Rect.Right - width; Native.SHAppBarMessage(3, ref data);
                    Bounds = data.Rect.Rectangle;
                }
                // ABM_REMOVE updates WorkingArea asynchronously. Anchor the overlay to
                // the physical monitor edge so disabling reservation cannot leave a gap.
                else Bounds = OverlayBounds(screen.Bounds, area, width, left);
            }
            finally { repositioning = false; }
        }
        internal static Rectangle OverlayBounds(Rectangle display, Rectangle working, int width, bool left) =>
            new Rectangle(left ? display.Left : display.Right - width, working.Top, width, working.Height);
        private Native.AppBarData Data() => new Native.AppBarData { Size = (uint)Marshal.SizeOf(typeof(Native.AppBarData)), Hwnd = Handle, CallbackMessage = callback, Edge = app.Settings.SidebarSide == SidebarDockSide.靠左 ? 0u : 2u };
        internal void ResizeFromDrag(int screenX)
        {
            int delta = screenX - resizeStartX;
            int pixels = resizeStartWidth + (app.Settings.SidebarSide == SidebarDockSide.靠左 ? delta : -delta);
            int width = Math.Max(AppSettings.SidebarMinimumWidth, Math.Min(AppSettings.SidebarMaximumWidth, (int)Math.Round(pixels * 96d / DeviceDpi)));
            if (width == app.Settings.SidebarWidth) return;
            app.Settings.SidebarWidth = width;
            PositionBar(!resizing);
        }
        private void FinishResize()
        {
            if (!resizing) return;
            resizing = false;
            resizeClock.Stop();
            resizeGrip.Capture = false;
            ResizeFromDrag(Cursor.Position.X);
            PositionBar();
            app.Safe(app.SaveBasicPreferences);
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            using (var brush = new SolidBrush(Ui.Background)) e.Graphics.FillRectangle(brush, ClientRectangle);
            using (var edge = new Pen(Ui.Dark ? Color.FromArgb(56,56,59) : Color.FromArgb(209,224,240)))
                e.Graphics.DrawRectangle(edge, 0, 0, Width-1, Height-1);
        }
        public void RemoveReservation() { if (reserved && IsHandleCreated) { var data = Data(); Native.SHAppBarMessage(1, ref data); reserved = false; } }
        private void CheckState()
        {
            if (app.IsCapturing || app.ManuallyHidden || resizing || ContainsFocus) return;
            bool fullscreen = app.Settings.FullscreenMode != 0 && Native.IsFullscreen(Handle);
            app.SetPinsFullscreenHidden(app.Settings.FullscreenMode == 2 && fullscreen);
            if (!app.Settings.SidebarEnabled || app.SidebarManuallyHidden) return;
            if (fullscreen && !fullscreenHidden) { fullscreenHidden = true; RemoveReservation(); Hide(); }
            else if (!fullscreen && fullscreenHidden) { fullscreenHidden = false; PositionBar(); Show(); }
            if (!fullscreenHidden && app.Settings.SidebarAutoFold && !app.Settings.SidebarReserveSpace)
            {
                bool fold = !Bounds.Contains(Cursor.Position);
                if (fold != collapsed) { collapsed = fold; PositionBar(); }
            }
        }
        protected override void WndProc(ref Message m)
        {
            if ((uint)m.Msg == callback && m.WParam.ToInt32() == 1 && !repositioning) PositionBar();
            if (m.Msg == 0x21 && !settingsVisible) { m.Result = new IntPtr(3); return; }
            base.WndProc(ref m);
        }
        protected override void Dispose(bool disposing) { if (disposing) { RemoveReservation(); timer.Dispose(); Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged; } base.Dispose(disposing); }
    }

    internal sealed class BufferedSettingsPanel : FlowLayoutPanel
    {
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr handle, string appName, string idList);
        public BufferedSettingsPanel() { DoubleBuffered = true; SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (Ui.Dark) SetWindowTheme(Handle, "DarkMode_Explorer", null);
        }
        protected override Point ScrollToControl(Control activeControl) => AutoScrollPosition;
    }

    internal sealed class PhotoStrip : Control
    {
        private readonly CabinetContext app;
        private List<HistoryItem> items = new List<HistoryItem>();
        private readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();
        private readonly ToolTip tips = new ToolTip();
        private int scroll;
        private Point down;
        private string pressed, hovered;
        private readonly HistorySelection selection = new HistorySelection();
        internal event Action SelectionChanged;
        internal int SelectedCount => selection.Count;
        internal IReadOnlyList<HistoryItem> SelectedItems => rows.Where(r => r.Item != null && selection.Contains(r.Item.Id)).Select(r => r.Item).ToArray();
        private const int Cell = 192;
        private sealed class Row { public HistoryItem Item; public DateTime Date; public int Top, Height, Count; }
        private readonly List<Row> rows = new List<Row>();
        private readonly HashSet<DateTime> foldedDates = new HashSet<DateTime>();
        private bool groupDates;
        private bool emptyActionHovered;
        private bool dragImportActive;
        public PhotoStrip(CabinetContext app) { this.app = app; DoubleBuffered = true; SetStyle(ControlStyles.SupportsTransparentBackColor, true); SetStyle(ControlStyles.Selectable, false); BackColor = Color.Transparent; AllowDrop = true; }
        public void SetItems(List<HistoryItem> values, bool grouped = false)
        {
            bool added = values.FirstOrDefault()?.Id != items.FirstOrDefault()?.Id;
            if (groupDates != grouped) selection.Clear();
            items = values; groupDates = grouped; emptyActionHovered = false; Cursor = Cursors.Default;
            BuildRows(); PruneSelection();
            foreach (var b in cache.Values) b.Dispose(); cache.Clear();
            scroll = added ? 0 : Math.Min(scroll, MaxScroll); Invalidate();
        }
        internal static string DateHeading(DateTime date, DateTime today) => date.Date == today.Date ? Localize.T("今天") : date.Date == today.Date.AddDays(-1) ? Localize.T("昨天") : date.ToString("yyyy-MM-dd");
        internal string EmptyStateTitle => groupDates ? Localize.T("暂无历史截图") : Localize.T("还没有近期截图");
        private Rectangle EmptyActionBounds => new Rectangle(Math.Max(8, (Width - 136) / 2), 244, Math.Min(136, Width - 16), 34);
        private void BuildRows()
        {
            rows.Clear(); int top=0;
            foreach (var day in items.GroupBy(x => x.CreatedUtc.ToLocalTime().Date))
            {
                if (groupDates) { rows.Add(new Row { Date=day.Key, Top=top, Height=42, Count=day.Count() }); top+=42; }
                if (groupDates && foldedDates.Contains(day.Key)) continue;
                foreach(var item in day) { rows.Add(new Row { Item=item,Date=day.Key,Top=top,Height=ItemHeight(item) }); top+=ItemHeight(item); }
            }
        }
        internal void ToggleDate(DateTime date) { if (!foldedDates.Add(date.Date)) foldedDates.Remove(date.Date); BuildRows(); PruneSelection(); scroll=Math.Min(scroll,MaxScroll); Invalidate(); }
        private IReadOnlyList<string> VisibleIds() => rows.Where(r => r.Item != null).Select(r => r.Item.Id).ToArray();
        private void PruneSelection() { int before = selection.Count; selection.SetVisible(VisibleIds()); if (selection.Count != before) SelectionChanged?.Invoke(); }
        private void SelectionDidChange() { SelectionChanged?.Invoke(); Invalidate(); }
        private int MaxScroll => Math.Max(0, (rows.Count == 0 ? 0 : rows.Last().Top + rows.Last().Height) - Height);
        protected override void OnMouseWheel(MouseEventArgs e) { scroll = Math.Max(0, Math.Min(MaxScroll, scroll - e.Delta / 2)); Invalidate(); }
        internal bool CanImport(IDataObject data)
        {
            if (!app.Settings.HistoryEnabled || data == null) return false;
            if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] paths && paths.Any(CabinetContext.IsImportableImageFile)) return true;
            return data.GetDataPresent(DataFormats.Bitmap);
        }
        protected override void OnDragEnter(DragEventArgs e) { SetImportFeedback(e, true); base.OnDragEnter(e); }
        protected override void OnDragOver(DragEventArgs e) { SetImportFeedback(e, false); base.OnDragOver(e); }
        private void SetImportFeedback(DragEventArgs e, bool repaint)
        {
            bool accept = (e.AllowedEffect & DragDropEffects.Copy) != 0 && CanImport(e.Data);
            e.Effect = accept ? DragDropEffects.Copy : DragDropEffects.None;
            if (dragImportActive != accept) { dragImportActive = accept; Invalidate(); }
            else if (repaint && accept) Invalidate();
        }
        protected override void OnDragLeave(EventArgs e) { dragImportActive = false; Invalidate(); base.OnDragLeave(e); }
        protected override void OnDragDrop(DragEventArgs e)
        {
            dragImportActive = false; Invalidate();
            ImportData(e.Data);
            base.OnDragDrop(e);
        }
        internal void ImportData(IDataObject data)
        {
            if (CanImport(data)) app.Safe(() =>
            {
                if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] paths && paths.Any(CabinetContext.IsImportableImageFile))
                    app.ImportDroppedImages(paths);
                else if (data.GetDataPresent(DataFormats.Bitmap) && data.GetData(DataFormats.Bitmap) is Image image)
                    app.ImportDroppedImage(image);
            });
        }
        private HistoryItem At(Point p) => rows.FirstOrDefault(r => p.Y+scroll >= r.Top && p.Y+scroll < r.Top+r.Height)?.Item;
        protected override void OnMouseDown(MouseEventArgs e) { down = e.Location; pressed = At(e.Location)?.Id; base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (items.Count == 0)
            {
                bool hover = EmptyActionBounds.Contains(e.Location);
                if (hover != emptyActionHovered) { emptyActionHovered = hover; Cursor = hover ? Cursors.Hand : Cursors.Default; Invalidate(EmptyActionBounds); }
                base.OnMouseMove(e); return;
            }
            var item = At(e.Location);
            if (item?.Id != hovered) { hovered = item?.Id; tips.SetToolTip(this, item == null ? "" : string.Join(" · ", Labels(item)) + (item.Redacted && !item.RedactionBaked ? "\n内部原图仍保留，可重新编辑。" : "") + "\n" + Localize.T("Ctrl+单击增减选择，Shift+单击选择范围")); }
            if (e.Button == MouseButtons.Left && pressed != null && (Math.Abs(e.X - down.X) + Math.Abs(e.Y - down.Y) > 8))
            {
                var id = pressed; pressed = null; var hit = items.First(x => x.Id == id);
                Keys previewKey = app.Settings.PreviewDragKey == CardDragModifier.Ctrl ? Keys.Control : app.Settings.PreviewDragKey == CardDragModifier.Shift ? Keys.Shift : Keys.Alt;
                if ((ModifierKeys & previewKey) != 0) { app.Safe(() => app.PreviewCard(hit, Cursor.Position)); return; }
                app.Safe(() => { using (var image = Images.Load(hit.CurrentPath)) using (var data = app.Clipboard.Build(image, true, false, app.Settings.ExportFormat, app.Settings.JpegQuality)) DoDragDrop(data, DragDropEffects.Copy); });
            }
            base.OnMouseMove(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (items.Count == 0)
            {
                if (e.Button == MouseButtons.Left && EmptyActionBounds.Contains(e.Location))
                {
                    if (groupDates) app.ShowHistory(); else app.Capture("region");
                }
                base.OnMouseUp(e); return;
            }
            var row = rows.FirstOrDefault(r => e.Y+scroll >= r.Top && e.Y+scroll < r.Top+r.Height);
            if (e.Button == MouseButtons.Left && row != null && row.Item == null) { pressed=null; ToggleDate(row.Date); return; }
            var item = At(e.Location);
            if (item == null)
            {
                if (e.Button == MouseButtons.Left && selection.Count > 0) { selection.Clear(); SelectionDidChange(); }
                pressed = null; base.OnMouseUp(e); return;
            }
            var visibleIds = VisibleIds();
            if (e.Button == MouseButtons.Right)
            {
                selection.SelectForContext(visibleIds, item.Id); pressed = null; SelectionDidChange();
                if (selection.Count > 1) app.BatchItemMenu(SelectedItems).Show(this, e.Location);
                else app.ItemMenu(item).Show(this, e.Location);
                return;
            }
            if (e.Button == MouseButtons.Left && pressed == item.Id)
            {
                bool control = (ModifierKeys & Keys.Control) != 0;
                bool shift = (ModifierKeys & Keys.Shift) != 0;
                selection.Click(visibleIds, item.Id, control, shift);
                SelectionDidChange();
                if (!control && !shift) app.Copy(item);
            }
            pressed = null; base.OnMouseUp(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (items.Count == 0) { DrawEmptyState(e.Graphics); DrawImportFeedback(e.Graphics); return; }
            var visible = new HashSet<string>();
            foreach (var row in rows.Where(r => r.Top+r.Height > scroll && r.Top-scroll < Height))
            {
                int top=row.Top-scroll;
                if (row.Item == null) { string text=(foldedDates.Contains(row.Date) ? "›  " : "⌄  ")+DateHeading(row.Date,DateTime.Today)+"   · "+row.Count; TextRenderer.DrawText(e.Graphics,text,Font,new Rectangle(16,top,Width-32,42),Ui.Muted,TextFormatFlags.VerticalCenter); continue; }
                var item = row.Item; visible.Add(item.Id);
                var box = new Rectangle(10, top + 4, Math.Max(40, Width - 20), row.Height - 12);
                using (var path = Ui.Rounded(box, 10)) using (var brush = new SolidBrush(Ui.Surface)) e.Graphics.FillPath(brush, path);
                if (!cache.TryGetValue(item.Id, out var thumb))
                {
                    try { thumb = Images.Thumbnail(item.CurrentPath); cache[item.Id] = thumb; } catch (Exception) { thumb = null; }
                }
                if (thumb != null)
                {
                    double scale = Math.Min((box.Width - 12d) / thumb.Width, 108d / thumb.Height);
                    var target = new Rectangle(box.X + (box.Width - (int)(thumb.Width * scale)) / 2, box.Y + 8, (int)(thumb.Width * scale), (int)(thumb.Height * scale));
                    e.Graphics.DrawImage(thumb, target);
                }
                foreach (var badge in BadgeLayout(item))
                {
                    var labelBox=badge.Bounds; labelBox.Offset(box.X+8,box.Y+122);
                    using (var path=Ui.Rounded(labelBox,8)) using(var brush=new SolidBrush(Color.FromArgb(badge.Custom ? 24 : 42,badge.Color))) e.Graphics.FillPath(brush,path);
                    if (badge.Custom) using(var path=Ui.Rounded(labelBox,8)) using(var pen=new Pen(Color.FromArgb(90,badge.Color))) e.Graphics.DrawPath(pen,path);
                    TextRenderer.DrawText(e.Graphics,badge.Text,Font,labelBox,Ui.Dark ? badge.Color : ControlPaint.Dark(badge.Color),TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
                string caption = (item.Favorite ? "★ " : "") + item.CreatedUtc.ToLocalTime().ToString("MM-dd HH:mm") + (Width < 230 ? "" : "  " + item.Width + "×" + item.Height);
                TextRenderer.DrawText(e.Graphics, caption, Font, new Rectangle(box.X + 8, box.Bottom - 28, box.Width - 16, 23), Ui.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
                if (selection.Contains(item.Id)) using (var path = Ui.Rounded(box, 10)) using (var pen = new Pen(Ui.Accent, 2)) e.Graphics.DrawPath(pen, path);
                if (app.Clipboard.OwnsClipboard && app.Clipboard.ItemId == item.Id) using (var brush = new SolidBrush(Color.FromArgb(139, 226, 175))) e.Graphics.FillEllipse(brush, box.X + 9, box.Y + 9, 9, 9);
            }
            foreach (var key in cache.Keys.Where(x => !visible.Contains(x)).ToArray()) { cache[key].Dispose(); cache.Remove(key); }
            DrawImportFeedback(e.Graphics);
        }
        private void DrawImportFeedback(Graphics graphics)
        {
            if (!dragImportActive) return;
            var bounds = Rectangle.Inflate(ClientRectangle, -8, -8);
            using (var path = Ui.Rounded(bounds, 11))
            using (var fill = new SolidBrush(Color.FromArgb(205, Ui.Surface)))
            using (var border = new Pen(Ui.Accent, 2) { DashStyle = DashStyle.Dash })
            { graphics.FillPath(fill, path); graphics.DrawPath(border, path); }
            TextRenderer.DrawText(graphics, Localize.T("松开导入图片"), Font, bounds, Ui.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }
        private void DrawEmptyState(Graphics graphics)
        {
            int center = Width / 2;
            using (var pen = new Pen(Ui.Muted, 2))
            {
                if (groupDates)
                {
                    graphics.DrawRectangle(pen, center - 23, 81, 40, 34);
                    graphics.DrawRectangle(pen, center - 17, 88, 40, 34);
                    graphics.DrawLine(pen, center - 8, 99, center + 14, 99);
                    graphics.DrawLine(pen, center - 8, 107, center + 8, 107);
                }
                else
                {
                    graphics.DrawLine(pen, center - 22, 91, center - 22, 81);
                    graphics.DrawLine(pen, center - 22, 81, center - 12, 81);
                    graphics.DrawLine(pen, center + 12, 81, center + 22, 81);
                    graphics.DrawLine(pen, center + 22, 81, center + 22, 91);
                    graphics.DrawLine(pen, center - 22, 109, center - 22, 119);
                    graphics.DrawLine(pen, center - 22, 119, center - 12, 119);
                    graphics.DrawLine(pen, center + 12, 119, center + 22, 119);
                    graphics.DrawLine(pen, center + 22, 109, center + 22, 119);
                }
            }
            using (var titleFont = Ui.Font(11, FontStyle.Bold))
                TextRenderer.DrawText(graphics, EmptyStateTitle, titleFont, new Rectangle(12, 141, Width - 24, 29), Ui.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            string description = groupDates ? Localize.T("按日期浏览截图\n隐藏项见完整历史") : Localize.T("按 {0} 截图，完成后自动加入近期").Replace("{0}", app.Settings.CaptureShortcut);
            TextRenderer.DrawText(graphics, description, Font, new Rectangle(16, 177, Width - 32, 58), Ui.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
            var action = EmptyActionBounds;
            using (var path = Ui.Rounded(action, 10))
            using (var brush = new SolidBrush(groupDates ? Ui.Surface : Ui.Accent))
            using (var border = new Pen(groupDates ? Ui.Muted : Ui.Accent))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(border, path);
            }
            TextRenderer.DrawText(graphics, groupDates ? Localize.T("打开完整历史") : Localize.T("开始截图"), Font, action, groupDates ? Ui.Text : Ui.Background, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (emptyActionHovered) using (var path = Ui.Rounded(Rectangle.Inflate(action, 2, 2), 11)) using (var border = new Pen(Ui.Muted)) graphics.DrawPath(border, path);
            TextRenderer.DrawText(graphics, Localize.T("也可拖入图片"), Font, new Rectangle(16, 288, Width - 32, 25), Ui.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        internal sealed class Badge { public string Text; public Rectangle Bounds; public Color Color; public bool Custom; }
        internal List<Badge> BadgeLayout(HistoryItem item)
        {
            var result=new List<Badge>(); int available=Math.Max(24,Width-36), x=0,y=0;
            Action<string,Color,bool> add=(text,color,custom) => {
                int width=Math.Min(available,TextRenderer.MeasureText(text,Font).Width+16);
                if(x>0 && x+width>available) { x=0; y+=28; }
                result.Add(new Badge { Text=text,Color=color,Custom=custom,Bounds=new Rectangle(x,y,width,23) }); x+=width+6;
            };
            if(item.RedactionBaked) add(Localize.T("遮挡已固化"),Color.FromArgb(169,137,238),false);
            else if(item.Redacted) add(Localize.T("已打码"),Color.FromArgb(239,180,86),false);
            add(Localize.T(item.Edited || item.RedactionBaked ? "已编辑" : "原图"),item.Edited || item.RedactionBaked ? Color.FromArgb(106,176,242) : Color.FromArgb(104,198,161),false);
            if(item.Tags.Count>0) { x=0; y+=28; }
            foreach(var tag in item.Tags) add("# " + tag,Color.FromArgb(184,164,223),true);
            return result;
        }
        private int ItemHeight(HistoryItem item) => Cell+BadgeLayout(item).Last().Bounds.Y;
        protected override void OnResize(EventArgs e) { base.OnResize(e); if(items==null) return; BuildRows(); scroll=Math.Min(scroll,MaxScroll); Invalidate(); }
        internal static List<string> Labels(HistoryItem x)
        {
            var labels = new List<string>();
            if (x.RedactionBaked) labels.Add("遮挡已固化"); else if (x.Redacted) labels.Add("已打码");
            labels.Add(x.Edited || x.RedactionBaked ? "已编辑" : "原图"); labels.AddRange(x.Tags); return labels;
        }
        protected override void Dispose(bool disposing) { if (disposing) { foreach (var b in cache.Values) b.Dispose(); tips.Dispose(); } base.Dispose(disposing); }
    }
}
