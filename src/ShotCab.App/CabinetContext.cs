using ShotCab.Core;
using ShareX.ScreenCaptureLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using CancellationTokenSource = System.Threading.CancellationTokenSource;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShotCab.App
{
    internal sealed class CabinetContext : ApplicationContext
    {
        public HistoryStore Store { get; private set; }
        public AppSettings Settings { get; private set; }
        public ClipboardService Clipboard { get; private set; }
        public readonly HashSet<string> BusyIds = new HashSet<string>();
        private readonly Dictionary<string, int> recordUsers = new Dictionary<string, int>();
        internal void AcquireRecord(string id)
        {
            if (id == null) return;
            recordUsers.TryGetValue(id, out int count); recordUsers[id] = count + 1; BusyIds.Add(id);
        }
        internal void ReleaseRecord(string id)
        {
            if (id == null || !recordUsers.TryGetValue(id, out int count)) return;
            if (count > 1) recordUsers[id] = count - 1;
            else { recordUsers.Remove(id); BusyIds.Remove(id); }
        }
        private readonly List<PinForm> pins = new List<PinForm>();
        private readonly HotkeyWindow events;
        private readonly NotifyIcon tray;
        private readonly bool interactive;
        private CaptureResultBar resultBar;
        private readonly Timer maintenance = new Timer { Interval = 60000 };
        private SettingsStore settingsStore;
        private SidebarForm sidebar;
        private HistoryForm history;
        private readonly ContextMenuStrip itemMenu = new ContextMenuStrip();
        private DateTime lastCleanup = DateTime.MinValue, lastWarning = DateTime.MinValue;
        private readonly string pointerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data-location.txt");
        private Rectangle lastRegion;
        private RegionCaptureOptions captureOptions;
        private Bitmap lastImage;
        private Form[] scrollingHiddenForms = new Form[0];
        public bool IsCapturing { get; private set; }
        public bool ManuallyHidden { get; private set; }
        public bool SidebarManuallyHidden { get; private set; }
        public event Action Changed;
        public CabinetContext(string dataRoot = null, bool interactive = true)
        {
            this.interactive = interactive;
            var root = dataRoot ?? (File.Exists(pointerPath) ? File.ReadAllText(pointerPath).Trim() : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ShotCab.Data"));
            if (dataRoot == null && File.Exists(pointerPath) && !Directory.Exists(root)) throw new IOException("历史目录暂时不可用，请恢复连接后重试：" + root);
            Store = new HistoryStore(root); settingsStore = new SettingsStore(root); Settings = settingsStore.Load();
            Localize.SetLanguage(Settings.Language);
            if (Settings.SidebarWidth < AppSettings.SidebarMinimumWidth) Settings.SidebarWidth = AppSettings.SidebarMinimumWidth;
            Directory.CreateDirectory(Path.Combine(root, "items"));
            Clipboard = new ClipboardService(root);
            var optionsPath = Path.Combine(root, "annotation-options.json");
            captureOptions = File.Exists(optionsPath) ? JsonConvert.DeserializeObject<RegionCaptureOptions>(File.ReadAllText(optionsPath)) : new RegionCaptureOptions();
            captureOptions.InputDelay = 0; captureOptions.EnableAnimations = false; captureOptions.FPSLimit = 60; captureOptions.ShowEditorPanTip = false;
            events = new HotkeyWindow(); events.ClipboardChanged += () => sidebar?.RefreshClipboard();
            tray = new NotifyIcon { Text = "ShotCab · 图柜", Icon = Ui.AppIcon, Visible = interactive };
            var menu = new ContextMenuStrip();
            Add(menu, "截图", () => Capture("region")); Add(menu, "全屏截图", () => Capture("full")); Add(menu, "当前窗口", () => Capture("window"));
            var monitors = new ToolStripMenuItem("指定显示器");
            for (int i = 0; i < Screen.AllScreens.Length; i++) { int index = i; monitors.DropDownItems.Add("显示器 " + (i + 1), null, (s, e) => Capture("monitor:" + index)); } menu.Items.Add(monitors);
            Add(menu, "自由形状截图", () => Capture("free")); Add(menu, "延时 3 秒截图", () => Capture("delay"));
            var lastRegionAction = new ToolStripMenuItem(Localize.T("重截上次区域")) { ToolTipText = Localize.T("按上次的位置截取当前屏幕；仅在本次运行中保留。") };
            lastRegionAction.Click += (sender, args) => Capture("last"); menu.Items.Add(lastRegionAction);
            Add(menu, "滚动长截图", () => SafeAsync(async () => await ScrollingCaptureForm.StartStopScrollingCapture(new ScrollingCaptureOptions { AutoUpload = false }, image => { using (image) Edit(new Bitmap(image)); }, captureState: ScrollingCaptureState)));
            Add(menu, "取色器", () => Safe(() => RegionCaptureTasks.ShowScreenColorPickerDialog(captureOptions)));
            menu.Items.Add(new ToolStripSeparator());
            Add(menu, "导入图片", ImportFile); Add(menu, "导入剪贴板图片", ImportClipboard); Add(menu, "图片拼接", () => CombineImages());
            Add(menu, "从剪贴板贴图", PinClipboard); Add(menu, "识别截图文字", () => Capture("ocr"));
            menu.Items.Add(new ToolStripSeparator()); Add(menu, "完整历史 / 存储管理", ShowHistory); Add(menu, "显示 / 隐藏侧栏", ToggleSidebar); Add(menu, "隐藏 / 恢复所有浮窗", ToggleAll); Add(menu, "设置", ShowSettings);
            Add(menu, "关于 ShotCab", ShowAbout);
            Add(menu, "退出", ExitThread); tray.ContextMenuStrip = menu; tray.DoubleClick += (s, e) => ShowSidebar();
            Localize.Apply(menu.Items);
            sidebar = new SidebarForm(this);
            Changed += () => { sidebar?.RefreshItems(); history?.Reload(); };
            if (interactive) { Program.Trace("Applying settings"); ApplySettings(); Program.Trace("Settings applied"); maintenance.Tick += (s, e) => Maintain(); maintenance.Start(); Maintain(); Program.Trace("Startup maintenance complete"); }
        }
        private static void Add(ContextMenuStrip menu, string text, Action action) => menu.Items.Add(Localize.T(text), null, (s, e) => action());
        private void ShowAbout()
        {
            using (var about = new AboutForm()) about.ShowDialog();
        }
        internal void ChangeLanguage(string language)
        {
            Settings.Language=language; SaveBasicPreferences(); Localize.SetLanguage(language);
            foreach(Form form in Application.OpenForms) Localize.Apply(form);
            Localize.Apply(tray.ContextMenuStrip.Items); sidebar.RefreshItems();
        }
        internal bool ChangeShortcut(string property, string shortcut)
        {
            var member=typeof(AppSettings).GetProperty(property); string old=(string)member.GetValue(Settings);
            string[] names={"CaptureShortcut","PinShortcut","SidebarShortcut","OcrShortcut"};
            if(names.Where(x=>x!=property).Any(x=>string.Equals((string)typeof(AppSettings).GetProperty(x).GetValue(Settings),shortcut,StringComparison.OrdinalIgnoreCase))) return false;
            member.SetValue(Settings,shortcut);
            string error=ConfigureHotkeys();
            if(error.Length>0) { member.SetValue(Settings,old); ConfigureHotkeys(); return false; }
            SaveBasicPreferences(); return true;
        }
        private string ConfigureHotkeys() => events.Configure(new[] { new KeyValuePair<string, Action>(Settings.CaptureShortcut, () => Capture("region")), new KeyValuePair<string, Action>(Settings.PinShortcut, PinClipboard), new KeyValuePair<string, Action>(Settings.SidebarShortcut, ToggleSidebar), new KeyValuePair<string, Action>(Settings.OcrShortcut, () => Capture("ocr")) });
        public void Safe(Action action) { try { action(); } catch (Exception ex) { Ui.Error(ex); } }
        public async void SafeAsync(Func<Task> action) { try { await action(); } catch (OperationCanceledException) { } catch (Exception ex) { Ui.Error(ex); } }
        public void Notify(string text) { tray.BalloonTipTitle = "ShotCab · 图柜"; tray.BalloonTipText = text; tray.ShowBalloonTip(3000); }
        public void Refresh() => Changed?.Invoke();
        public void ApplySettings()
        {
            ValidateSettings(Settings); settingsStore.Save(Settings);
            bool themeChanged = Ui.Dark != Settings.DarkTheme;
            int? activeSettings=sidebar?.ActiveSettingsCategory;
            Ui.Dark = Settings.DarkTheme;
            ShareX.HelpersLib.ShareXResources.Theme = Ui.Dark ? ShareX.HelpersLib.ShareXTheme.DarkTheme : ShareX.HelpersLib.ShareXTheme.LightTheme;
            var conflicts = ConfigureHotkeys();
            if (conflicts.Length > 0) Notify("快捷键被占用或无效：" + conflicts + "。请在设置中更改。");
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                if (Settings.StartWithWindows) key.SetValue("ShotCab", "\"" + Application.ExecutablePath + "\""); else key.DeleteValue("ShotCab", false);
            }
            if (themeChanged) { sidebar.Dispose(); sidebar = new SidebarForm(this); history?.Close(); }
            Program.Trace("Showing sidebar"); sidebar.ApplySettings(); Program.Trace("Sidebar shown"); Refresh();
            if(themeChanged && activeSettings.HasValue) { sidebar.ShowBasicSettings(true); sidebar.SelectBasicCategory(activeSettings.Value); }
        }
        public void UpdateSettings(AppSettings value) { ValidateSettings(value); Settings = value; ApplySettings(); }
        internal void SaveBasicPreferences() => settingsStore.Save(Settings);
        internal AnnotationOptions AnnotationDefaults => captureOptions.AnnotationOptions;
        internal void SaveAnnotationDefaults() => File.WriteAllText(Path.Combine(Store.RootPath, "annotation-options.json"), JsonConvert.SerializeObject(captureOptions));
        internal bool ShouldHideForCapture(Form form) => form.Visible && (!(form is SidebarForm) || Settings.HideSidebarDuringCapture);
        private static void ValidateSettings(AppSettings s)
        {
            if (s.RetentionDays < 0 || s.RecycleDays < 0 || s.RecentCount < 1 || s.RecentCount > 1000 || s.SidebarWidth < AppSettings.SidebarMinimumWidth || s.SidebarWidth > AppSettings.SidebarMaximumWidth || s.CapacityWarningGB < 0 || s.OcrPreviewSeconds < 0 || s.OcrPreviewSeconds > 120 || s.FullscreenMode < 0 || s.FullscreenMode > 2 || s.JpegQuality < 1 || s.JpegQuality > 100) throw new ArgumentException("设置超出允许范围，请检查天数、侧栏宽度(180–640)、全屏模式(0–2)和JPEG质量(1–100)。");
            s.ExportFormat = s.ExportFormat.ToLowerInvariant(); if (s.ExportFormat != "png" && s.ExportFormat != "jpg") throw new ArgumentException("导出格式只能为 png 或 jpg。");
        }
        public void ShowSettings() => Safe(() =>
        {
            if (ManuallyHidden) ToggleAll();
            if (!Settings.SidebarEnabled) { Settings.SidebarEnabled = true; SaveBasicPreferences(); }
            SidebarManuallyHidden = false;
            sidebar.ShowBasicSettings(true);
            sidebar.ApplySettings();
            sidebar.Activate();
        });
        public void ShowHistory() => Safe(() => { if (history == null || history.IsDisposed) { history = new HistoryForm(this); history.FormClosed += (s, e) => history = null; } history.Show(); history.Activate(); });
        public void ShowSidebar() => Safe(() =>
        {
            if (ManuallyHidden) ToggleAll();
            if (!Settings.SidebarEnabled) { Settings.SidebarEnabled = true; SaveBasicPreferences(); }
            SidebarManuallyHidden = false;
            sidebar.ApplySettings();
            sidebar.BringToFront();
        });
        public void ToggleSidebar() { if (sidebar.ActiveSettingsCategory.HasValue) { sidebar.ShowBasicSettings(false); SidebarManuallyHidden = true; sidebar.RemoveReservation(); sidebar.Hide(); return; } if (!Settings.SidebarEnabled) { Settings.SidebarEnabled = true; SidebarManuallyHidden = false; ApplySettings(); return; } SidebarManuallyHidden = !SidebarManuallyHidden; if (SidebarManuallyHidden) { sidebar.RemoveReservation(); sidebar.Hide(); } else sidebar.ApplySettings(); }
        public void ToggleAll() { ManuallyHidden = !ManuallyHidden; if (ManuallyHidden) { sidebar.RemoveReservation(); sidebar.Hide(); foreach (var p in pins) p.Hide(); } else { sidebar.ApplySettings(); foreach (var p in pins) { p.SetClickThrough(false); p.Show(); } } }
        public void SetPinsFullscreenHidden(bool hidden) { foreach (var p in pins) { if (hidden) p.Hide(); else if (!IsCapturing && !ManuallyHidden) p.Show(); } }
        private void ScrollingCaptureState(bool capturing)
        {
            if (capturing)
            {
                IsCapturing = true;
                scrollingHiddenForms = Application.OpenForms.Cast<Form>().Where(f => ShouldHideForCapture(f) && !(f is ScrollingCaptureForm)).ToArray();
                foreach (var form in scrollingHiddenForms) form.Hide();
            }
            else
            {
                foreach (var form in scrollingHiddenForms) if (!form.IsDisposed) form.Show();
                scrollingHiddenForms = new Form[0]; IsCapturing = false;
            }
        }
        public async void Capture(string mode)
        {
            if (IsCapturing) return; IsCapturing = true;
            resultBar?.Close();
            var activeWindow = Native.GetForegroundWindow(); var previouslyVisible = Application.OpenForms.Cast<Form>().Where(ShouldHideForCapture).ToArray();
            try
            {
                foreach (var f in previouslyVisible) f.Hide();
                await Task.Delay(mode == "delay" ? 3000 : 50);
                Bitmap image = null; string document = null;
                if (mode == "full") image = new Screenshot().CaptureFullscreen();
                else if (mode == "window") image = new Screenshot().CaptureWindow(activeWindow);
                else if (mode.StartsWith("monitor:")) image = new Screenshot().CaptureRectangle(Screen.AllScreens[int.Parse(mode.Substring(8))].Bounds);
                else if (mode == "last")
                {
                    if (lastRegion.IsEmpty) { Notify("还没有上次区域，请先进行一次区域截图。"); return; }
                    image = new Screenshot().CaptureRectangle(lastRegion);
                }
                else
                {
                    captureOptions.ShowMagnifier = captureOptions.ShowInfo = Settings.EditorCursorPreview;
                    captureOptions.QuickCrop = true;
                    var regionTool = mode == "free" ? ShapeType.RegionFreehand : ShapeType.RegionRectangle;
                    captureOptions.LastRegionTool = regionTool;
                    using (var capture = new RegionCaptureForm(RegionCaptureMode.Default, captureOptions))
                    {
                        capture.ShotCabSelectRegionTool(regionTool);
                        capture.ShowDialog(); image = capture.ShotCabCaptureBase(out document);
                        if (image != null && (mode == "region" || mode == "free" || mode == "delay")) lastRegion = capture.GetSelectedRectangle();
                    }
                }
                foreach (var f in previouslyVisible) if (!f.IsDisposed) f.Show();
                if (image != null)
                {
                    if (mode == "ocr") { using (image) new OcrForm(this, image, null).Show(); }
                    else ProcessCapturedImage(image, document);
                }
            }
            catch (Exception ex) { Ui.Error(ex); }
            finally { foreach (var f in previouslyVisible) if (!f.IsDisposed) f.Show(); IsCapturing = false; }
        }
        internal void ProcessCapturedImage(Bitmap image, string document, Action<Bitmap,HistoryItem,string> editor = null)
        {
            if (!Settings.EditAfterCapture)
            {
                var saved = CompleteCapture(image, document);
                if (interactive && Settings.ShowCaptureResult) ShowCaptureResult(saved);
                return;
            }
            try
            {
                var saved = CompleteCapture(new Bitmap(image), document);
                (editor ?? Edit)(image, saved, document);
                image = null; // Edit owns the bitmap once it begins.
            }
            finally { image?.Dispose(); }
        }
        internal HistoryItem CompleteCapture(Bitmap image, string document, bool copyToClipboard = true)
        {
            using (image)
            using (var renderer = new RegionCaptureForm(RegionCaptureMode.Editor, captureOptions, new Bitmap(image)))
            {
                if (!string.IsNullOrWhiteSpace(document)) renderer.RestoreShotCabDocument(document);
                using (var rendered = renderer.GetResultImage())
                {
                    var saved = Save(image, rendered, renderer.ExportShotCabDocument(), null, false, renderer.ShotCabHasAnnotations, renderer.ShotCabHasRedactions, false);
                    if (copyToClipboard) Clipboard.Copy(rendered, saved?.Id); ReplaceLast(rendered);
                    if (saved != null) sidebar?.SelectRecentTab();
                    return saved;
                }
            }
        }
        public void Edit(Bitmap image, HistoryItem item = null, string document = null)
        {
            if (item != null && BusyIds.Contains(item.Id)) { image.Dispose(); Notify("该图片正在使用，请先关闭已有编辑器、贴图或选字窗口。"); return; }
            AcquireRecord(item?.Id);
            try
            {
                using (image)
                using (var form = new RegionCaptureForm(RegionCaptureMode.Editor, JsonConvert.DeserializeObject<RegionCaptureOptions>(JsonConvert.SerializeObject(captureOptions)), new Bitmap(image)))
                {
                    form.Options.ShowMagnifier = form.Options.ShowInfo = Settings.EditorCursorPreview;
                    if (document == null && item != null) document = File.ReadAllText(item.DocumentPath);
                    if (!string.IsNullOrWhiteSpace(document)) form.RestoreShotCabDocument(document);
                    bool persist = false, pin = false, asNew = Settings.EditAsNew, bake = false;
                    CancellationTokenSource editorOcrCancellation = null;
                    Action<string> copyOcrText = value => Safe(() =>
                    {
                        if (string.IsNullOrEmpty(value)) return;
                        Clipboard.CopyText(value);
                        if (Settings.OcrPreviewSeconds > 0) new TextPreview(value, Settings.OcrPreviewSeconds).Show();
                    });
                    Action startEditorOcr = async () =>
                    {
                        editorOcrCancellation?.Cancel();
                        var current = new CancellationTokenSource();
                        editorOcrCancellation = current;
                        form.ShotCabShowOcrPanel();
                        try
                        {
                            using (var composed = form.GetResultImage())
                            {
                                var result = await Task.Run(() => OcrService.Recognize(this, composed, current.Token));
                                if (!form.IsDisposed && ReferenceEquals(editorOcrCancellation, current))
                                {
                                    string ready = result.Lines.Count == 0
                                        ? (Localize.English ? "No text found. Try a clearer image." : "未识别到文字，可尝试更清晰的图片。")
                                        : (Localize.English
                                            ? $"{result.Lines.Count} lines recognized. Drag over image text, or edit it here."
                                            : $"已识别 {result.Lines.Count} 行。可在图片上拖选文字，或在右侧修改。");
                                    var selection = new OcrCanvas(composed);
                                    selection.SetDocument(result);
                                    selection.CopyRequested += copyOcrText;
                                    selection.ZoomRequested += form.ShotCabZoomOcrImage;
                                    selection.SelectionChanged += () =>
                                    {
                                        string selected = selection.SelectedText;
                                        form.ShotCabSetOcrSelectedText(selected);
                                        form.ShotCabSetOcrSelectionStatus(string.IsNullOrEmpty(selected) ? ready :
                                            Localize.English ? $"Selected {selected.Length} characters · Ctrl+C to copy" : $"已选中 {selected.Length} 字 · Ctrl+C 复制");
                                    };
                                    form.ShotCabSetOcrResult(result.Text, ready);
                                    form.ShotCabAttachOcrOverlay(selection);
                                }
                            }
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception ex)
                        {
                            if (!form.IsDisposed && ReferenceEquals(editorOcrCancellation, current))
                                form.ShotCabSetOcrSelectionStatus(ex is FileNotFoundException && Localize.English
                                    ? "Offline OCR is not installed. Select ShotCab.Ocr.exe in Settings → OCR/Other and keep its full folder."
                                    : ex.Message);
                        }
                        finally
                        {
                            if (ReferenceEquals(editorOcrCancellation, current)) editorOcrCancellation = null;
                            current.Dispose();
                        }
                    };
                    form.ShotCabToolbarPreparing += () =>
                    {
                        form.ShotCabOrganizeTools(Settings.DarkTheme);
                        form.ShotCabAddCommand(Localize.T("鼠标放大预览：开/关"), () => { Settings.EditorCursorPreview = !form.Options.ShowMagnifier; form.Options.ShowMagnifier = form.Options.ShowInfo = Settings.EditorCursorPreview; SaveBasicPreferences(); form.Invalidate(); });
                        form.ShotCabAddCommand(Localize.T("完成并复制"), () => { persist = true; form.Close(); });
                        form.ShotCabAddCommand(Localize.T("图片拼接"), () => CombineWithEditor(form));
                        form.ShotCabAddCommand(Localize.T("遮挡/聚光样式"), form.ShotCabEffectStyleDialog);
                        form.ShotCabAddCommand(Localize.T("贴图"), () => { persist = true; pin = true; form.Close(); });
                        form.ShotCabAddCommand(Localize.T("另存新截图"), () => { persist = true; asNew = true; form.Close(); });
                        form.ShotCabAddCommand(Localize.T("识别文字"), startEditorOcr);
                        form.ShotCabAddCommand(Localize.T("永久应用遮挡"), () => { if (!form.ShotCabHasRedactions) { Notify("当前没有实心、马赛克或模糊遮挡。"); return; } if (Ui.Confirm("将永久应用此记录中的遮挡，无法通过撤销恢复被遮挡内容。其他已导出或另存的副本不受影响。")) { bake = persist = true; asNew = false; form.Close(); } });
                    };
                    form.ShotCabOcrCopyRequested += copyOcrText;
                    form.ShotCabOcrClosed += () => editorOcrCancellation?.Cancel();
                    form.FormClosed += (sender, args) => editorOcrCancellation?.Cancel();
                    form.Shown += (sender, args) =>
                    {
                        if (Settings.AutoOcrOnEdit && !form.IsDisposed)
                            form.BeginInvoke(startEditorOcr);
                    };
                    form.CopyImageRequested += bmp => { using (bmp) Clipboard.Copy(bmp, null); persist = true; form.Close(); };
                    form.SaveImageRequested += (bmp, path) => { using (bmp) { var saved = Export(bmp); if (saved != null) { persist = true; form.Close(); } return saved; } };
                    form.SaveImageAsRequested += (bmp, path) => { using (bmp) { var saved = Export(bmp); if (saved != null) { persist = true; form.Close(); } return saved; } };
                    form.ShowDialog();
                    persist |= form.Result == RegionResult.AnnotateRunAfterCaptureTasks || form.Result == RegionResult.AnnotateContinueTask;
                    if (!persist) return;
                    string updatedDocument = form.ExportShotCabDocument();
                    using (var original = bake ? form.ShotCabRedactedBase(out updatedDocument) : form.ShotCabBaseImage())
                    {
                        var changedRaster = !Images.Png(image).SequenceEqual(Images.Png(original));
                        var parsedDocument = Newtonsoft.Json.Linq.JObject.Parse(updatedDocument);
                        bool rasterEdited = changedRaster || (bool?)(string.IsNullOrWhiteSpace(document) ? null : Newtonsoft.Json.Linq.JObject.Parse(document)["RasterEdited"]) == true;
                        parsedDocument["RasterEdited"] = rasterEdited;
                        updatedDocument = parsedDocument.ToString(Formatting.None);
                        using (var rendered = bake ? RenderDocument(original, updatedDocument) : form.GetResultImage())
                        {
                        var saved = Save(original, rendered, updatedDocument, item, asNew, form.ShotCabHasAnnotations || rasterEdited || item?.RedactionBaked == true, form.ShotCabHasRedactions || item?.RedactionBaked == true, bake || item?.RedactionBaked == true);
                        Clipboard.Copy(rendered, saved?.Id); ReplaceLast(rendered);
                        if (pin) Pin(rendered, saved?.Id);
                        }
                    }
                    captureOptions.AnnotationOptions = form.Options.AnnotationOptions;
                    File.WriteAllText(Path.Combine(Store.RootPath, "annotation-options.json"), JsonConvert.SerializeObject(captureOptions));
                }
            }
            catch (Exception ex) { Ui.Error(ex); }
            finally { ReleaseRecord(item?.Id); }
        }
        private HistoryItem Save(Bitmap original, Bitmap current, string document, HistoryItem item, bool asNew, bool edited, bool redacted, bool baked)
        {
            if (!Settings.HistoryEnabled && item == null) return null;
            try
            {
                var saved = item == null ? Store.Add(Images.Png(original), Images.Png(current), document, edited, redacted, baked, current.Width, current.Height) : Store.Update(item.Id, Images.Png(original), Images.Png(current), document, edited, redacted, baked, current.Width, current.Height, asNew);
                Refresh(); return saved;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is Microsoft.Data.Sqlite.SqliteException) { Notify("历史未保存；截图仍可复制。" + ex.Message); return null; }
        }
        private Bitmap RenderDocument(Bitmap original, string document)
        {
            using (var renderer = new RegionCaptureForm(RegionCaptureMode.Editor, captureOptions, new Bitmap(original)))
            { renderer.RestoreShotCabDocument(document); return renderer.GetResultImage(); }
        }
        private void ReplaceLast(Bitmap image) { lastImage?.Dispose(); lastImage = new Bitmap(image); }
        internal Bitmap LastCaptureImage() => lastImage == null ? null : new Bitmap(lastImage);
        private void ShowCaptureResult(HistoryItem saved)
        {
            resultBar?.Close();
            if (lastImage == null) return;
            var bar = new CaptureResultBar(this, saved, lastImage);
            resultBar = bar;
            bar.FormClosed += (sender, args) => { if (ReferenceEquals(resultBar, bar)) resultBar = null; bar.Dispose(); };
            bar.PlaceNear(Screen.FromPoint(Cursor.Position), sidebar);
            bar.Show();
        }
        public void Copy(HistoryItem item, bool cut = false) => Safe(() => { using (var image = Images.Load(item.CurrentPath)) { Clipboard.Copy(image, item.Id, cut, Settings.ExportFormat, Settings.JpegQuality); ReplaceLast(image); } sidebar.RefreshClipboard(); });
        public void Pin(Bitmap bitmap, string id)
        {
            var form = new PinForm(this, bitmap, id); pins.Add(form); AcquireRecord(id);
            form.FormClosed += (s, e) => { pins.Remove(form); ReleaseRecord(id); };
            form.Show();
        }
        public void PinClipboard() => Safe(() => { if (System.Windows.Forms.Clipboard.ContainsImage()) { using (var image = System.Windows.Forms.Clipboard.GetImage()) using (var bmp = new Bitmap(image)) Pin(bmp, null); } else if (lastImage != null) Pin(lastImage, null); else Notify("剪贴板中没有图片。"); });
        public void ImportFile() => Safe(() => { using (var dialog = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp", Multiselect = true }) if (dialog.ShowDialog() == DialogResult.OK) foreach (var path in dialog.FileNames) Edit(Images.Load(path)); });
        public void ImportClipboard() => Safe(() => { if (System.Windows.Forms.Clipboard.ContainsImage()) using (var image = System.Windows.Forms.Clipboard.GetImage()) Edit(new Bitmap(image)); else Notify("剪贴板中没有图片。"); });
        internal static bool IsImportableImageFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            return new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff" }
                .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
        }
        internal int ImportDroppedImages(IEnumerable<string> paths)
        {
            if (!Settings.HistoryEnabled) { Notify("请先打开“保存截图历史”，再拖入图片。"); return 0; }
            int imported = 0, failed = 0;
            foreach (string path in paths.Where(IsImportableImageFile).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    using (var image = Images.Load(path))
                        if (Save(image, image, "{\"Version\":1,\"Shapes\":[]}", null, false, false, false, false) != null) imported++;
                        else failed++;
                }
                catch (Exception ex) when (ex is ArgumentException || ex is System.Runtime.InteropServices.ExternalException ||
                    ex is IOException || ex is UnauthorizedAccessException || ex is OutOfMemoryException) { failed++; }
            }
            if (failed > 0) Notify("部分图片未能导入：" + failed + " 张。请检查格式或磁盘空间。");
            else if (imported > 0) Notify("已导入 " + imported + " 张图片到历史。");
            return imported;
        }
        internal bool ImportDroppedImage(Image image)
        {
            if (!Settings.HistoryEnabled) { Notify("请先打开“保存截图历史”，再拖入图片。"); return false; }
            using (var copy = new Bitmap(image))
            {
                bool saved = Save(copy, copy, "{\"Version\":1,\"Shapes\":[]}", null, false, false, false, false) != null;
                if (saved) Notify("已导入图片到历史。");
                return saved;
            }
        }
        public string Export(Image image)
        {
            using (var dialog = new SaveFileDialog { Filter = "PNG 图片|*.png|JPEG 图片|*.jpg", FilterIndex = Settings.ExportFormat == "jpg" ? 2 : 1, FileName = "ShotCab-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), AddExtension = true })
            { if (dialog.ShowDialog() != DialogResult.OK) return null; Images.Export(image, dialog.FileName, Settings.JpegQuality); return dialog.FileName; }
        }
        public ContextMenuStrip ItemMenu(HistoryItem item)
        {
            // Closed runs inside WinForms' visibility transition; disposing there breaks that transition.
            var menu = itemMenu;
            menu.Close();
            while (menu.Items.Count > 0) menu.Items[0].Dispose();
            Add(menu, "复制图片", () => Copy(item)); Add(menu, "剪切导出文件（保留历史）", () => Copy(item, true));
            Add(menu, "修改", () => Safe(() => Edit(Images.Load(item.OriginalPath), item)));
            Add(menu, "拼接另一张图片", () => CombineImages(item));
            Add(menu, "贴图", () => Safe(() => { using (var image = Images.Load(item.CurrentPath)) Pin(image, item.Id); }));
            Add(menu, "OCR 选字", () => Safe(() => { using (var image = Images.Load(item.CurrentPath)) new OcrForm(this, image, item).Show(); }));
            Add(menu, "识别二维码", () => Safe(() => { using (var image = Images.Load(item.CurrentPath)) { var text = new ZXing.BarcodeReader().Decode(image)?.Text; if (text == null) Notify("未发现可识别的二维码。"); else ShowText(text); } }));
            Add(menu, "导出", () => Safe(() => { using (var image = Images.Load(item.CurrentPath)) Export(image); }));
            Add(menu, "打开图片所在文件夹", () => Safe(() => Process.Start("explorer.exe", "/select,\"" + item.CurrentPath + "\"")));
            menu.Items.Add(new ToolStripSeparator());
            Add(menu, item.Favorite ? "取消收藏" : "★ 收藏（免于到期清理）", () => Safe(() => { Store.SetFavorite(item.Id, !item.Favorite); Refresh(); }));
            Add(menu, item.Hidden ? "恢复侧栏显示" : "隐藏", () => Safe(() => { Store.SetHidden(item.Id, !item.Hidden); Refresh(); }));
            Add(menu, "设置标签", () => EditTags(new[] { item.Id }, item.Tags));
            Add(menu, "删除到回收站", () => Trash(new[] { item.Id }));
            Ui.StyleMenu(menu); return menu;
        }
        internal void PreviewCard(HistoryItem item, Point location, bool startDrag = true)
        {
            using (var image = Images.Load(item.CurrentPath))
            {
                var form = new PinForm(this, image, item.Id, true); pins.Add(form); AcquireRecord(item.Id);
                form.FormClosed += (s,e) => { pins.Remove(form); ReleaseRecord(item.Id); };
                form.StartPosition = FormStartPosition.Manual;
                var area = Screen.FromPoint(location).WorkingArea;
                form.Location = new Point(Math.Max(area.Left, Math.Min(area.Right - form.Width, location.X - form.Width / 2)), Math.Max(area.Top, Math.Min(area.Bottom - form.Height, location.Y - 18)));
                form.Show();
                if (startDrag) { Native.ReleaseCapture(); Native.SendMessage(form.Handle, 0xA1, new IntPtr(2), IntPtr.Zero); }
            }
        }
        public void EditTags(IEnumerable<string> ids, IEnumerable<string> existing = null)
        {
            var value = Ui.Prompt("标签", "用逗号分隔多个标签；留空移除标签。", string.Join(", ", existing ?? new string[0]));
            if (value != null) Safe(() => { foreach (var id in ids) Store.SetTags(id, value.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())); Refresh(); });
        }
        public void Trash(IEnumerable<string> ids) => Safe(() => { var list = ids.Where(x => !BusyIds.Contains(x)).ToArray(); Store.Trash(list, DateTime.UtcNow); Refresh(); if (list.Length < ids.Count()) Notify("正在编辑或贴图的图片暂未删除。"); });
        public void ShowText(string text) { var f = new TextResultForm(this, text); f.Show(); }
        private static string ChooseCombineFile(string title, IWin32Window owner = null)
        {
            using (var dialog = new OpenFileDialog { Title = Localize.T(title), Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff", Multiselect = false })
                return (owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) == DialogResult.OK ? dialog.FileName : null;
        }

        private static bool? ChooseCombineDirection(IWin32Window owner = null)
        {
            var result = MessageBox.Show(owner, Localize.T("选择拼接方向：是 = 横向，否 = 纵向。"), Localize.T("图片拼接"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            return result == DialogResult.Cancel ? (bool?)null : result == DialogResult.Yes;
        }

        private void CombineWithEditor(RegionCaptureForm editor) => Safe(() =>
        {
            string secondPath = ChooseCombineFile("选择要拼接的另一张图片", editor);
            if (secondPath == null) return;
            bool? horizontal = ChooseCombineDirection(editor);
            if (!horizontal.HasValue) return;
            using (var first = editor.GetResultImage())
            using (var second = Images.Load(secondPath))
                editor.ShotCabReplaceCanvas(ImageCombiner.Combine(first, second, horizontal.Value));
        });

        public void CombineImages(HistoryItem firstItem = null) => Safe(() =>
        {
            string firstPath = firstItem?.CurrentPath ?? ChooseCombineFile("选择第一张图片");
            if (firstPath == null) return;
            string secondPath = ChooseCombineFile("选择第二张图片");
            if (secondPath == null) return;
            bool? horizontal = ChooseCombineDirection();
            if (!horizontal.HasValue) return;
            using (var first = Images.Load(firstPath))
            using (var second = Images.Load(secondPath))
                Edit(ImageCombiner.Combine(first, second, horizontal.Value));
        });
        public void Migrate(string destination)
        {
            if (BusyIds.Count != 0) throw new InvalidOperationException("迁移前请关闭编辑器、贴图和选字窗口。");
            var result = Store.MigrateTo(destination); File.WriteAllText(pointerPath, result.DestinationRoot);
            Store.Dispose(); Store = result.Store; settingsStore = new SettingsStore(Store.RootPath); settingsStore.Save(Settings); Clipboard = new ClipboardService(Store.RootPath); Refresh();
        }
        private void Maintain() => Safe(() =>
        {
            if (DateTime.UtcNow - lastCleanup < TimeSpan.FromHours(1)) return; lastCleanup = DateTime.UtcNow;
            Store.Cleanup(DateTime.UtcNow, Settings.RetentionDays, Settings.RecycleDays, BusyIds); Clipboard.Cleanup(); Refresh();
            if (Settings.CapacityWarningGB > 0 && Store.GetUsage().TotalBytes > Settings.CapacityWarningGB * 1024 * 1024 * 1024 && DateTime.UtcNow - lastWarning > TimeSpan.FromHours(6)) { lastWarning = DateTime.UtcNow; Notify("历史占用已超过提醒值。打开完整历史的存储管理，可预览清理最旧图片或手动批量清理。不会因容量超限自动删除。"); }
        });
        protected override void ExitThreadCore() { maintenance.Stop(); resultBar?.Close(); sidebar.RemoveReservation(); tray.Visible = false; foreach (var ocr in Application.OpenForms.OfType<OcrForm>().ToArray()) ocr.Close(); foreach (var pin in pins.ToArray()) pin.Close(); history?.Close(); sidebar.Close(); base.ExitThreadCore(); }
        protected override void Dispose(bool disposing) { if (disposing) { maintenance.Dispose(); events.Dispose(); itemMenu.Dispose(); tray.ContextMenuStrip?.Dispose(); tray.Dispose(); sidebar.Dispose(); lastImage?.Dispose(); Store.Dispose(); } base.Dispose(disposing); }
    }
}
