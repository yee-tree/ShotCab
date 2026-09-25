using ShotCab.Core;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ShotCab.App
{
    internal static class SmokeTests
    {
        internal static int ClipboardRoundTrip(string output)
        {
            Directory.CreateDirectory(output); var previous = Clipboard.GetDataObject();
            try
            {
                using (var app = new CabinetContext(Path.Combine(output,"data"),false))
                using (var bitmap = new Bitmap(40,30))
                {
                    using (var g = Graphics.FromImage(bitmap)) g.Clear(Color.CornflowerBlue);
                    for (int i = 0; i < 8; i++)
                    {
                        app.Clipboard.Copy(bitmap, "clipboard-test");
                        Application.DoEvents();
                        using (var result = Clipboard.GetImage()) using (var read = new Bitmap(result))
                            if (read.Size != bitmap.Size || read.GetPixel(10,10).ToArgb() != bitmap.GetPixel(10,10).ToArgb()) throw new Exception("System clipboard image differs");
                        if (!Clipboard.ContainsData(DataFormats.Dib) || !Clipboard.ContainsData("PNG") || !app.Clipboard.OwnsClipboard) throw new Exception("Clipboard format/state missing");
                    }
                }
                File.WriteAllText(Path.Combine(output,"clipboard-results.txt"), "PASS 8 actual system clipboard image round trips, DIB/PNG formats and ownership state"); return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output,"clipboard-results.txt"),ex.ToString()); return 1; }
            finally { if (previous != null) Clipboard.SetDataObject(previous,true,10,100); }
        }
        public static int Performance(string output)
        {
            Directory.CreateDirectory(output);
            var report = new System.Text.StringBuilder();
            try
            {
                using (var context = new CabinetContext(Path.Combine(output, "data"), false))
                using (var bitmap = new Bitmap(1920, 1080))
                {
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.Clear(Color.White);
                        using (var font = Ui.Font(30))
                            for (int y = 0; y < 1080; y += 60) g.DrawString("ShotCab 图柜 1000 records — annotation and history", font, Brushes.Navy, 30, y);
                    }
                    byte[] png = Images.Png(bitmap);
                    for (int i = context.Store.Count(new HistoryQuery()); i < 1000; i++) context.Store.Add(png, png, "{\"Version\":1,\"Shapes\":[]}", false, false, false, bitmap.Width, bitmap.Height);
                    var samples = new System.Collections.Generic.List<double>();
                    for (int i = 0; i < 100; i++) { var sw = Stopwatch.StartNew(); var items = context.Store.Query(new HistoryQuery { Limit = 50, ExcludeHidden = true }); if (items.Count != 50) throw new Exception("History load was not 50 records"); samples.Add(sw.Elapsed.TotalMilliseconds); }
                    samples.Sort(); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    report.AppendLine("Synthetic dataset: 1000 records, each 1920x1080 PNG, history tab 50 records.");
                    report.AppendLine("Query50 P95 ms: " + samples[94].ToString("F2"));
                    using (var sidebar = new SidebarForm(context))
                    using (var timer = new Timer { Interval = 10000 })
                    using (var process = Process.GetCurrentProcess())
                    {
                        sidebar.StartPosition = FormStartPosition.Manual; sidebar.Location = new Point(-10000, -10000); sidebar.Size = new Size(220, 1000); sidebar.SelectHistoryTab();
                        TimeSpan cpu = TimeSpan.Zero; var elapsed = new Stopwatch();
                        sidebar.Shown += (s, e) => { process.Refresh(); cpu = process.TotalProcessorTime; elapsed.Start(); timer.Start(); };
                        timer.Tick += (s, e) => { timer.Stop(); process.Refresh(); report.AppendLine("Sidebar idle private bytes: " + process.PrivateMemorySize64); report.AppendLine("10-second idle CPU, machine-normalized %: " + ((process.TotalProcessorTime - cpu).TotalMilliseconds / elapsed.Elapsed.TotalMilliseconds / Environment.ProcessorCount * 100).ToString("F3")); sidebar.Close(); };
                        Application.Run(sidebar);
                    }
                    report.AppendLine("OS: " + Environment.OSVersion + "; logical CPUs: " + Environment.ProcessorCount);
                    report.AppendLine("Displays: " + string.Join("; ", Screen.AllScreens.Select(s => s.Bounds.ToString())));
                    report.AppendLine("Scope: off-screen rendered sidebar; excludes capture-mask latency, real user screenshots, editor/pin/OCR peaks, other operating systems and display configurations.");
                }
                File.WriteAllText(Path.Combine(output, "performance.txt"), report.ToString()); return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output, "performance.txt"), report + ex.ToString()); return 1; }
        }
        public static int Run(string output)
        {
            Directory.CreateDirectory(output); var log = Path.Combine(output, "smoke-results.txt");
            try
            {
                using (var context = new CabinetContext(Path.Combine(output, "data"), false))
                using (var image = new Bitmap(720, 400))
                {
                    InteractionTests.Run(context);
                    using (var about = new AboutForm()) Render(about, Path.Combine(output, "about.png"));
                    using (var emptyHost = new Form { Size = new Size(180, 330), BackColor = Ui.Background, FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) })
                    using (var empty = new PhotoStrip(context) { Dock = DockStyle.Fill })
                    using (var preview = new Bitmap(180, 330))
                    {
                        emptyHost.Controls.Add(empty); emptyHost.Show(); Application.DoEvents();
                        empty.SetItems(new System.Collections.Generic.List<HistoryItem>(), false);
                        emptyHost.DrawToBitmap(preview, new Rectangle(Point.Empty, preview.Size));
                        preview.Save(Path.Combine(output, "recent-empty.png"));
                        empty.SetItems(new System.Collections.Generic.List<HistoryItem>(), true);
                        emptyHost.DrawToBitmap(preview, new Rectangle(Point.Empty, preview.Size));
                        preview.Save(Path.Combine(output, "history-empty.png"));
                    }
                    using (var g = Graphics.FromImage(image))
                    {
                        g.Clear(Color.FromArgb(243, 246, 253));
                        using (var title = Ui.Font(30, FontStyle.Bold)) g.DrawString("ShotCab · 图柜", title, Brushes.MidnightBlue, 36, 34);
                        using (var body = Ui.Font(18)) { g.DrawString("截图、标注与随手可取的历史", body, Brushes.SteelBlue, 38, 116); g.DrawString("Screenshot Cabinet 2026", body, Brushes.DarkSlateGray, 38, 170); }
                        g.FillRectangle(Brushes.CornflowerBlue, 38, 252, 260, 75);
                    }
                    image.Save(Path.Combine(output, "ocr-input.png"));
                    var bytes = Images.Png(image); var item = context.Store.Add(bytes, bytes, "{\"Version\":1,\"Shapes\":[]}", false, false, false, 720, 400); context.Store.SetTags(item.Id, new[] { "工作", "参考" }); context.Store.SetFavorite(item.Id, true);
                    using (var result = new CaptureResultBar(context, item, image))
                    {
                        if (result.Controls.OfType<FlowLayoutPanel>().Single().Controls.Count != 4) throw new Exception("Capture result actions missing");
                        Render(result, Path.Combine(output, "capture-result-bar.png"));
                    }
                    using (var modalEditor = new ShareX.ScreenCaptureLib.RegionCaptureForm(ShareX.ScreenCaptureLib.RegionCaptureMode.Editor, new ShareX.ScreenCaptureLib.RegionCaptureOptions(), new Bitmap(image)))
                    {
                        bool shown=false, titleReady=false;
                        if (modalEditor.TransparencyKey != Color.Empty) throw new Exception("Editor still uses a transparent surround");
                        modalEditor.ShotCabToolbarPreparing += () => modalEditor.ShotCabOrganizeTools();
                        modalEditor.Shown += (s,e) =>
                        {
                            shown = true;
                            var caption = modalEditor.Controls.Find("ShotCabEditorCaption", true).OfType<Label>().Single();
                            titleReady = caption.Text.Contains("编辑图片");
                            modalEditor.BeginInvoke((Action)(() => modalEditor.Close()));
                        };
                        modalEditor.ShowDialog();
                        if (!shown) throw new Exception("Editor did not finish its first modal show");
                        if (!titleReady) throw new Exception("Editor title was incomplete on its first show");
                    }
                    using (var card = new PinForm(context, image, item.Id, true)) Render(card, Path.Combine(output, "preview-card.png"));
                    using (var host = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) })
                    {
                        host.Show(); var menu = context.ItemMenu(item); menu.Show(host, new Point(0,0));
                        using (var rendered = new Bitmap(menu.Width, menu.Height)) { menu.DrawToBitmap(rendered, new Rectangle(Point.Empty, rendered.Size)); rendered.Save(Path.Combine(output, "picture-menu.png")); }
                        menu.Close(); host.Hide();
                    }
                    using (var sidebar = new SidebarForm(context))
                    {
                        sidebar.Size = new Size(250, 750); sidebar.RefreshItems();
                        Render(sidebar, Path.Combine(output, "sidebar.png"));
                        int originalWidth=context.Settings.SidebarWidth;
                        context.Settings.SidebarWidth=180; sidebar.ApplySettings();
                        Render(sidebar, Path.Combine(output,"sidebar-narrow.png"));
                        sidebar.ShowBasicSettings(true); Render(sidebar,Path.Combine(output,"sidebar-settings-narrow.png"));
                        sidebar.SelectBasicCategory(2); Render(sidebar,Path.Combine(output,"sidebar-appearance-narrow.png"));
                        context.Settings.SidebarWidth=originalWidth; sidebar.ApplySettings();
                        sidebar.ShowBasicSettings(true); Render(sidebar, Path.Combine(output, "sidebar-settings.png"));
                        var settingsControls = (FlowLayoutPanel)typeof(SidebarForm).GetField("basicSettings",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(sidebar);
                        if (settingsControls.Controls.Cast<Control>().Any(x => x.Text.Contains("详细设置")))
                            throw new Exception("The sidebar still sends users to a separate detailed-settings window");
                        foreach (string key in new[] { "Setting_JpegQualityLabel", "Setting_RecycleDaysLabel", "Setting_SidebarReserveSpace",
                            "Setting_CapacityWarningGBLabel", "Setting_OcrExecutablePath", "Setting_AutoOcrOnEdit", "Setting_AnnotationHeading" })
                            if (settingsControls.Controls[key] == null) throw new Exception("Detailed setting was not migrated: " + key);
                        foreach (string key in new[] { "Setting_EditAsNew", "Setting_SidebarReserveSpace" })
                            if (!(settingsControls.Controls[key] is SoftToggle explained) || string.IsNullOrWhiteSpace(explained.HelpText))
                                throw new Exception("Setting lacks its hover explanation: " + key);
                        sidebar.SelectBasicCategory(2); Render(sidebar, Path.Combine(output,"sidebar-appearance.png"));
                        var annotationHeading = settingsControls.Controls["Setting_AnnotationHeading"] as Label;
                        if (annotationHeading == null || annotationHeading.Text != "默认标注样式")
                            throw new Exception("Annotation defaults are still English in the Chinese sidebar");
                        sidebar.Show();
                        var expandAnnotation = settingsControls.Controls["Setting_AnnotationToggle"];
                        typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            .Invoke(expandAnnotation, new object[] { EventArgs.Empty });
                        var annotationPanel = (FlowLayoutPanel)settingsControls.Controls["Setting_AnnotationDefaults"];
                        if (!annotationPanel.Visible || !annotationPanel.Controls.OfType<Label>().Any(x => x.Text == "边框 / 线条颜色") ||
                            annotationPanel.Controls.Count < 45)
                            throw new Exception("Expanded annotation defaults are incomplete or not localized: visible=" +
                                annotationPanel.Visible + ", count=" + annotationPanel.Controls.Count + ", labels=" +
                                string.Join("|", annotationPanel.Controls.OfType<Label>().Take(8).Select(x => x.Text)));
                        if (settingsControls.DisplayRectangle.Height <= settingsControls.ClientSize.Height)
                            throw new Exception("Long sidebar settings cannot be scrolled");
                        int beforeWheel = settingsControls.AutoScrollPosition.Y;
                        typeof(Control).GetMethod("OnMouseWheel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            .Invoke(settingsControls, new object[] { new MouseEventArgs(MouseButtons.None, 0, 12, 360, -120) });
                        if (settingsControls.AutoScrollPosition.Y >= beforeWheel)
                            throw new Exception("Mouse wheel did not scroll the expanded settings");
                        settingsControls.AutoScrollPosition = Point.Empty;
                        var shadowToggle = annotationPanel.Controls.OfType<SoftToggle>().Single(x => x.Text == "阴影");
                        bool originalShadow = context.AnnotationDefaults.Shadow;
                        typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            .Invoke(shadowToggle, new object[] { EventArgs.Empty });
                        if (context.AnnotationDefaults.Shadow == originalShadow)
                            throw new Exception("Migrated annotation default did not change immediately");
                        var savedDefaults = Newtonsoft.Json.JsonConvert.DeserializeObject<ShareX.ScreenCaptureLib.RegionCaptureOptions>(
                            File.ReadAllText(Path.Combine(context.Store.RootPath, "annotation-options.json")));
                        if (savedDefaults.AnnotationOptions.Shadow == originalShadow)
                            throw new Exception("Migrated annotation default was not persisted");
                        typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            .Invoke(shadowToggle, new object[] { EventArgs.Empty });
                        Render(sidebar, Path.Combine(output, "sidebar-annotation-settings.png"));
                        sidebar.Show(); settingsControls.AutoScrollPosition = new Point(0, 100000);
                        if (settingsControls.AutoScrollPosition.Y >= 0)
                            throw new Exception("Cannot reach the end of long annotation settings");
                        Render(sidebar, Path.Combine(output, "sidebar-annotation-bottom.png"));
                        settingsControls.AutoScrollPosition = Point.Empty;
                        typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            .Invoke(expandAnnotation, new object[] { EventArgs.Empty });
                        sidebar.SelectBasicCategory(3); Render(sidebar, Path.Combine(output,"sidebar-general.png"));
                        sidebar.SelectBasicCategory(4); Render(sidebar, Path.Combine(output,"sidebar-storage-settings.png"));
                        sidebar.SelectBasicCategory(5); Render(sidebar, Path.Combine(output,"sidebar-ocr-settings.png"));
                        sidebar.SelectHistoryTab(); Render(sidebar,Path.Combine(output,"sidebar-dates.png"));
                        context.Settings.Language="en"; Localize.SetLanguage("en");
                        try { using(var englishSidebar=new SidebarForm(context)) { englishSidebar.ApplySettings(); englishSidebar.ShowBasicSettings(true); englishSidebar.SelectBasicCategory(3); Render(englishSidebar,Path.Combine(output,"sidebar-english.png")); englishSidebar.SelectBasicCategory(2); Render(englishSidebar,Path.Combine(output,"sidebar-english-appearance.png")); } }
                        finally { context.Settings.Language="zh-CN"; Localize.SetLanguage("zh-CN"); }
                    }
                    using (var form = new HistoryForm(context))
                    {
                        form.Reload(); Render(form, Path.Combine(output, "history.png"));
                        form.Size = form.MinimumSize; Render(form, Path.Combine(output, "history-compact.png"));
                    }
                    context.Settings.Language="en"; Localize.SetLanguage("en");
                    try { using (var form = new HistoryForm(context)) { form.Reload(); Render(form, Path.Combine(output, "history-english.png")); } }
                    finally { context.Settings.Language="zh-CN"; Localize.SetLanguage("zh-CN"); }
                    using (var editor = new ShareX.ScreenCaptureLib.RegionCaptureForm(ShareX.ScreenCaptureLib.RegionCaptureMode.Editor, new ShareX.ScreenCaptureLib.RegionCaptureOptions(), new Bitmap(image)))
                    {
                        editor.Options.ShowMagnifier = editor.Options.ShowInfo = false;
                        editor.ShotCabToolbarPreparing += () =>
                        {
                            var manager = editor.GetType().GetProperty("ShapeManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(editor);
                            var menu = (Form)manager.GetType().GetField("menuForm", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(manager);
                            if (menu == null || menu.Visible) throw new Exception("Toolbar styling did not run before first display");
                            editor.ShotCabOrganizeTools(); editor.ShotCabAddCommand("完成并复制", () => { }); editor.ShotCabAddCommand("鼠标放大预览：开/关", () => { });
                            editor.ShotCabAddCommand("识别文字", editor.ShotCabShowOcrPanel);
                        };
                        editor.RestoreShotCabDocument("{\"Version\":1,\"Shapes\":[{\"Type\":\"EffectSolidMask\",\"Properties\":{\"Rectangle\":{\"X\":40,\"Y\":250,\"Width\":120,\"Height\":35}}},{\"Type\":\"EffectSpotlight\",\"Properties\":{\"Rectangle\":{\"X\":20,\"Y\":10,\"Width\":650,\"Height\":215},\"Darkness\":120}}]}");
                        Render(editor, Path.Combine(output, "editor-effects.png"));
                        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
                        var manager = editor.GetType().GetProperty("ShapeManager", flags).GetValue(editor);
                        var toolbar = (ToolStrip)manager.GetType().GetField("tsMain", flags).GetValue(manager);
                        var toolbarWindow = (Form)manager.GetType().GetField("menuForm", flags).GetValue(manager);
                        var more = toolbar.Items.OfType<ToolStripButton>().Single(x => x.Name == "ShotCabMore");
                        var ocr = toolbar.Items.OfType<ToolStripButton>().Single(x => x.Name == "ShotCabOcr");
                        if (!ocr.Visible || string.IsNullOrWhiteSpace(ocr.ToolTipText)) throw new Exception("Direct OCR button is missing its hover description");
                        using (var actualIcon = editor.Icon.ToBitmap())
                        using (var expectedIcon = Ui.AppIcon.ToBitmap())
                        {
                            if (actualIcon.Size != expectedIcon.Size) throw new Exception("Editor does not use ShotCab's application icon");
                            for (int y = 0; y < actualIcon.Height; y++) for (int x = 0; x < actualIcon.Width; x++)
                                if (actualIcon.GetPixel(x, y).ToArgb() != expectedIcon.GetPixel(x, y).ToArgb())
                                    throw new Exception("Editor still uses the upstream application icon");
                        }
                        if (toolbarWindow.Width != editor.ClientSize.Width || toolbarWindow.Left != editor.Left) throw new Exception("Bottom toolbar is not aligned to editor width");
                        var visible = toolbar.Items.OfType<ToolStripItem>().Where(x => x.Visible).ToArray();
                        var selectTool = visible.Single(x => x.Name == ShareX.ScreenCaptureLib.ShapeType.ToolSelect.ToString());
                        if (!selectTool.ToolTipText.StartsWith("选择")) throw new Exception("Chinese editor displayed an English select-tool hint: " + selectTool.ToolTipText);
                        if (visible.Length < 14 || visible.Length > 19) throw new Exception("Editor toolbar lost common actions: " + visible.Length);
                        foreach (var moved in new[] { ShareX.ScreenCaptureLib.ShapeType.DrawingLine, ShareX.ScreenCaptureLib.ShapeType.DrawingFreehandArrow, ShareX.ScreenCaptureLib.ShapeType.DrawingTextBackground })
                            if (!visible.Any(x => x.Name == moved.ToString())) throw new Exception("Common toolbar is missing " + moved);
                        foreach (var pill in visible.OfType<ToolStripButton>().Where(x => x.Image != null))
                            if (pill.DisplayStyle != ToolStripItemDisplayStyle.Image || string.IsNullOrWhiteSpace(pill.ToolTipText)) throw new Exception("Toolbar button should be icon-only with a hover description: " + pill.Name);
                        foreach (var tool in visible.OfType<ToolStripButton>().Where(x => x.Tag is ShareX.ScreenCaptureLib.ShapeType))
                            using (var icon = new Bitmap(tool.Image))
                                for (int y = 0; y < icon.Height; y++) for (int x = 0; x < icon.Width; x++)
                                { var color = icon.GetPixel(x, y); if (color.A > 0 && Math.Max(color.R, Math.Max(color.G, color.B)) - Math.Min(color.R, Math.Min(color.G, color.B)) > 15) throw new Exception("A colorful legacy icon remains in the editor toolbar: " + tool.Name); }
                        var rows = visible.OfType<ToolStripButton>().Select(x => x.Bounds.Top).Distinct().Count();
                        if (rows != 1) throw new Exception("Collapsed editor toolbar should have one row, saw " + rows + ": " + string.Join("; ", visible.Select(x => x.Name + "=" + x.Bounds)));
                        if (visible.OfType<ToolStripButton>().All(x => x.Name != "ShotCabPrimary")) throw new Exception("Done and copy is not in the bottom toolbar");
                        if (!(editor.FormBorderStyle == FormBorderStyle.None) || editor.Controls.OfType<Panel>().All(x => x.Dock != DockStyle.Top)) throw new Exception("Editor is not borderless or has no caption bar");
                        using (var rendered = new Bitmap(toolbar.Width, toolbar.Height)) { toolbar.DrawToBitmap(rendered, new Rectangle(Point.Empty, rendered.Size)); rendered.Save(Path.Combine(output, "editor-toolbar.png")); }
                        int collapsedHeight = toolbarWindow.Height;
                        more.PerformClick(); Application.DoEvents();
                        var secondRow = (ToolStrip)manager.GetType().GetField("shotCabSecondary", flags).GetValue(manager);
                        var curvedArrow = toolbar.Items.Cast<ToolStripItem>().Single(x => x.Name == ShareX.ScreenCaptureLib.ShapeType.DrawingFreehandArrow.ToString());
                        if (!curvedArrow.ToolTipText.StartsWith("曲线箭头")) throw new Exception("Chinese editor displayed an English drawing-tool hint: " + curvedArrow.ToolTipText);
                        if (secondRow.Items.Cast<ToolStripItem>().All(x => x.Name != "ShotCabCombine")) throw new Exception("Editor has no dedicated combine-image action");
                        var colorAction = secondRow.Items.Cast<ToolStripItem>().Single(x => x.Name == "ShotCabColor");
                        colorAction.PerformClick();
                        var stylePalette = (Form)manager.GetType().GetField("shotCabStylePalette", flags).GetValue(manager);
                        if (stylePalette == null || !stylePalette.Visible) throw new Exception("Annotation color button did not open a style palette");
                        var strokeChoices = stylePalette.Controls.Cast<Control>().Where(x => x.Tag as string == "stroke").ToArray();
                        var swatchProperty = strokeChoices[0].GetType().GetProperty("Swatch", flags);
                        if (strokeChoices.Select(x => ((Color?)swatchProperty.GetValue(x)).Value.ToArgb()).Distinct().Count() != strokeChoices.Length)
                            throw new Exception("Stroke palette contains duplicate colors");
                        using (var paletteImage = new Bitmap(stylePalette.Width, stylePalette.Height))
                        { stylePalette.DrawToBitmap(paletteImage, new Rectangle(Point.Empty, paletteImage.Size)); paletteImage.Save(Path.Combine(output, "editor-style-palette.png")); }
                        var customWidth = stylePalette.Controls.Cast<Control>().Single(x => x.AccessibleName?.StartsWith("自定义线条粗细") == true);
                        customWidth.Controls.OfType<TextBox>().Single().Text = "13";
                        var annotationOptions = manager.GetType().GetProperty("AnnotationOptions", flags).GetValue(manager);
                        if ((int)annotationOptions.GetType().GetProperty("BorderSize", flags).GetValue(annotationOptions) != 13)
                            throw new Exception("Custom stroke width was not applied immediately");
                        var blueStroke = stylePalette.Controls.Cast<Control>().Single(x => x.Tag as string == "stroke" && x.Left == 54);
                        typeof(Control).GetMethod("OnClick", flags).Invoke(blueStroke, new object[] { EventArgs.Empty });
                        var borderColor = (Color)annotationOptions.GetType().GetProperty("BorderColor", flags).GetValue(annotationOptions);
                        if (borderColor.ToArgb() != Color.FromArgb(53, 111, 220).ToArgb())
                            throw new Exception("Annotation color palette did not apply a selected color immediately");
                        if (stylePalette.Controls.Cast<Control>().Any(x => x.AccessibleName == "应用绘图样式"))
                            throw new Exception("The drawing style palette still requires an Apply button");
                        var ellipseTool = toolbar.Items.Cast<ToolStripItem>().Single(x => x.Name == ShareX.ScreenCaptureLib.ShapeType.DrawingEllipse.ToString() && x.Visible);
                        var ellipseHandlers = (System.ComponentModel.EventHandlerList)typeof(System.ComponentModel.Component)
                            .GetProperty("Events", flags).GetValue(ellipseTool);
                        var eventKey = typeof(ToolStripItem).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                            .Single(x => x.Name.IndexOf("mousedown", StringComparison.OrdinalIgnoreCase) >= 0 && x.FieldType == typeof(object)).GetValue(null);
                        (ellipseHandlers[eventKey] as MouseEventHandler)?.Invoke(ellipseTool, new MouseEventArgs(MouseButtons.Left, 1, 15, 15, 0));
                        if (stylePalette.Visible) throw new Exception("Choosing another drawing tool left the previous style palette open");
                        ellipseTool.PerformClick();
                        var rectangleTool = toolbar.Items.Cast<ToolStripItem>().Single(x => x.Name == ShareX.ScreenCaptureLib.ShapeType.DrawingRectangle.ToString() && x.Visible);
                        var handlers = (System.ComponentModel.EventHandlerList)typeof(System.ComponentModel.Component)
                            .GetProperty("Events", flags).GetValue(rectangleTool);
                        var mouseDownField = typeof(ToolStripItem).GetFields(
                            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                            .FirstOrDefault(x => x.Name.IndexOf("mousedown", StringComparison.OrdinalIgnoreCase) >= 0 && x.FieldType == typeof(object));
                        if (mouseDownField == null) throw new Exception("Could not inspect drawing-tool mouse event binding");
                        var mouseDownKey = mouseDownField.GetValue(null);
                        var onMouseDown = handlers[mouseDownKey] as MouseEventHandler;
                        if (onMouseDown == null) throw new Exception("Drawing tool has no right-click style handler");
                        onMouseDown(rectangleTool, new MouseEventArgs(MouseButtons.Right, 1, 15, 15, 0));
                        stylePalette = (Form)manager.GetType().GetField("shotCabStylePalette", flags).GetValue(manager);
                        if (stylePalette == null || !stylePalette.Visible ||
                            !stylePalette.Controls.OfType<Label>().Any(x => x.Text.StartsWith("矩形")))
                            throw new Exception("Right-clicking a drawing tool did not open its style palette: " +
                                (stylePalette == null ? "null" : "visible=" + stylePalette.Visible +
                                ", labels=" + string.Join("|", stylePalette.Controls.OfType<Label>().Select(x => x.Text))));
                        stylePalette.Close();
                        if (!secondRow.Visible || toolbarWindow.Height <= collapsedHeight) throw new Exception("More did not expand a second toolbar row");
                        if (secondRow.Items.OfType<ToolStripButton>().Count(x => x.Tag is ShareX.ScreenCaptureLib.ShapeType) < 8) throw new Exception("Expanded toolbar is missing annotation tools");
                        foreach (var popup in secondRow.Items.OfType<ToolStripDropDownButton>())
                            if (popup.DropDown is ToolStripDropDownMenu menu && menu.ShowImageMargin) throw new Exception("A dropdown still has the white image-margin stripe: " + popup.Name);
                        using (var rendered = new Bitmap(toolbarWindow.Width, toolbarWindow.Height)) { toolbarWindow.DrawToBitmap(rendered, new Rectangle(Point.Empty, rendered.Size)); rendered.Save(Path.Combine(output, "editor-toolbar-expanded.png")); }
                        more.PerformClick(); Application.DoEvents();
                        if (secondRow.Visible || toolbarWindow.Height != collapsedHeight) throw new Exception("More did not collapse the second toolbar row");
                        editor.Size = new Size(830, 620); Application.DoEvents(); toolbar.PerformLayout(); Application.DoEvents();
                        if (toolbarWindow.Width != editor.ClientSize.Width || toolbarWindow.Left != editor.Left) throw new Exception("Bottom toolbar stopped following editor resize");
                        rows = toolbar.Items.OfType<ToolStripButton>().Where(x => x.Visible).Select(x => x.Bounds.Top).Distinct().Count();
                        if (rows > 1) throw new Exception("Narrow editor wraps the collapsed toolbar into " + rows + " rows");
                        using (var rendered = new Bitmap(toolbar.Width, toolbar.Height)) { toolbar.DrawToBitmap(rendered, new Rectangle(Point.Empty, rendered.Size)); rendered.Save(Path.Combine(output, "editor-toolbar-narrow.png")); }
                        editor.ShotCabOrganizeTools(false);
                        Render(editor, Path.Combine(output, "editor-light.png"));
                        // The tool palette is its own top-level window, so a form-only render never
                        // shows it. Compose what the user actually sees on the editor surface.
                        editor.ShotCabOrganizeTools(true);
                        editor.Show(); Application.DoEvents(); editor.Refresh(); Application.DoEvents();
                        toolbar.PerformLayout(); Application.DoEvents();
                        using (var composed = new Bitmap(editor.Width, editor.Height))
                        {
                            editor.DrawToBitmap(composed, new Rectangle(Point.Empty, composed.Size));
                            using (var g = Graphics.FromImage(composed))
                            using (var toolbarImage = new Bitmap(toolbar.Width, toolbar.Height))
                            {
                                toolbar.DrawToBitmap(toolbarImage, new Rectangle(Point.Empty, toolbarImage.Size));
                                g.DrawImageUnscaled(toolbarImage, toolbarWindow.Left - editor.Left, toolbarWindow.Top - editor.Top);
                            }
                            composed.Save(Path.Combine(output, "editor-window.png"));
                        }
                        var shapesProperty = manager.GetType().GetProperty("Shapes", flags) ?? throw new Exception("Editor shapes property missing");
                        var visualShapes = (System.Collections.IList)shapesProperty.GetValue(manager);
                        if (visualShapes == null || visualShapes.Count == 0) throw new Exception("Editor preview has no sample shapes");
                        var toolProperty = manager.GetType().GetProperty("CurrentTool", flags) ?? throw new Exception("Editor tool property missing");
                        var shapeProperty = manager.GetType().GetProperty("CurrentShape", flags) ?? throw new Exception("Editor selected-shape property missing");
                        var styleMethod = manager.GetType().GetMethod("ShotCabApplyEffectStyle", flags) ?? throw new Exception("Editor style method missing");
                        var zoomProperty = editor.GetType().GetProperty("ZoomFactor", flags);
                        float zoomBeforeOcr = (float)zoomProperty.GetValue(editor);
                        ocr.PerformClick(); Application.DoEvents();
                        var ocrPanel = (Control)editor.GetType().GetField("shotCabOcrPanel", flags).GetValue(editor);
                        var ocrText = (TextBox)editor.GetType().GetField("shotCabOcrText", flags).GetValue(editor);
                        if (!ocrPanel.Visible || ocrPanel.Parent != editor || ocrText.Parent == null)
                            throw new Exception("OCR did not open inside the editor");
                        ocr.PerformClick(); Application.DoEvents();
                        editor.ShotCabSetOcrResult("识别结果", "已识别 1 行");
                        if (ocrText.Text != "识别结果") throw new Exception("OCR result did not reach the in-editor text panel");
                        var ocrCount = ocrPanel.Controls.Find("ShotCabOcrCount", true).OfType<Label>().SingleOrDefault();
                        if (ocrCount == null) throw new Exception("OCR text count is missing from the panel header");
                        ocrText.Text = "ab c\n你";
                        if (!ocrCount.Text.Contains("4")) throw new Exception("OCR text count did not update after editing");
                        var attachOcr = editor.GetType().GetMethod("ShotCabAttachOcrOverlay", flags);
                        if (attachOcr == null) throw new Exception("Editor image cannot host selectable OCR text");
                        var selectionImage = new Bitmap(image);
                        var selection = new OcrCanvas(selectionImage);
                        selectionImage.Dispose();
                        selection.SetDocument(new OcrDocument { Text = "ABC", Lines = new System.Collections.Generic.List<OcrLine>
                        {
                            new OcrLine { Text = "ABC", Points = new[] { new Point(10, 20), new Point(45, 20), new Point(45, 50), new Point(10, 50) },
                                Chars = new System.Collections.Generic.List<OcrCharacter>
                                {
                                    new OcrCharacter { Text = "A", Points = new[] { new Point(10, 20), new Point(20, 20), new Point(20, 50), new Point(10, 50) } },
                                    new OcrCharacter { Text = "B", Points = new[] { new Point(21, 20), new Point(31, 20), new Point(31, 50), new Point(21, 50) } },
                                    new OcrCharacter { Text = "C", Points = new[] { new Point(32, 20), new Point(44, 20), new Point(44, 50), new Point(32, 50) } }
                                } }
                        } });
                        attachOcr.Invoke(editor, new object[] { selection });
                        if (selection.Parent != editor || selection.Width <= 0 || selection.Height <= 0)
                            throw new Exception("Selectable OCR image was not aligned inside the editor");
                        var mouseDown = typeof(Control).GetMethod("OnMouseDown", flags);
                        var mouseMove = typeof(Control).GetMethod("OnMouseMove", flags);
                        var mouseUp = typeof(Control).GetMethod("OnMouseUp", flags);
                        int imageY = Math.Max(1, 30 * selection.Height / image.Height);
                        mouseDown.Invoke(selection, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 15 * selection.Width / image.Width, imageY, 0) });
                        mouseMove.Invoke(selection, new object[] { new MouseEventArgs(MouseButtons.Left, 0, 39 * selection.Width / image.Width, imageY, 0) });
                        mouseUp.Invoke(selection, new object[] { new MouseEventArgs(MouseButtons.Left, 0, 39 * selection.Width / image.Width, imageY, 0) });
                        if (selection.SelectedText != "ABC") throw new Exception("Image text drag selection failed: " + selection.SelectedText);
                        string keyboardCopy = null;
                        selection.CopyRequested += value => keyboardCopy = value;
                        var command = typeof(OcrCanvas).GetMethod("ProcessCmdKey", flags);
                        command.Invoke(selection, new object[] { Message.Create(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero), Keys.Control | Keys.C });
                        if (keyboardCopy != "ABC") throw new Exception("Ctrl+C did not copy the selected image text");
                        string panelCopy = null;
                        editor.ShotCabOcrCopyRequested += value => panelCopy = value;
                        ocrText.Select(0, 2);
                        editor.ShotCabSetOcrSelectedText(selection.SelectedText);
                        var copyButton = ocrPanel.Controls.Find("ShotCabOcrCopy", true).SingleOrDefault();
                        if (copyButton == null) throw new Exception("OCR panel copy action is missing");
                        typeof(Control).GetMethod("OnClick", flags).Invoke(copyButton, new object[] { EventArgs.Empty });
                        if (panelCopy != "ABC") throw new Exception("OCR panel did not copy the selected image text");
                        Render(editor, Path.Combine(output, "editor-ocr.png"));
                        editor.Show(); Application.DoEvents();
                        editor.ShotCabCloseOcrPanel();
                        if (Math.Abs((float)zoomProperty.GetValue(editor) - zoomBeforeOcr) > .001f)
                            throw new Exception("Repeating OCR lost the editor's original zoom");
                        toolProperty.SetValue(manager, ShareX.ScreenCaptureLib.ShapeType.EffectSolidMask);
                        shapeProperty.SetValue(manager, visualShapes[0]);
                        styleMethod.Invoke(manager, new object[] { Color.FromArgb(50, 64, 80), 120 });
                        var toggleHistory = editor.GetType().GetMethod("ShotCabToggleHistoryPanel", flags);
                        toggleHistory.Invoke(editor, null);
                        using (var firstFrame = new Bitmap(editor.Width, editor.Height))
                        {
                            editor.DrawToBitmap(firstFrame, new Rectangle(Point.Empty, firstFrame.Size));
                            var right = new Rectangle(editor.Width - 220, 70, 190, 150);
                            int bright = 0;
                            for (int y = right.Top; y < right.Bottom; y += 2)
                                for (int x = right.Left; x < right.Right; x += 2)
                                {
                                    var color = firstFrame.GetPixel(x, y);
                                    if (color.R > 220 && color.G > 220 && color.B > 220) bright++;
                                }
                            if (bright > right.Width * right.Height / 100)
                                throw new Exception("History first paint still contains a bright native-control strip");
                            firstFrame.Save(Path.Combine(output, "editor-history-first-frame.png"));
                        }
                        Application.DoEvents(); editor.Refresh(); Application.DoEvents();
                        var caption = editor.Controls.Find("ShotCabEditorCaption", true).OfType<Label>().FirstOrDefault();
                        if (caption != null && !caption.Text.Contains("编辑图片")) throw new Exception("Editor caption became incomplete after opening history");
                        using (var composed = new Bitmap(editor.Width, editor.Height))
                        {
                            editor.DrawToBitmap(composed, new Rectangle(Point.Empty, composed.Size));
                            using (var g = Graphics.FromImage(composed))
                            using (var toolbarImage = new Bitmap(toolbar.Width, toolbar.Height))
                            {
                                toolbar.DrawToBitmap(toolbarImage, new Rectangle(Point.Empty, toolbarImage.Size));
                                g.DrawImageUnscaled(toolbarImage, toolbarWindow.Left - editor.Left, toolbarWindow.Top - editor.Top);
                            }
                            composed.Save(Path.Combine(output, "editor-history.png"));
                        }
                        var historyPanel = (Control)editor.GetType().GetField("shotCabHistoryPanel", flags).GetValue(editor);
                        var historyRows = (FlowLayoutPanel)editor.GetType().GetField("shotCabHistoryRows", flags).GetValue(editor);
                        if (!historyPanel.Visible || historyRows.Controls.Count != 2) throw new Exception("Expanded editor history did not show the original and edited steps: visible=" + historyPanel.Visible + ", rows=" + historyRows.Controls.Count);
                        var onClick = typeof(Control).GetMethod("OnClick", flags);
                        onClick.Invoke(historyRows.Controls[0], new object[] { EventArgs.Empty });
                        if ((int)manager.GetType().GetProperty("ShotCabHistoryPosition", flags).GetValue(manager) != 0) throw new Exception("History row did not jump back to the original step");
                        onClick.Invoke(historyRows.Controls[1], new object[] { EventArgs.Empty });
                        if ((int)manager.GetType().GetProperty("ShotCabHistoryPosition", flags).GetValue(manager) != 1) throw new Exception("History row did not jump forward to the edited step");
                        ocr.PerformClick(); Application.DoEvents();
                        var historyButton = toolbar.Items.OfType<ToolStripButton>().Single(x => x.Name == "ShotCabHistory");
                        if (historyPanel.Visible || historyButton.Checked || !ocrPanel.Visible)
                            throw new Exception("Opening OCR did not close editor history and update its toolbar state");
                        editor.ShotCabCloseOcrPanel();
                    }
                    File.WriteAllText(log, "PASS startup, SQLite history, labels, favorite, sidebar/history/editor render, OCR scaled and reverse cross-line selection, record use counting.\nPrivate bytes: " + Process.GetCurrentProcess().PrivateMemorySize64 + "\nOS: " + Environment.OSVersion + "\n" + string.Join("\n", Screen.AllScreens.Select(s => s.Bounds.ToString())));
                }
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(log, ex.ToString()); return 1; }
        }
        private static void Render(Form form, string path)
        {
            form.StartPosition = FormStartPosition.Manual;
            if (!(form is SidebarForm)) form.Location = new Point(-10000, -10000);
            form.Show(); Application.DoEvents(); form.PerformLayout(); form.Refresh(); Application.DoEvents();
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                var colors = new System.Collections.Generic.HashSet<int>();
                for (int y = 0; y < bitmap.Height; y += 4)
                    for (int x = 0; x < bitmap.Width; x += 4) colors.Add(bitmap.GetPixel(x, y).ToArgb());
                if (colors.Count < 20) throw new InvalidOperationException("Rendered window is blank: " + path);
                bitmap.Save(path);
            }
            form.Hide();
        }
    }
}
