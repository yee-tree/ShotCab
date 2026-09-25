using ShotCab.Core;
using ShareX.ScreenCaptureLib;
using ShareX.HelpersLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace ShotCab.App
{
    internal sealed partial class SidebarForm
    {
        private bool annotationExpanded;
        private static string SettingText(string chinese, string english) => Localize.English ? english : chinese;

        private void AddMigratedSettings()
        {
            // Keep the six top-level destinations. These are the fields that previously
            // lived only in the separate detailed-settings window.
            AddSettingsNumber(0, "JpegQuality", "JPG 质量", "JPG quality", app.Settings.JpegQuality,
                new[] { 70, 80, 90, 95, 100 }, 1, 100, value => app.Settings.JpegQuality = value);
            AddSettingsToggle(0, "EditAsNew", "修改历史时生成新记录", "Create a new record when editing", app.Settings.EditAsNew,
                value => { app.Settings.EditAsNew = value; app.SaveBasicPreferences(); },
                "关闭：保存修改会更新当前历史记录。打开：保留旧记录，并把修改结果新增为一张截图。",
                "Off: edits update the current history item. On: keep it and add the edited result as a new item.");

            AddSettingsNumber(1, "RecycleDays", "回收站保留天数", "Recycle-bin retention days", app.Settings.RecycleDays,
                new[] { 0, 1, 3, 7, 30 }, 0, 36500, value => app.Settings.RecycleDays = value);

            AddSettingsToggle(2, "SidebarEnabled", "显示侧栏", "Show sidebar", app.Settings.SidebarEnabled,
                value => { app.Settings.SidebarEnabled = value; app.Safe(app.ApplySettings); });
            AddSettingsToggle(2, "SidebarReserveSpace", "为侧栏预留桌面空间", "Reserve desktop space", app.Settings.SidebarReserveSpace,
                value => { app.Settings.SidebarReserveSpace = value; app.Safe(app.ApplySettings); },
                "打开后，Windows 会缩小桌面工作区，普通窗口不会被侧栏覆盖。关闭后恢复工作区，侧栏贴回所选屏幕边缘并覆盖显示。",
                "On: Windows reduces the work area so normal windows avoid the sidebar. Off: restore the work area and overlay the sidebar at the chosen screen edge.");
            AddSettingsChoice(2, "SidebarScreen", "所在显示器", "Display", Math.Max(0, Math.Min(Screen.AllScreens.Length - 1, app.Settings.SidebarScreen)),
                Enumerable.Range(1, Screen.AllScreens.Length).Select(n => SettingText("显示器 " + n, "Display " + n)).ToArray(),
                index => { app.Settings.SidebarScreen = index; app.Safe(app.ApplySettings); });
            AddSettingsToggle(2, "SidebarAutoFold", "不用时自动折叠", "Auto-fold when idle", app.Settings.SidebarAutoFold,
                value => { app.Settings.SidebarAutoFold = value; app.SaveBasicPreferences(); });
            AddAnnotationDefaults();

            AddSettingsNumber(4, "CapacityWarningGB", "容量提醒阈值（GB）", "Storage warning threshold (GB)", app.Settings.CapacityWarningGB,
                new[] { 0, 1, 2, 5, 10 }, 0, 10000, value => app.Settings.CapacityWarningGB = value);
            AddSettingsLabel(4, "StorageHint", "0 表示关闭容量提醒；按天数清理仍照常进行。", "0 disables capacity warnings; time-based cleanup continues.");
            AddSettingsButton(4, "StorageManage", "存储管理与目录迁移", "Storage management and folder migration", app.ShowHistory);

            AddSettingsToggle(5, "AutoOcrOnEdit", "进入编辑器时自动识别文字", "Recognize text when opening editor", app.Settings.AutoOcrOnEdit,
                value => { app.Settings.AutoOcrOnEdit = value; app.SaveBasicPreferences(); },
                "打开后，每次进入图片编辑器便自动识别，并在右侧显示文字；关闭后仍可点击编辑器中的识别按钮。需要已安装离线 OCR 组件。",
                "On: recognize text and show it on the right each time the image editor opens. Off: use the editor OCR button on demand. The offline component must be installed.");
            AddSettingsLabel(5, "OcrHint", "离线 OCR 按需启动；安装版会内置组件，便携版可在下方指定路径。", "Offline OCR runs locally. The future installer will include it; portable builds can select the path below.");
            AddSettingsText(5, "OcrExecutablePath", "OCR 程序路径", "OCR executable path", app.Settings.OcrExecutablePath,
                value => { app.Settings.OcrExecutablePath = value; app.SaveBasicPreferences(); });
            AddSettingsButton(5, "OcrBrowse", "选择 ShotCab.Ocr.exe", "Choose ShotCab.Ocr.exe", () =>
            {
                using (var picker = new OpenFileDialog { Filter = "ShotCab OCR|ShotCab.Ocr.exe" })
                    if (picker.ShowDialog(this) == DialogResult.OK)
                    {
                        app.Settings.OcrExecutablePath = picker.FileName;
                        app.SaveBasicPreferences();
                        var field = basicSettings.Controls["Setting_OcrExecutablePath"] as SoftEntry;
                        if (field != null) field.Input.Text = picker.FileName;
                    }
            });
            AddSettingsNumber(5, "OcrPreviewSeconds", "识字结果预览秒数", "Text preview seconds", app.Settings.OcrPreviewSeconds,
                new[] { 0, 3, 5, 10 }, 0, 120, value => app.Settings.OcrPreviewSeconds = value);
        }

        private void AddSettingsLabel(int category, string key, string chinese, string english)
        {
            basicSettings.Controls.Add(new Label
            {
                Name = "Setting_" + key, Tag = category, Text = SettingText(chinese, english),
                AutoSize = true, ForeColor = Ui.Muted, Margin = new Padding(3, 8, 3, 6)
            });
        }

        private void AddSettingsToggle(int category, string key, string chinese, string english, bool current, Action<bool> save,
            string chineseHelp = null, string englishHelp = null)
        {
            var toggle = new SoftToggle
            {
                Name = "Setting_" + key, Tag = category, Text = SettingText(chinese, english),
                AutoSize = true, Checked = current, HelpText = SettingText(chineseHelp, englishHelp)
            };
            toggle.CheckedChanged += (sender, args) => save(toggle.Checked);
            basicSettings.Controls.Add(toggle);
        }

        private void AddSettingsChoice(int category, string key, string chinese, string english, int selected,
            string[] options, Action<int> save)
        {
            AddSettingsLabel(category, key + "Label", chinese, english);
            var choice = new SoftChoice { Name = "Setting_" + key, Tag = category, Width = 170 };
            choice.Items.AddRange(options);
            choice.SelectedIndex = selected;
            choice.SelectedIndexChanged += (sender, args) => save(choice.SelectedIndex);
            basicSettings.Controls.Add(choice);
        }

        private void AddSettingsButton(int category, string key, string chinese, string english, Action action)
        {
            var button = Ui.GlassButton(SettingText(chinese, english), action);
            button.Name = "Setting_" + key; button.Tag = category;
            basicSettings.Controls.Add(button);
        }

        private void AddSettingsText(int category, string key, string chinese, string english, string current, Action<string> save)
        {
            AddSettingsLabel(category, key + "Label", chinese, english);
            var entry = new SoftEntry { Name = "Setting_" + key, Tag = category, Width = 220 };
            entry.Input.Text = current ?? string.Empty;
            entry.Input.AccessibleName = SettingText(chinese, english);
            Action commit = () => save(entry.Input.Text.Trim());
            entry.Input.Leave += (sender, args) => commit();
            entry.Input.KeyDown += (sender, args) => { if (args.KeyCode == Keys.Enter) { commit(); args.SuppressKeyPress = true; } };
            basicSettings.Controls.Add(entry);
        }

        private void AddSettingsNumber(int category, string key, string chinese, string english, double current,
            int[] presets, double minimum, double maximum, Action<double> save, bool wholeNumber = false)
        {
            AddSettingsLabel(category, key + "Label", chinese, english);
            var row = new FlowLayoutPanel
            {
                Name = "Setting_Number_" + key, Tag = category, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true, Width = 220, Height = 44, Margin = new Padding(0, 2, 0, 5)
            };
            var choice = new SoftChoice { Width = 112 };
            string zero = key == "RecycleDays" ? SettingText("当天清理", "Same day") :
                key == "CapacityWarningGB" ? SettingText("关闭提醒", "No warning") :
                SettingText("关闭预览", "No preview");
            foreach (int value in presets) choice.Items.Add(value == 0 ? zero : value.ToString());
            choice.Items.Add(SettingText("自定义", "Custom"));
            int selected = Array.FindIndex(presets, value => Math.Abs(value - current) < .0001);
            choice.SelectedIndex = selected >= 0 ? selected : presets.Length;
            var custom = new SoftEntry { Width = 72, Visible = selected < 0 };
            custom.Input.Text = current.ToString(CultureInfo.CurrentCulture);
            custom.Input.AccessibleName = SettingText(chinese, english) + SettingText("自定义值", " custom value");
            Action commit = () =>
            {
                if (!custom.Visible) return;
                if (double.TryParse(custom.Input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value) &&
                    value >= minimum && value <= maximum && (!wholeNumber || value == Math.Truncate(value)))
                {
                    custom.Input.BackColor = Ui.Surface; save(value); app.Safe(app.SaveBasicPreferences);
                }
                else custom.Input.BackColor = Ui.Dark ? Color.FromArgb(93, 43, 43) : Color.MistyRose;
            };
            choice.SelectedIndexChanged += (sender, args) =>
            {
                custom.Visible = choice.SelectedIndex == presets.Length;
                if (!custom.Visible) { custom.Input.Text = presets[choice.SelectedIndex].ToString(); save(presets[choice.SelectedIndex]); app.Safe(app.SaveBasicPreferences); }
                FitBasicSettings();
            };
            custom.Input.Leave += (sender, args) => commit();
            custom.Input.KeyDown += (sender, args) => { if (args.KeyCode == Keys.Enter) { commit(); args.SuppressKeyPress = true; } };
            row.Controls.Add(choice); row.Controls.Add(custom);
            basicSettings.Controls.Add(row);
        }

        private void AddSettingsNumber(int category, string key, string chinese, string english, int current,
            int[] presets, int minimum, int maximum, Action<int> save) =>
            AddSettingsNumber(category, key, chinese, english, (double)current, presets, minimum, maximum,
                value => save((int)value), true);

        private void AddAnnotationDefaults()
        {
            AddSettingsLabel(2, "AnnotationHeading", "默认标注样式", "Annotation defaults");
            var panel = new FlowLayoutPanel
            {
                Name = "Setting_AnnotationDefaults", Tag = 2, Width = 220,
                FlowDirection = FlowDirection.TopDown, WrapContents = false,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(2, 4, 2, 8), Visible = annotationExpanded
            };
            bool built = false;
            Control button = null;
            button = Ui.GlassButton(SettingText(annotationExpanded ? "收起样式选项" : "展开样式选项",
                annotationExpanded ? "Collapse style options" : "Expand style options"), () =>
            {
                annotationExpanded = !annotationExpanded;
                if (annotationExpanded && !built) { BuildAnnotationSettings(panel); built = true; }
                panel.Visible = annotationExpanded && settingsCategory == 2;
                button.Text = SettingText(annotationExpanded ? "收起样式选项" : "展开样式选项",
                    annotationExpanded ? "Collapse style options" : "Expand style options");
                FitBasicSettings(); basicSettings.PerformLayout();
            });
            button.Name = "Setting_AnnotationToggle"; button.Tag = 2;
            basicSettings.Controls.Add(button);
            basicSettings.Controls.Add(panel);
            if (annotationExpanded) { BuildAnnotationSettings(panel); built = true; }
        }

        private void BuildAnnotationSettings(FlowLayoutPanel panel)
        {
            var a = app.AnnotationDefaults;
            AddAnnotationHeading(panel, "形状与线条", "Shapes and strokes");
            AddAnnotationNumber(panel, "选区圆角", "Region corner radius", () => a.RegionCornerRadius, v => a.RegionCornerRadius = v, 0, 100);
            AddAnnotationColor(panel, "边框 / 线条颜色", "Stroke color", () => a.BorderColor, v => a.BorderColor = v);
            AddAnnotationNumber(panel, "线条粗细", "Stroke width", () => a.BorderSize, v => a.BorderSize = v, 0, 50);
            AddAnnotationEnum(panel, "线条样式", "Stroke pattern", () => a.BorderStyle, v => a.BorderStyle = v,
                new[] { "实线", "虚线", "点线", "点划线", "双点划线" }, new[] { "Solid", "Dashed", "Dotted", "Dash dot", "Dash dot dot" });
            AddAnnotationColor(panel, "内部填充", "Fill", () => a.FillColor, v => a.FillColor = v);
            AddAnnotationNumber(panel, "形状圆角", "Shape corner radius", () => a.DrawingCornerRadius, v => a.DrawingCornerRadius = v, 0, 100);
            AddAnnotationToggle(panel, "阴影", "Shadow", () => a.Shadow, v => a.Shadow = v);
            AddAnnotationColor(panel, "阴影颜色", "Shadow color", () => a.ShadowColor, v => a.ShadowColor = v);
            AddAnnotationNumber(panel, "阴影水平偏移", "Shadow horizontal offset", () => a.ShadowOffset.X,
                v => a.ShadowOffset = new Point(v, a.ShadowOffset.Y), -100, 100);
            AddAnnotationNumber(panel, "阴影垂直偏移", "Shadow vertical offset", () => a.ShadowOffset.Y,
                v => a.ShadowOffset = new Point(a.ShadowOffset.X, v), -100, 100);
            AddAnnotationNumber(panel, "线条中点数量", "Line control points", () => a.LineCenterPointCount, v => a.LineCenterPointCount = v, 0, 20);
            AddAnnotationEnum(panel, "箭头方向", "Arrowhead direction", () => a.ArrowHeadDirection, v => a.ArrowHeadDirection = v,
                new[] { "末端", "起点", "两端" }, new[] { "End", "Start", "Both" });

            AddAnnotationHeading(panel, "文字与气泡", "Text and balloons");
            AddTextDefaults(panel, a.TextOutlineOptions, "描边文字", "Outlined text");
            AddAnnotationColor(panel, "文字描边颜色", "Text outline color", () => a.TextOutlineBorderColor, v => a.TextOutlineBorderColor = v);
            AddAnnotationNumber(panel, "文字描边粗细", "Text outline width", () => a.TextOutlineBorderSize, v => a.TextOutlineBorderSize = v, 0, 50);
            AddTextDefaults(panel, a.TextOptions, "气泡 / 文字框", "Balloon / text box");
            AddAnnotationColor(panel, "文字框边框", "Text-box border", () => a.TextBorderColor, v => a.TextBorderColor = v);
            AddAnnotationNumber(panel, "文字框边框粗细", "Text-box border width", () => a.TextBorderSize, v => a.TextBorderSize = v, 0, 50);
            AddAnnotationColor(panel, "文字框填充", "Text-box fill", () => a.TextFillColor, v => a.TextFillColor = v);

            AddAnnotationHeading(panel, "序号与特效", "Numbering and effects");
            AddAnnotationColor(panel, "序号边框", "Number marker border", () => a.StepBorderColor, v => a.StepBorderColor = v);
            AddAnnotationNumber(panel, "序号边框粗细", "Number marker border width", () => a.StepBorderSize, v => a.StepBorderSize = v, 0, 50);
            AddAnnotationColor(panel, "序号填充", "Number marker fill", () => a.StepFillColor, v => a.StepFillColor = v);
            AddAnnotationNumber(panel, "序号文字大小", "Number marker font size", () => a.StepFontSize, v => a.StepFontSize = v, 1, 200);
            AddAnnotationEnum(panel, "序号形式", "Number marker style", () => a.StepType, v => a.StepType = v,
                new[] { "数字", "大写字母", "小写字母", "大写罗马数字", "小写罗马数字" },
                new[] { "Numbers", "Uppercase letters", "Lowercase letters", "Uppercase Roman", "Lowercase Roman" });
            AddAnnotationNumber(panel, "放大强度", "Magnify strength", () => a.MagnifyStrength, v => a.MagnifyStrength = v, 1, 1000);
            AddAnnotationNumber(panel, "贴纸大小", "Sticker size", () => a.StickerSize, v => a.StickerSize = v, 1, 1000);
            AddAnnotationNumber(panel, "模糊半径", "Blur radius", () => a.BlurRadius, v => a.BlurRadius = v, 1, 200);
            AddAnnotationNumber(panel, "马赛克块大小", "Pixelate block size", () => a.PixelateSize, v => a.PixelateSize = v, 1, 200);
            AddAnnotationColor(panel, "实心遮挡颜色", "Solid mask color", () => a.SolidMaskColor, v => a.SolidMaskColor = v);
            AddAnnotationNumber(panel, "聚光灯外部暗度", "Spotlight darkness", () => a.SpotlightDarkness, v => a.SpotlightDarkness = v, 0, 255);
            AddAnnotationColor(panel, "荧光笔颜色", "Highlighter color", () => a.HighlightColor, v => a.HighlightColor = v);
            AddAnnotationEnum(panel, "留白边缘效果", "Canvas-gap edge", () => a.CutOutEffectType, v => a.CutOutEffectType = v,
                new[] { "无", "锯齿", "撕裂", "波浪" }, new[] { "None", "Zigzag", "Torn edge", "Wave" });
            AddAnnotationNumber(panel, "留白边缘大小", "Canvas-gap edge size", () => a.CutOutEffectSize, v => a.CutOutEffectSize = v, 0, 200);
            AddAnnotationColor(panel, "留白背景", "Canvas-gap background", () => a.CutOutBackgroundColor, v => a.CutOutBackgroundColor = v);

            AddAnnotationHeading(panel, "素材路径", "Asset paths");
            AddAnnotationText(panel, "上次插入图片路径", "Last inserted image", () => a.LastImageFilePath, v => a.LastImageFilePath = v);
            AddAnnotationText(panel, "上次贴纸路径", "Last sticker path", () => a.LastStickerPath, v => a.LastStickerPath = v);
        }

        private void AddTextDefaults(FlowLayoutPanel panel, TextDrawingOptions options, string chinese, string english)
        {
            AddAnnotationHeading(panel, chinese, english);
            AddAnnotationText(panel, "字体", "Font", () => options.Font, v => options.Font = v);
            AddAnnotationNumber(panel, "字号", "Font size", () => options.Size, v => options.Size = v, 1, 300);
            AddAnnotationColor(panel, "文字颜色", "Text color", () => options.Color, v => options.Color = v);
            AddAnnotationToggle(panel, "粗体", "Bold", () => options.Bold, v => options.Bold = v);
            AddAnnotationToggle(panel, "斜体", "Italic", () => options.Italic, v => options.Italic = v);
            AddAnnotationToggle(panel, "下划线", "Underline", () => options.Underline, v => options.Underline = v);
            AddAnnotationEnum(panel, "水平对齐", "Horizontal alignment", () => options.AlignmentHorizontal, v => options.AlignmentHorizontal = v,
                new[] { "靠左", "居中", "靠右" }, new[] { "Left", "Center", "Right" });
            AddAnnotationEnum(panel, "垂直对齐", "Vertical alignment", () => options.AlignmentVertical, v => options.AlignmentVertical = v,
                new[] { "顶部", "居中", "底部" }, new[] { "Top", "Center", "Bottom" });
            AddAnnotationToggle(panel, "渐变文字", "Gradient text", () => options.Gradient, v => options.Gradient = v);
            AddAnnotationColor(panel, "渐变第二颜色", "Gradient second color", () => options.Color2, v => options.Color2 = v);
            AddAnnotationEnum(panel, "渐变方向", "Gradient direction", () => options.GradientMode, v => options.GradientMode = v,
                new[] { "水平", "垂直", "向前斜", "向后斜" }, new[] { "Horizontal", "Vertical", "Forward diagonal", "Backward diagonal" });
            AddAnnotationToggle(panel, "回车插入换行", "Enter inserts newline", () => options.EnterKeyNewLine, v => options.EnterKeyNewLine = v);
        }

        private void AddAnnotationHeading(FlowLayoutPanel panel, string chinese, string english)
        {
            panel.Controls.Add(new Label { Text = SettingText(chinese, english), AutoSize = true,
                Font = Ui.Font(9, FontStyle.Bold), ForeColor = Ui.Text, Margin = new Padding(3, 14, 3, 5) });
        }

        private void AddAnnotationLabel(FlowLayoutPanel panel, string chinese, string english)
        {
            panel.Controls.Add(new Label { Text = SettingText(chinese, english), AutoSize = true,
                ForeColor = Ui.Muted, Margin = new Padding(3, 9, 3, 3) });
        }

        private void AddAnnotationNumber(FlowLayoutPanel panel, string chinese, string english, Func<int> read, Action<int> write, int min, int max)
        {
            AddAnnotationLabel(panel, chinese, english);
            var entry = new SoftEntry { Width = 140 };
            entry.Input.Text = read().ToString(CultureInfo.CurrentCulture);
            entry.Input.AccessibleName = SettingText(chinese, english);
            Action commit = () =>
            {
                if (int.TryParse(entry.Input.Text, out int value) && value >= min && value <= max)
                { entry.Input.BackColor = Ui.Surface; write(value); app.Safe(app.SaveAnnotationDefaults); }
                else entry.Input.BackColor = Ui.Dark ? Color.FromArgb(93, 43, 43) : Color.MistyRose;
            };
            entry.Input.Leave += (sender, args) => commit();
            entry.Input.KeyDown += (sender, args) => { if (args.KeyCode == Keys.Enter) { commit(); args.SuppressKeyPress = true; } };
            panel.Controls.Add(entry);
        }

        private void AddAnnotationText(FlowLayoutPanel panel, string chinese, string english, Func<string> read, Action<string> write)
        {
            AddAnnotationLabel(panel, chinese, english);
            var entry = new SoftEntry { Width = 180 };
            entry.Input.Text = read() ?? string.Empty;
            entry.Input.AccessibleName = SettingText(chinese, english);
            Action commit = () => { write(entry.Input.Text.Trim()); app.Safe(app.SaveAnnotationDefaults); };
            entry.Input.Leave += (sender, args) => commit();
            entry.Input.KeyDown += (sender, args) => { if (args.KeyCode == Keys.Enter) { commit(); args.SuppressKeyPress = true; } };
            panel.Controls.Add(entry);
        }

        private void AddAnnotationToggle(FlowLayoutPanel panel, string chinese, string english, Func<bool> read, Action<bool> write)
        {
            var toggle = new SoftToggle { Text = SettingText(chinese, english), AutoSize = true, Checked = read() };
            toggle.CheckedChanged += (sender, args) => { write(toggle.Checked); app.Safe(app.SaveAnnotationDefaults); };
            panel.Controls.Add(toggle);
        }

        private void AddAnnotationColor(FlowLayoutPanel panel, string chinese, string english, Func<Color> read, Action<Color> write)
        {
            AddAnnotationLabel(panel, chinese, english);
            var chip = new SoftColorChoice { Width = 140, Value = read(), AccessibleName = SettingText(chinese, english) };
            chip.ColorChanged += (sender, args) => { write(chip.Value); app.Safe(app.SaveAnnotationDefaults); };
            panel.Controls.Add(chip);
            if (chinese == "内部填充" || chinese == "文字框填充" || chinese == "留白背景")
            {
                var clear = Ui.GlassButton(SettingText("设为透明", "Set transparent"), () =>
                { chip.Value = Color.Transparent; write(Color.Transparent); app.Safe(app.SaveAnnotationDefaults); });
                clear.AutoSize = false; clear.Height = 36; clear.Width = 124;
                clear.AccessibleName = SettingText(chinese, english) + SettingText("设为透明", " transparent");
                panel.Controls.Add(clear);
            }
        }

        private void AddAnnotationEnum<T>(FlowLayoutPanel panel, string chinese, string english,
            Func<T> read, Action<T> write, string[] chineseOptions, string[] englishOptions) where T : struct
        {
            AddAnnotationLabel(panel, chinese, english);
            T[] values = Enum.GetValues(typeof(T)).Cast<T>().ToArray();
            var choice = new SoftChoice { Width = 170 };
            string[] labels = Localize.English ? englishOptions : chineseOptions;
            for (int i = 0; i < values.Length; i++) choice.Items.Add(i < labels.Length ? labels[i] : values[i].ToString());
            choice.SelectedIndex = Math.Max(0, Array.IndexOf(values, read()));
            choice.SelectedIndexChanged += (sender, args) => { write(values[choice.SelectedIndex]); app.Safe(app.SaveAnnotationDefaults); };
            panel.Controls.Add(choice);
        }

        private void FitAnnotationSettings(FlowLayoutPanel panel, int width)
        {
            panel.Width = width;
            int inner = Math.Max(96, width - panel.Padding.Horizontal - 8);
            foreach (Control child in panel.Controls)
            {
                if (child is Label || child is SoftToggle) child.MaximumSize = new Size(inner, 0);
                if (child is SoftEntry || child is SoftChoice || child is SoftColorChoice || child.AccessibleRole == AccessibleRole.PushButton)
                    child.Width = Math.Min(inner, 210);
            }
        }
    }

    internal sealed class SoftColorChoice : Control
    {
        private Color value;
        internal event EventHandler ColorChanged;
        internal Color Value { get => value; set { this.value = value; Invalidate(); } }
        internal SoftColorChoice()
        {
            Height = 36; Cursor = Cursors.Hand; TabStop = true; AccessibleRole = AccessibleRole.PushButton;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor |
                ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }
        protected override void OnClick(EventArgs e)
        {
            using (var picker = new ColorDialog { Color = Value.A == 0 ? Color.White : Value, FullOpen = true, AnyColor = true })
                if (picker.ShowDialog(FindForm()) == DialogResult.OK) { Value = picker.Color; ColorChanged?.Invoke(this, EventArgs.Empty); }
            base.OnClick(e);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Ui.Rounded(new Rectangle(1, 1, Width - 3, Height - 3), 9))
            using (var brush = new SolidBrush(Ui.Surface))
            using (var border = new Pen(Color.FromArgb(90, Ui.Muted)))
            { e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(border, path); }
            var swatch = new Rectangle(10, 9, 18, 18);
            if (Value.A == 0)
            {
                using (var pen = new Pen(Ui.Muted, 1.5f)) { e.Graphics.DrawRectangle(pen, swatch); e.Graphics.DrawLine(pen, swatch.Left, swatch.Bottom, swatch.Right, swatch.Top); }
            }
            else
            {
                using (var brush = new SolidBrush(Value)) e.Graphics.FillRectangle(brush, swatch);
                using (var pen = new Pen(Ui.Muted)) e.Graphics.DrawRectangle(pen, swatch);
            }
            string text = Value.A == 0 ? SettingColorText() : "#" + Value.R.ToString("X2") + Value.G.ToString("X2") + Value.B.ToString("X2");
            using (var font = Ui.Font(8.5f))
                TextRenderer.DrawText(e.Graphics, text, font, new Rectangle(36, 1, Width - 42, Height - 2), Ui.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        private static string SettingColorText() => Localize.English ? "Transparent" : "透明";
    }
}
