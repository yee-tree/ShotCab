// ShotCab editor palette. Upstream GPL license applies.
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ShareX.ScreenCaptureLib
{
    internal partial class ShapeManager
    {
        internal Bitmap ShotCabEffectSnapshot { get; set; }
        private const int ShotCabMainHeight = 54;
        private const int ShotCabExtraHeight = 48;
        private ToolStrip shotCabSecondary;
        private ToolStripButton shotCabMore, shotCabUndo, shotCabRedo, shotCabDone, shotCabHistory, shotCabOcr, shotCabCombine;
        private ToolStripDropDownButton shotCabUtilities;
        private bool shotCabExpanded;
        private ToolTip shotCabToolHints;
        private ToolStripItem shotCabHoveredItem;
        private string shotCabHoverDescription;

        internal int ShotCabHistoryCount => history.ShotCabStepCount;
        internal int ShotCabHistoryPosition => history.ShotCabCurrentStep;
        internal int ShotCabHistoryVersion => history.ShotCabVersion;
        internal string ShotCabHistoryTitle(int step) => history.ShotCabStepTitle(step);
        internal void ShotCabJumpToHistory(int step)
        {
            history.ShotCabJumpTo(step);
            ShotCabRefreshHistoryButtons();
        }

        internal void ShotCabReplaceCanvas(Bitmap image)
        {
            history.CreateCanvasMemento();
            DeleteAllShapes();
            UpdateMenu();
            UpdateCanvas(image);
            ShotCabRefreshHistoryButtons();
            Form.Invalidate();
        }

        internal void ShotCabOrganizeTools(bool dark = true)
        {
            if (tsMain == null || shotCabMore != null) return;
            bool english = System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "en";
            var renderer = new ShotCabSoftRenderer(dark);
            var ink = renderer.Foreground;
            tsMain.SuspendLayout();
            menuForm.SuspendLayout();

            // Preserve the upstream commands in a themed file/options menu. The underlying
            // shape buttons remain alive for ShareX's own state updates, but are not painted.
            var originals = tsMain.Items.OfType<ToolStripButton>().Where(x => x.Tag is ShapeType).ToDictionary(x => (ShapeType)x.Tag);
            var files = new ToolStripDropDownButton { Name = "ShotCabFiles", Image = ShotCabMonoIcons.Action("files", ink), ToolTipText = english ? "Files and advanced options" : "文件与高级选项", DisplayStyle = ToolStripItemDisplayStyle.Image, ShowDropDownArrow = false };
            foreach (var item in tsMain.Items.Cast<ToolStripItem>().ToArray())
            {
                if (item.Tag is ShapeType) { item.Visible = false; continue; }
                tsMain.Items.Remove(item);
                if (item is ToolStripButton legacy && item != tsbBorderColor) legacy.DisplayStyle = ToolStripItemDisplayStyle.Text;
                files.DropDownItems.Add(item);
            }
            files.DropDownItems.Remove(tsddbShapeOptions);
            tsddbShapeOptions.Text = string.Empty;
            tsddbShapeOptions.Image = ShotCabMonoIcons.Action("style", ink);
            tsddbShapeOptions.DisplayStyle = ToolStripItemDisplayStyle.Image;
            tsddbShapeOptions.ShowDropDownArrow = false;
            tsddbShapeOptions.ToolTipText = english ? "Selected tool style" : "当前工具样式";

            tsMain.Renderer = renderer;
            tsMain.BackColor = renderer.Surface;
            tsMain.AutoSize = false;
            tsMain.Dock = DockStyle.Top;
            tsMain.Height = ShotCabMainHeight;
            tsMain.GripStyle = ToolStripGripStyle.Hidden;
            tsMain.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
            tsMain.CanOverflow = false;
            tsMain.ImageScalingSize = new Size(22, 22);
            tsMain.Padding = new Padding(11, 5, 11, 5);
            tsMain.ShowItemToolTips = true;

            var common = new[]
            {
                ShapeType.ToolSelect, ShapeType.DrawingRectangle, ShapeType.DrawingEllipse,
                ShapeType.DrawingArrow, ShapeType.DrawingLine, ShapeType.DrawingFreehandArrow,
                ShapeType.DrawingTextOutline, ShapeType.DrawingTextBackground,
                ShapeType.DrawingFreehand, ShapeType.EffectPixelate, ShapeType.ToolCrop
            };
            foreach (var type in common) tsMain.Items.Add(ShotCabTool(type, ShotCabToolLabel(type, english), ink, 38));
            tsMain.Items.Add(new ToolStripSeparator());
            shotCabMore = ShotCabAction("ShotCabMore", english ? "More tools" : "更多工具", "more", ink, 43);
            shotCabMore.Click += (sender, args) => { ShotCabCloseStylePalette(); ShotCabToggleExtra(); };
            tsMain.Items.Add(shotCabMore);

            shotCabDone = ShotCabAction("ShotCabPrimary", english ? "Done and copy" : "完成并复制", "done", renderer.Accent, 44);
            shotCabDone.Enabled = false;
            shotCabDone.Alignment = ToolStripItemAlignment.Right;
            tsMain.Items.Add(shotCabDone);
            var cancel = ShotCabAction("ShotCabCancel", english ? "Close editor" : "关闭编辑器", "close", ink, 42);
            cancel.Alignment = ToolStripItemAlignment.Right;
            cancel.Click += (sender, args) => Form.Close();
            tsMain.Items.Add(cancel);
            shotCabRedo = ShotCabAction("ShotCabRedo", english ? "Redo" : "重做", "redo", ink, 42);
            shotCabRedo.Alignment = ToolStripItemAlignment.Right;
            shotCabRedo.Click += (sender, args) => tsmiRedo.PerformClick();
            tsMain.Items.Add(shotCabRedo);
            shotCabUndo = ShotCabAction("ShotCabUndo", english ? "Undo" : "撤销", "undo", ink, 42);
            shotCabUndo.Alignment = ToolStripItemAlignment.Right;
            shotCabUndo.Click += (sender, args) => tsmiUndo.PerformClick();
            tsMain.Items.Add(shotCabUndo);
            shotCabHistory = ShotCabAction("ShotCabHistory", english ? "Edit history" : "编辑历史", "history", ink, 42);
            shotCabHistory.Alignment = ToolStripItemAlignment.Right;
            shotCabHistory.Click += (sender, args) => { Form.ShotCabToggleHistoryPanel(); shotCabHistory.Checked = Form.ShotCabHistoryVisible; };
            tsMain.Items.Add(shotCabHistory);
            shotCabOcr = ShotCabAction("ShotCabOcr", english ? "Recognize text" : "识别文字", "ocr", ink, 42);
            shotCabOcr.Alignment = ToolStripItemAlignment.Right;
            tsMain.Items.Add(shotCabOcr);

            shotCabSecondary = new ToolStrip
            {
                Name = "ShotCabExpandedTools", AutoSize = false, Dock = DockStyle.Bottom,
                Height = ShotCabExtraHeight, LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow,
                CanOverflow = false, GripStyle = ToolStripGripStyle.Hidden, ShowItemToolTips = true,
                ImageScalingSize = new Size(22, 22), Padding = new Padding(11, 4, 11, 4),
                BackColor = renderer.Surface, Renderer = renderer, Visible = false
            };
            var secondary = new[]
            {
                ShapeType.DrawingSpeechBalloon, ShapeType.DrawingStep, ShapeType.EffectBlur,
                ShapeType.EffectSolidMask, ShapeType.EffectHighlight, ShapeType.EffectSpotlight,
                ShapeType.DrawingMagnify, ShapeType.ToolCutOut
            };
            foreach (var type in secondary) shotCabSecondary.Items.Add(ShotCabTool(type, ShotCabToolLabel(type, english), ink, 35));

            shotCabCombine = ShotCabAction("ShotCabCombine", english ? "Combine with another image" : "与另一张图片拼接", "combine", ink, 38);
            shotCabSecondary.Items.Add(shotCabCombine);

            var otherTools = ShotCabDropdown("ShotCabOtherTools", english ? "Other annotation tools" : "其他标注工具", "more", ink, 36);
            foreach (var type in originals.Keys.Except(common).Except(secondary))
            {
                var chosen = type;
                var choice = new ToolStripMenuItem(ShotCabToolLabel(type, english)) { ForeColor = ink, Tag = type };
                if (type == ShapeType.DrawingSmartEraser)
                    choice.ToolTipText = english ? "Covers a rectangle with the color sampled at its starting point; best on flat backgrounds" : "用起点像素颜色覆盖矩形，适合纯色背景；不是内容识别修复";
                choice.Click += (sender, args) => { ShotCabCloseStylePalette(); CurrentTool = chosen; ShotCabSyncQuickSelection(); };
                otherTools.DropDownItems.Add(choice);
            }
            ShotCabThemeDropdown(otherTools, renderer);
            shotCabSecondary.Items.Add(otherTools);

            var color = ShotCabAction("ShotCabColor", english ? "Annotation color and stroke" : "标注颜色与线条", "color", ink, 38);
            color.Click += (sender, args) => ShotCabOpenStylePalette(ShotCabCurrentStyleTool(), color);
            shotCabSecondary.Items.Add(color);
            ShotCabThemeDropdown(tsddbShapeOptions, renderer);
            ShotCabSetButtonSize(tsddbShapeOptions, 38);
            shotCabSecondary.Items.Add(tsddbShapeOptions);
            ShotCabThemeDropdown(files, renderer);
            ShotCabSetButtonSize(files, 38);
            shotCabSecondary.Items.Add(files);
            shotCabUtilities = ShotCabDropdown("ShotCabUtilities", english ? "More actions" : "更多操作", "actions", ink, 38);
            ShotCabThemeDropdown(shotCabUtilities, renderer);
            shotCabSecondary.Items.Add(shotCabUtilities);

            menuForm.AutoSize = false;
            menuForm.BackColor = renderer.Surface;
            menuForm.Controls.Add(shotCabSecondary);
            menuForm.ClientSize = new Size(Math.Max(1, Form.ClientSize.Width), ShotCabMainHeight);
            shotCabToolHints = new ToolTip { AutoPopDelay = 4500, InitialDelay = 150, ReshowDelay = 100 };
            ShotCabWireToolHints(tsMain);
            ShotCabWireToolHints(shotCabSecondary);
            menuForm.FormClosed += (sender, args) => shotCabToolHints.Dispose();
            tsMain.ResumeLayout(true);
            menuForm.ResumeLayout(true);
            ShotCabRefreshHistoryButtons();
            ShotCabSyncQuickSelection();
        }

        private ToolStripButton ShotCabTool(ShapeType type, string description, Color ink, int width)
        {
            if (ShotCabStyleSupported(type)) description += System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "en"
                ? " · right-click for style" : " · 右键调整样式";
            var button = new ToolStripButton
            {
                Name = type.ToString(), Tag = type, Image = ShotCabMonoIcons.Tool(type, ink),
                DisplayStyle = ToolStripItemDisplayStyle.Image, ToolTipText = description,
                AccessibleName = description, Checked = type == CurrentTool
            };
            ShotCabSetButtonSize(button, width);
            button.Click += (sender, args) => { Form.ShotCabCloseOcrPanel(); CurrentTool = type; ShotCabSyncQuickSelection(); };
            button.MouseDown += (sender, args) =>
            {
                if (args.Button == MouseButtons.Left) ShotCabCloseStylePalette();
                else if (args.Button == MouseButtons.Right && ShotCabStyleSupported(type))
                {
                    Form.ShotCabCloseOcrPanel();
                    CurrentTool = type;
                    ShotCabSyncQuickSelection();
                    ShotCabOpenStylePalette(type, button);
                }
            };
            return button;
        }

        private static string ShotCabToolLabel(ShapeType type, bool english)
        {
            switch (type)
            {
                case ShapeType.RegionRectangle: return english ? "Rectangle region" : "矩形选区";
                case ShapeType.RegionEllipse: return english ? "Ellipse region" : "椭圆选区";
                case ShapeType.RegionFreehand: return english ? "Freehand region" : "自由形状选区";
                case ShapeType.ToolSelect: return english ? "Select" : "选择";
                case ShapeType.DrawingRectangle: return english ? "Rectangle" : "矩形";
                case ShapeType.DrawingEllipse: return english ? "Ellipse" : "椭圆";
                case ShapeType.DrawingFreehand: return english ? "Brush" : "画笔";
                case ShapeType.DrawingFreehandArrow: return english ? "Freehand arrow" : "曲线箭头";
                case ShapeType.DrawingLine: return english ? "Line" : "直线";
                case ShapeType.DrawingArrow: return english ? "Arrow" : "箭头";
                case ShapeType.DrawingTextOutline: return english ? "Outlined text" : "描边文字";
                case ShapeType.DrawingTextBackground: return english ? "Text box" : "文字框";
                case ShapeType.DrawingSpeechBalloon: return english ? "Speech balloon" : "气泡框";
                case ShapeType.DrawingStep: return english ? "Numbered marker" : "序号";
                case ShapeType.DrawingMagnify: return english ? "Magnify" : "局部放大";
                case ShapeType.DrawingImage: return english ? "Insert image" : "插入图片";
                case ShapeType.DrawingImageScreen: return english ? "Insert screen image" : "屏幕图片";
                case ShapeType.DrawingSticker: return english ? "Sticker" : "贴纸";
                case ShapeType.DrawingCursor: return english ? "Cursor marker" : "光标标记";
                case ShapeType.DrawingSmartEraser: return english ? "Sample cover" : "取样覆盖";
                case ShapeType.EffectBlur: return english ? "Blur" : "模糊";
                case ShapeType.EffectPixelate: return english ? "Pixelate" : "马赛克";
                case ShapeType.EffectHighlight: return english ? "Highlight" : "荧光笔";
                case ShapeType.EffectSolidMask: return english ? "Solid mask" : "实心遮挡";
                case ShapeType.EffectSpotlight: return english ? "Spotlight" : "聚光灯";
                case ShapeType.ToolCrop: return english ? "Crop" : "裁剪";
                case ShapeType.ToolCutOut: return english ? "Canvas gap" : "留白";
                default: return type.ToString();
            }
        }

        private static ToolStripButton ShotCabAction(string name, string tooltip, string icon, Color ink, int width)
        {
            var button = new ToolStripButton
            {
                Name = name, Image = ShotCabMonoIcons.Action(icon, ink),
                DisplayStyle = ToolStripItemDisplayStyle.Image, ToolTipText = tooltip,
                AccessibleName = tooltip
            };
            ShotCabSetButtonSize(button, width);
            return button;
        }

        private static ToolStripDropDownButton ShotCabDropdown(string name, string tooltip, string icon, Color ink, int width)
        {
            var button = new ToolStripDropDownButton
            {
                Name = name, Image = ShotCabMonoIcons.Action(icon, ink),
                DisplayStyle = ToolStripItemDisplayStyle.Image, ShowDropDownArrow = false,
                ToolTipText = tooltip, AccessibleName = tooltip
            };
            ShotCabSetButtonSize(button, width);
            return button;
        }

        private static void ShotCabSetButtonSize(ToolStripItem item, int width)
        {
            item.AutoSize = false;
            item.Size = new Size(width, 39);
            item.Margin = new Padding(2, 2, 2, 2);
            item.Padding = Padding.Empty;
        }

        private void ShotCabWireToolHints(ToolStrip strip)
        {
            // WinForms' native ToolStrip hints are unreliable on this separately owned,
            // borderless palette. Show the active item's description explicitly.
            strip.ShowItemToolTips = false;
            strip.MouseMove += (sender, args) => ShotCabShowToolHint(strip, strip.GetItemAt(args.Location));
            strip.MouseLeave += (sender, args) => ShotCabShowToolHint(strip, null);
            foreach (ToolStripItem item in strip.Items)
            {
                ToolStripItem capture = item;
                capture.MouseEnter += (sender, args) => ShotCabShowToolHint(strip, capture);
                capture.MouseLeave += (sender, args) => ShotCabShowToolHint(strip, null);
            }
        }

        private void ShotCabShowToolHint(ToolStrip strip, ToolStripItem item)
        {
            if (shotCabHoveredItem == item) return;
            shotCabHoveredItem = item;
            shotCabToolHints.Hide(strip);
            string message = item?.ToolTipText;
            shotCabHoverDescription = message ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(message) && item.Available &&
                Screen.AllScreens.Any(screen => screen.Bounds.IntersectsWith(strip.RectangleToScreen(strip.ClientRectangle))))
                shotCabToolHints.Show(message, strip, Math.Max(4, item.Bounds.Left), -30, 4500);
        }

        private static void ShotCabThemeDropdown(ToolStripDropDownItem item, ShotCabSoftRenderer renderer)
        {
            item.DropDown.Renderer = renderer;
            item.DropDown.BackColor = renderer.Surface;
            item.DropDown.ForeColor = renderer.Foreground;
            if (item.DropDown is ToolStripDropDownMenu menu) { menu.ShowImageMargin = false; menu.ShowCheckMargin = false; }
            foreach (ToolStripItem child in item.DropDownItems)
            {
                child.ForeColor = renderer.Foreground;
                if (child is ToolStripDropDownItem nested) ShotCabThemeDropdown(nested, renderer);
            }
        }

        private void ShotCabToggleExtra()
        {
            if (shotCabSecondary == null) return;
            shotCabExpanded = !shotCabExpanded;
            shotCabSecondary.Visible = shotCabExpanded;
            shotCabMore.Checked = shotCabExpanded;
            ShotCabResizeToolbar(Form.ClientSize.Width);
            Form.CenterCanvas();
            Form.Invalidate();
        }

        internal void ShotCabResizeToolbar(int width)
        {
            if (shotCabMore == null || menuForm == null || menuForm.IsDisposed) return;
            menuForm.ClientSize = new Size(Math.Max(1, width), ShotCabMainHeight + (shotCabExpanded ? ShotCabExtraHeight : 0));
            Form.ToolbarHeight = menuForm.Height + Form.ShotCabCaptionHeight + 6;
            UpdateMenuPosition();
            Form.ShotCabUpdateHistoryBounds();
            Form.ShotCabUpdateOcrBounds();
        }

        private void ShotCabRefreshHistoryButtons()
        {
            if (shotCabUndo == null) return;
            shotCabUndo.Enabled = tsmiUndo.Enabled;
            shotCabRedo.Enabled = tsmiRedo.Enabled;
            Form.ShotCabRefreshHistoryPanel();
        }

        private void ShotCabSyncQuickSelection()
        {
            if (tsMain == null || shotCabMore == null) return;
            foreach (var strip in new[] { tsMain, shotCabSecondary })
            {
                if (strip == null) continue;
                foreach (var button in strip.Items.OfType<ToolStripButton>().Where(x => x.Tag is ShapeType))
                    button.Checked = (ShapeType)button.Tag == CurrentTool;
            }
        }

        internal void ShotCabAddCommand(string text, Action action)
        {
            if (shotCabMore == null) return;
            if (text == "图片拼接" || text == "Combine images")
            {
                shotCabCombine.Click += (sender, args) => action();
                return;
            }
            if (text == "识别文字" || text == "Recognize text")
            {
                shotCabOcr.Click += (sender, args) => action();
                return;
            }
            if (text == "完成并复制" || text == "Done & copy")
            {
                shotCabDone.Enabled = true;
                shotCabDone.ToolTipText = text;
                shotCabDone.Click += (sender, args) => action();
                return;
            }
            var command = new ToolStripMenuItem(text) { ToolTipText = text };
            command.Click += (sender, args) => action();
            shotCabUtilities.DropDownItems.Add(command);
            ShotCabThemeDropdown(shotCabUtilities, (ShotCabSoftRenderer)tsMain.Renderer);
        }

        internal void ShotCabSetHistoryButtonState(bool visible)
        {
            if (shotCabHistory != null) shotCabHistory.Checked = visible;
        }

        internal void ShotCabEffectStyleDialog()
        {
            var mask = CurrentShape as SolidMaskEffectShape;
            var spotlight = CurrentShape as SpotlightEffectShape;
            var color = mask?.MaskColor ?? AnnotationOptions.SolidMaskColor;
            using (var dialog = new Form { Text = "遮挡与聚光灯样式", ClientSize = new Size(380, 175), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false })
            {
                var choose = new Button { Text = "实心遮挡颜色…", Left = 16, Top = 16, Width = 175, Height = 32, BackColor = color };
                choose.Click += (sender, args) => { using (var picker = new ColorDialog { Color = color, FullOpen = true }) if (picker.ShowDialog(dialog) == DialogResult.OK) { color = picker.Color; choose.BackColor = color; } };
                var label = new Label { Text = "聚光灯外部暗度（0–255）", Left = 16, Top = 65, AutoSize = true };
                var darkness = new NumericUpDown { Minimum = 0, Maximum = 255, Value = Math.Max(0, Math.Min(255, spotlight?.Darkness ?? AnnotationOptions.SpotlightDarkness)), Left = 235, Top = 61, Width = 100 };
                var apply = new Button { Text = "应用并记忆", Left = 235, Top = 115, Width = 125, Height = 32, DialogResult = DialogResult.OK };
                dialog.Controls.AddRange(new Control[] { choose, label, darkness, apply }); dialog.AcceptButton = apply;
                if (dialog.ShowDialog(Form) != DialogResult.OK) return;
                ShotCabApplyEffectStyle(color, (int)darkness.Value);
            }
        }

        internal void ShotCabApplyEffectStyle(Color color, int darkness)
        {
            AnnotationOptions.SolidMaskColor = Color.FromArgb(255, color.R, color.G, color.B);
            AnnotationOptions.SpotlightDarkness = Math.Max(0, Math.Min(255, darkness));
            if (CurrentShape is SolidMaskEffectShape || CurrentShape is SpotlightEffectShape)
            {
                history.CreateShapesMemento();
                CurrentShape.OnConfigLoad(); CurrentShape.OnMoved(); OnImageModified();
            }
            Form.Invalidate();
        }
    }
}
