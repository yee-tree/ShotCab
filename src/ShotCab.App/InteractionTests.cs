using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace ShotCab.App
{
    internal static class InteractionTests
    {
        internal static void Run(CabinetContext app)
        {
            Program.Trace("Interaction tests: capture");
            int before = app.Store.Count(new ShotCab.Core.HistoryQuery()); bool refreshed = false;
            string oldCapture=app.Settings.CaptureShortcut;
            if(app.ChangeShortcut("CaptureShortcut",app.Settings.PinShortcut) || app.Settings.CaptureShortcut!=oldCapture) throw new Exception("Duplicate hotkey changed the previous binding");
            Action onChanged = () => refreshed = true; app.Changed += onChanged;
            try { app.CompleteCapture(new Bitmap(24, 16), null, false); }
            finally { app.Changed -= onChanged; }
            if (!refreshed || app.Store.Count(new ShotCab.Core.HistoryQuery()) != before + 1) throw new Exception("Completed capture was not immediately saved/refreshed");
            int beforeEdit = app.Store.Count(new ShotCab.Core.HistoryQuery());
            bool observedEarlySave = false;
            bool originalEditMode = app.Settings.EditAfterCapture;
            app.Settings.EditAfterCapture = true;
            try
            {
                app.ProcessCapturedImage(new Bitmap(24,16), null, (bitmap,record,document) =>
                {
                    using(bitmap) observedEarlySave = record != null && app.Store.Count(new ShotCab.Core.HistoryQuery()) == beforeEdit + 1;
                });
            }
            finally { app.Settings.EditAfterCapture = originalEditMode; }
            if (!observedEarlySave) throw new Exception("Capture did not appear in history before direct editing");
            var liveSidebar = (SidebarForm)typeof(CabinetContext).GetField("sidebar", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(app);
            if ((bool)typeof(SidebarForm).GetField("history", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(liveSidebar)) throw new Exception("A completed capture did not switch to Recent");
            app.ToggleSidebar();
            if (!app.SidebarManuallyHidden) throw new Exception("Sidebar did not hide before tray reopen test");
            app.ShowSidebar();
            if (app.SidebarManuallyHidden || !liveSidebar.Visible) throw new Exception("Tray reopen did not show the sidebar");
            app.ToggleSidebar();
            app.ShowSettings();
            if (app.SidebarManuallyHidden || !liveSidebar.Visible || !liveSidebar.ActiveSettingsCategory.HasValue)
                throw new Exception("Tray settings did not restore the sidebar from a manually hidden state");
            liveSidebar.SelectHistoryTab();
            if (!liveSidebar.Visible || liveSidebar.ActiveSettingsCategory.HasValue) throw new Exception("History navigation hid the sidebar after tray settings");
            liveSidebar.SelectRecentTab();
            if (!liveSidebar.Visible) throw new Exception("Recent navigation hid the sidebar after tray settings");
            FreehandCaptureTool();
            CombinePixels();
            var latest = app.Store.Query(new ShotCab.Core.HistoryQuery { Limit = 1 })[0];
            app.PreviewCard(latest, new Point(300, 200), false);
            PinForm preview = null;
            foreach (Form form in Application.OpenForms) if (form is PinForm pin && pin.ItemId == latest.Id) preview = pin;
            if (preview == null || preview.FormBorderStyle != FormBorderStyle.None || !preview.ShowInTaskbar || !app.BusyIds.Contains(latest.Id)) throw new Exception("Preview card lifecycle or chrome failed");
            var cardBar = preview.Controls[0];
            foreach (Control control in cardBar.Controls)
                if (control.AccessibleRole == AccessibleRole.PushButton && (control.Width < 20 || control.Right > cardBar.ClientSize.Width || control.Left < 0)) throw new Exception("Card window buttons are clipped");
            preview.ToggleMaximize();
            if (preview.WindowState != FormWindowState.Maximized) throw new Exception("Card did not maximize");
            preview.ToggleMaximize(); preview.WindowState = FormWindowState.Minimized;
            if (preview.WindowState != FormWindowState.Minimized) throw new Exception("Card did not minimize");
            preview.WindowState = FormWindowState.Normal; preview.Close();
            if (app.BusyIds.Contains(latest.Id)) throw new Exception("Card did not release history protection");
            if (!System.IO.File.Exists(latest.CurrentPath)) throw new Exception("Screenshot was not saved as an independent file");
            EditorChrome();
            var shiftedWorkArea = new Rectangle(0, 0, 1600, 1040);
            var monitorBounds = new Rectangle(0, 0, 1920, 1080);
            if (SidebarForm.OverlayBounds(monitorBounds, shiftedWorkArea, 320, false).Right != monitorBounds.Right ||
                SidebarForm.OverlayBounds(monitorBounds, shiftedWorkArea, 320, true).Left != monitorBounds.Left)
                throw new Exception("Overlay sidebar did not return to the physical monitor edge after reservation");
            var today = DateTime.Today;
            if (PhotoStrip.DateHeading(today, today) != Localize.T("今天") ||
                PhotoStrip.DateHeading(today.AddDays(-1), today) != Localize.T("昨天") ||
                PhotoStrip.DateHeading(today.AddDays(-2), today) != today.AddDays(-2).ToString("yyyy-MM-dd"))
                throw new Exception("History date headings do not distinguish today, yesterday and earlier days");
            using(var grouped = new PhotoStrip(app))
            {
                grouped.Width=280;
                grouped.SetItems(new List<ShotCab.Core.HistoryItem>(), false);
                string recentEmpty = grouped.EmptyStateTitle;
                grouped.SetItems(new List<ShotCab.Core.HistoryItem>(), true);
                if (recentEmpty == grouped.EmptyStateTitle) throw new Exception("Recent and History empty states are indistinguishable");
                latest.Tags.AddRange(new[] { "工作", "参考", "一个用于验证换行的长标签" });
                var badges=grouped.BadgeLayout(latest);
                if(badges.FindAll(x=>x.Custom).Count!=3 || badges.Exists(x=>x.Text.Contains("+1"))) throw new Exception("Custom tags were collapsed into a count");
                for(int i=0;i<badges.Count;i++) for(int j=i+1;j<badges.Count;j++) if(badges[i].Bounds.IntersectsWith(badges[j].Bounds)) throw new Exception("Tag badges overlap");
                foreach(var badge in badges) if(badge.Bounds.Right>244) throw new Exception("Tag exceeds card width");
                latest.Tags.Clear();
                grouped.SetItems(new List<ShotCab.Core.HistoryItem> { latest },true);
                var flags=BindingFlags.Instance | BindingFlags.NonPublic;
                var rows=(System.Collections.ICollection)typeof(PhotoStrip).GetField("rows",flags).GetValue(grouped);
                if(rows.Count!=2) throw new Exception("Date header missing");
                grouped.ToggleDate(latest.CreatedUtc.ToLocalTime().Date);
                if(rows.Count!=1) throw new Exception("Date collapse failed");
                grouped.ToggleDate(latest.CreatedUtc.ToLocalTime().Date);
                if(rows.Count!=2) throw new Exception("Date expand failed");
                string droppedFile = System.IO.Path.Combine(app.Store.RootPath, "drop-import-test.png");
                try
                {
                    using (var source = new Bitmap(19, 11)) source.Save(droppedFile, System.Drawing.Imaging.ImageFormat.Png);
                    var data = new DataObject(); data.SetData(DataFormats.FileDrop, new[] { droppedFile });
                    var drag = new DragEventArgs(data, 0, 0, 0, DragDropEffects.Copy, DragDropEffects.None);
                    int beforeDrop = app.Store.Count(new ShotCab.Core.HistoryQuery());
                    typeof(PhotoStrip).GetMethod("OnDragEnter", flags).Invoke(grouped, new object[] { drag });
                    if (drag.Effect != DragDropEffects.Copy) throw new Exception("Sidebar rejected an image file drop");
                    typeof(PhotoStrip).GetMethod("OnDragDrop", flags).Invoke(grouped, new object[] { drag });
                    if (app.Store.Count(new ShotCab.Core.HistoryQuery()) != beforeDrop + 1) throw new Exception("Dropped image was not saved to history");
                    var imported = app.Store.Query(new ShotCab.Core.HistoryQuery { Limit = 1 })[0];
                    System.IO.File.Delete(droppedFile);
                    if (imported.Width != 19 || imported.Height != 11 || !System.IO.File.Exists(imported.CurrentPath))
                        throw new Exception("Dropped image still depends on its source file");
                }
                finally { if (System.IO.File.Exists(droppedFile)) System.IO.File.Delete(droppedFile); }
            }
            using (var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) })
            {
                host.Show();
                for (int i = 0; i < 12; i++)
                {
                    var menu = app.ItemMenu(app.Store.Get(latest.Id));
                    menu.Show(host, new Point(10, 10));
                    menu.Close(ToolStripDropDownCloseReason.AppClicked);
                    if (menu.IsDisposed) throw new Exception("Context menu disposed during the close event");
                    menu.Show(host, new Point(10, 10));
                    menu.Close(ToolStripDropDownCloseReason.ItemClicked);
                    foreach (ToolStripItem entry in menu.Items)
                        if (entry.Text.Contains("收藏")) { entry.PerformClick(); break; }
                    if (app.Store.Get(latest.Id).Favorite != (i % 2 == 0)) throw new Exception("Context menu action used stale record state");
                    Application.DoEvents();
                }
                host.Hide();
            }
            using (var sidebar = new SidebarForm(app))
            {
                Program.Trace("Interaction tests: sidebar");
                sidebar.ApplySettings();
                Program.Trace("Sidebar applied");
                var screen = Screen.AllScreens[Math.Max(0, Math.Min(Screen.AllScreens.Length - 1, app.Settings.SidebarScreen))];
                if (sidebar.Right != screen.WorkingArea.Right || sidebar.Top != screen.WorkingArea.Top) throw new Exception("Sidebar did not dock to right edge on first show");
                sidebar.ShowBasicSettings(true);
                Program.Trace("Sidebar settings shown");
                if ((Native.GetWindowLong(sidebar.Handle, -20) & 0x08000000) != 0)
                    throw new Exception("Settings sidebar still prevents keyboard focus");
                var timings = System.Diagnostics.Stopwatch.StartNew();
                for (int cycle = 0; cycle < 5; cycle++) { sidebar.ShowBasicSettings(false); sidebar.ShowBasicSettings(true); }
                timings.Stop(); Program.Trace("Five settings switches ms: " + timings.ElapsedMilliseconds);
                if (timings.ElapsedMilliseconds > 5000) throw new Exception("Settings tab rebuild is excessively slow");
                var basic = (FlowLayoutPanel)typeof(SidebarForm).GetField("basicSettings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sidebar);
                if (basic.AutoScrollPosition != Point.Empty || basic.Controls[0].Top < 0) throw new Exception("Settings opened scrolled away from the top");
                var categories = basic.Controls.OfType<SettingsNavigation>().Single();
                categories.PerformLayout();
                for (int row = 0; row < 2; row++)
                {
                    if (categories.Controls[row * 3].Left != 0 || categories.Controls[row * 3 + 2].Right != categories.Width)
                        throw new Exception("Settings categories do not fill the available width");
                    for (int column = 1; column < 3; column++)
                        if (categories.Controls[row * 3 + column - 1].Right != categories.Controls[row * 3 + column].Left)
                            throw new Exception("Settings categories have a horizontal gap");
                }
                if (Ui.Dark && (Ui.Background.R != Ui.Background.G || Ui.Surface.R != Ui.Surface.G || Ui.Accent.R != Ui.Accent.G))
                    throw new Exception("Dark sidebar palette is not neutral");
                var hideOption = (SoftToggle)basic.Controls["HideSidebarDuringCapture"];
                sidebar.SelectBasicCategory(1);
                FlowLayoutPanel numberRow = null;
                foreach (Control control in basic.Controls) if (control.Name == "PresetValue") { numberRow = (FlowLayoutPanel)control; break; }
                if (numberRow == null || !(numberRow.Controls[0] is SoftChoice) || !(numberRow.Controls[1] is TextBox)) throw new Exception("Preset/custom numeric controls missing");
                var preset = (SoftChoice)numberRow.Controls[0]; var custom = (TextBox)numberRow.Controls[1]; int retention = app.Settings.RetentionDays;
                preset.SelectedIndex = 3;
                if (app.Settings.RetentionDays != 30 || custom.Visible) throw new Exception("Retention preset failed");
                preset.SelectedIndex = preset.Items.Count - 1; custom.Text = "12";
                typeof(Control).GetMethod("OnLeave", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(custom, new object[] { EventArgs.Empty });
                if (app.Settings.RetentionDays != 12 || !custom.Visible) throw new Exception("Custom retention failed");
                app.Settings.RetentionDays = retention; sidebar.SelectBasicCategory(0);
                hideOption.Checked = false;
                if (app.ShouldHideForCapture(sidebar)) throw new Exception("Capture ignored visible sidebar preference");
                hideOption.Checked = true;
                if (!app.ShouldHideForCapture(sidebar)) throw new Exception("Capture did not hide sidebar by default");
                var fields = BindingFlags.Instance | BindingFlags.NonPublic;
                var settingsTab = (Control)typeof(SidebarForm).GetField("settingsTab", fields).GetValue(sidebar);
                var recentTab = (Control)typeof(SidebarForm).GetField("recentTab", fields).GetValue(sidebar);
                var historyTab = (Control)typeof(SidebarForm).GetField("historyTab", fields).GetValue(sidebar);
                if (!recentTab.AllowDrop || !historyTab.AllowDrop) throw new Exception("Recent and History tabs do not accept image drops");
                if (settingsTab.BackColor != Ui.Accent || recentTab.BackColor == Ui.Accent) throw new Exception("Settings navigation highlight is incorrect");
                if (!((Control)typeof(SidebarForm).GetField("basicSettings", fields).GetValue(sidebar)).Visible || ((Control)typeof(SidebarForm).GetField("strip", fields).GetValue(sidebar)).Visible) throw new Exception("Basic settings did not replace photo content");
                sidebar.ShowBasicSettings(false);
                if ((Native.GetWindowLong(sidebar.Handle, -20) & 0x08000000) == 0)
                    throw new Exception("Photo sidebar did not restore its no-activation behavior");
                if (recentTab.BackColor != Ui.Accent || settingsTab.BackColor == Ui.Accent) throw new Exception("Recent navigation highlight did not return");
                if (!((Control)typeof(SidebarForm).GetField("strip", fields).GetValue(sidebar)).Visible) throw new Exception("Recent content did not return after settings");
                var photoStrip = (PhotoStrip)typeof(SidebarForm).GetField("strip",fields).GetValue(sidebar);
                typeof(PhotoStrip).GetMethod("OnMouseUp",fields).Invoke(photoStrip,new object[] { new MouseEventArgs(MouseButtons.Right,1,20,20,0) });
                string rightSelected = (string)typeof(PhotoStrip).GetField("selected",fields).GetValue(photoStrip);
                string newestId = app.Store.Query(new ShotCab.Core.HistoryQuery { Limit = 1 })[0].Id;
                if (rightSelected != newestId)
                    throw new Exception("Right click did not select the image under the pointer: selected=" + rightSelected + ", newest=" + newestId);
                app.ItemMenu(latest).Close();
                int savedWidth = app.Settings.SidebarWidth;
                foreach (var side in new[] { ShotCab.Core.SidebarDockSide.靠左, ShotCab.Core.SidebarDockSide.靠右 })
                {
                    Program.Trace("Interaction tests: dock " + side);
                    app.Settings.SidebarSide = side; sidebar.ApplySettings();
                    if (side == ShotCab.Core.SidebarDockSide.靠左 ? sidebar.Left != screen.WorkingArea.Left : sidebar.Right != screen.WorkingArea.Right) throw new Exception("Sidebar dock side incorrect");
                    typeof(SidebarForm).GetField("resizeStartX", fields).SetValue(sidebar, 500);
                    typeof(SidebarForm).GetField("resizeStartWidth", fields).SetValue(sidebar, sidebar.Width);
                    sidebar.ResizeFromDrag(side == ShotCab.Core.SidebarDockSide.靠左 ? 10000 : -10000);
                    if (app.Settings.SidebarWidth != 640) throw new Exception("Drag maximum width incorrect");
                    sidebar.ResizeFromDrag(side == ShotCab.Core.SidebarDockSide.靠左 ? -10000 : 10000);
                    if (app.Settings.SidebarWidth != 180) throw new Exception("Drag minimum width incorrect");
                    sidebar.ShowBasicSettings(true); sidebar.PerformLayout();
                    var settingsPanel = (Control)typeof(SidebarForm).GetField("basicSettings", fields).GetValue(sidebar);
                    foreach (Control control in settingsPanel.Controls)
                        if (control.Right > settingsPanel.ClientSize.Width) throw new Exception("Settings control clipped at minimum width: " + control.Text);
                    if (sidebar.Opacity != 1) throw new Exception("Sidebar text is transparent");
                }
                app.Settings.SidebarWidth = savedWidth; app.Settings.SidebarSide = ShotCab.Core.SidebarDockSide.靠右;
                sidebar.Hide();
            }
            using (var image = new Bitmap(200, 100))
            using (var canvas = new OcrCanvas(image))
            {
                canvas.CreateControl();
                canvas.SetDocument(new OcrDocument
                {
                    Version = 1, Text = "A B C\n中文",
                    Lines = new List<OcrLine>
                    {
                        new OcrLine { Text = "A B C", Chars = new List<OcrCharacter> { Character("A",10,10), Character("B",30,10), Character("C",50,10) } },
                        new OcrLine { Text = "中文", Chars = new List<OcrCharacter> { Character("中",10,50), Character("文",30,50) } }
                    }
                });
                foreach (float scale in new[] { 0.5f, 1f, 2f })
                {
                    canvas.Zoom(scale);
                    Mouse(canvas, "OnMouseDown", 35, 15, scale);
                    Mouse(canvas, "OnMouseMove", 15, 55, scale);
                    Mouse(canvas, "OnMouseUp", 15, 55, scale);
                    if (canvas.SelectedText != "B C" + Environment.NewLine + "中") throw new Exception("OCR cross-line selection or coordinate scale failed at " + scale);
                    Mouse(canvas, "OnMouseDown", 15, 55, scale);
                    Mouse(canvas, "OnMouseMove", 35, 15, scale);
                    Mouse(canvas, "OnMouseUp", 35, 15, scale);
                    if (canvas.SelectedText != "B C" + Environment.NewLine + "中") throw new Exception("OCR reverse selection changed reading order");
                }
                canvas.SelectAllText();
                if (canvas.SelectedText != "A B C" + Environment.NewLine + "中文") throw new Exception("OCR full selection lost spaces");
            }
            string id = Guid.NewGuid().ToString("D");
            using (var image = new Bitmap(12, 8))
            {
                image.SetPixel(3, 4, Color.Red);
                using (var payload = app.Clipboard.Build(image, false, false))
                {
                    if (!payload.GetDataPresent(DataFormats.Bitmap) || !payload.GetDataPresent("PNG")) throw new Exception("Clipboard image formats missing");
                    var bitmap = (Bitmap)payload.GetData(DataFormats.Bitmap);
                    if (bitmap.GetPixel(3, 4).ToArgb() != Color.Red.ToArgb()) throw new Exception("Clipboard bitmap content changed");
                    var stream = (System.IO.MemoryStream)payload.GetData("PNG");
                    using (var decoded = Images.FromBytes(stream.ToArray())) if (decoded.GetPixel(3, 4).ToArgb() != Color.Red.ToArgb()) throw new Exception("PNG clipboard content changed");
                    if (payload.GetDataPresent(DataFormats.FileDrop)) throw new Exception("Ordinary image copy unexpectedly requires a file");
                }
            }
            app.AcquireRecord(id); app.AcquireRecord(id); app.ReleaseRecord(id);
            if (!app.BusyIds.Contains(id)) throw new Exception("Closing one of multiple record users released protection too early");
            app.ReleaseRecord(id); app.ReleaseRecord(id);
            if (app.BusyIds.Contains(id)) throw new Exception("Last record user did not release protection");
            using (var form = new HistoryForm(app))
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var fromDate = (DateTimePicker)typeof(HistoryForm).GetField("from", flags).GetValue(form);
                if (fromDate.CustomFormat != "yyyy-MM-dd" || fromDate.Width < 150) throw new Exception("History date filter is clipped");
                var enabled = (SoftToggle)typeof(HistoryForm).GetField("limitSize", flags).GetValue(form);
                var maximum = (NumericUpDown)typeof(HistoryForm).GetField("maxSize", flags).GetValue(form);
                var minimum = (NumericUpDown)typeof(HistoryForm).GetField("minSize", flags).GetValue(form);
                enabled.Checked = true; maximum.Value = 0; minimum.Value = 0;
                var query = (ShotCab.Core.HistoryQuery)typeof(HistoryForm).GetMethod("Query", flags).Invoke(form, null);
                if (query.MaxSizeBytes != 0 || !maximum.Enabled || app.Store.Count(query) != 0) throw new Exception("History maximum-size filter not applied");
                enabled.Checked = false;
                query = (ShotCab.Core.HistoryQuery)typeof(HistoryForm).GetMethod("Query", flags).Invoke(form, null);
                if (query.MaxSizeBytes != null || maximum.Enabled) throw new Exception("Disabling maximum-size filter retained its limit");
            }
        }
        // The windowed editor replaces the native frame with a ShotCab caption bar. Checks the
        // borderless state, the caption buttons, the toolbar sitting below the caption and the
        // real minimize/maximize/restore behaviour, mirroring the preview-card chrome checks.
        private static void FreehandCaptureTool()
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            using (var bitmap = new Bitmap(24, 24))
            using (var capture = new ShareX.ScreenCaptureLib.RegionCaptureForm(ShareX.ScreenCaptureLib.RegionCaptureMode.Default,
                new ShareX.ScreenCaptureLib.RegionCaptureOptions { QuickCrop = true }, new Bitmap(bitmap)))
            {
                capture.ShotCabSelectRegionTool(ShareX.ScreenCaptureLib.ShapeType.RegionFreehand);
                var manager = typeof(ShareX.ScreenCaptureLib.RegionCaptureForm).GetProperty("ShapeManager", flags).GetValue(capture);
                var tool = (ShareX.ScreenCaptureLib.ShapeType)manager.GetType().GetProperty("CurrentTool", flags).GetValue(manager);
                if (tool != ShareX.ScreenCaptureLib.ShapeType.RegionFreehand) throw new Exception("Freehand capture still starts with a rectangle");
            }
        }

        private static void CombinePixels()
        {
            using (var first = new Bitmap(2, 3))
            using (var second = new Bitmap(4, 1))
            {
                first.SetPixel(0, 0, Color.Red);
                second.SetPixel(0, 0, Color.Blue);
                using (var horizontal = ImageCombiner.Combine(first, second, true))
                    if (horizontal.Size != new Size(6, 3) || horizontal.GetPixel(0, 0).ToArgb() != Color.Red.ToArgb() ||
                        horizontal.GetPixel(2, 0).ToArgb() != Color.Blue.ToArgb())
                        throw new Exception("Horizontal image combine misplaced a source image");
                using (var vertical = ImageCombiner.Combine(first, second, false))
                    if (vertical.Size != new Size(4, 4) || vertical.GetPixel(0, 3).ToArgb() != Color.Blue.ToArgb())
                        throw new Exception("Vertical image combine misplaced a source image");
            }
        }

        private static void EditorChrome()
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            using (var image = new Bitmap(320, 200))
            using (var editor = new ShareX.ScreenCaptureLib.RegionCaptureForm(ShareX.ScreenCaptureLib.RegionCaptureMode.Editor, new ShareX.ScreenCaptureLib.RegionCaptureOptions(), new Bitmap(image)))
            {
                editor.Options.ShowMagnifier = editor.Options.ShowInfo = false;
                editor.StartPosition = FormStartPosition.Manual; editor.Location = new Point(-10000, -10000); editor.Size = new Size(900, 700);
                Point firstToolbarLocation = Point.Empty;
                editor.ShotCabToolbarPreparing += () =>
                {
                    editor.ShotCabOrganizeTools(); editor.ShotCabAddCommand("完成并复制", () => { });
                    var editingManager = typeof(ShareX.ScreenCaptureLib.RegionCaptureForm).GetProperty("ShapeManager", flags).GetValue(editor);
                    var firstToolbar = (Form)editingManager.GetType().GetField("menuForm", flags).GetValue(editingManager);
                    firstToolbar.VisibleChanged += (sender, args) => { if (firstToolbar.Visible) firstToolbarLocation = firstToolbar.Location; };
                };
                editor.Show(); Application.DoEvents(); editor.PerformLayout(); editor.Refresh(); Application.DoEvents();

                if (editor.FormBorderStyle != FormBorderStyle.None) throw new Exception("Editor kept its native frame");
                var bar = (Panel)typeof(ShareX.ScreenCaptureLib.RegionCaptureForm).GetField("shotCabCaptionBar", flags).GetValue(editor);
                int captionHeight = (int)typeof(ShareX.ScreenCaptureLib.RegionCaptureForm).GetProperty("ShotCabCaptionHeight", flags).GetValue(editor);
                if (bar == null || captionHeight <= 0 || bar.Dock != DockStyle.Top || bar.Height != captionHeight) throw new Exception("Editor caption bar missing or not docked on top");
                if (bar.Top != 0 || bar.Width != editor.ClientSize.Width) throw new Exception("Caption bar does not span the editor top edge");

                var buttons = new List<Control>();
                foreach (Control control in bar.Controls) if (control.GetType().Name == "ShotCabWindowButton") buttons.Add(control);
                if (buttons.Count != 3) throw new Exception("Editor window buttons missing: " + buttons.Count);
                if (bar.Controls.Cast<Control>().Any(x => x.AccessibleName == "保存并复制图片" || x.AccessibleName == "Save and copy image" || x.AccessibleName == "撤销" || x.AccessibleName == "重做")) throw new Exception("Editing actions still occupy the title bar");
                buttons.Sort((a, b) => a.Left.CompareTo(b.Left));
                for (int i = 0; i < buttons.Count; i++)
                {
                    if (buttons[i].Width < 20 || buttons[i].Top < 0 || buttons[i].Bottom > bar.ClientSize.Height || buttons[i].Right > bar.ClientSize.Width) throw new Exception("Editor window buttons are clipped");
                    if (i > 0 && buttons[i - 1].Bounds.IntersectsWith(buttons[i].Bounds)) throw new Exception("Editor window buttons overlap");
                }
                if (buttons[buttons.Count - 1].Right >= bar.ClientSize.Width) throw new Exception("Editor close button is not inset from the trailing edge: " + buttons[buttons.Count - 1].Right + " of " + bar.ClientSize.Width);
                if (buttons[0].Left < 200) throw new Exception("Editor window buttons are not grouped on the right: " + buttons[0].Left);

                // The compact palette must be below the canvas and clear of the caption.
                var manager = typeof(ShareX.ScreenCaptureLib.RegionCaptureForm).GetProperty("ShapeManager", flags).GetValue(editor);
                var toolbarForm = (Form)manager.GetType().GetField("menuForm", flags).GetValue(manager);
                if (toolbarForm == null || toolbarForm.Top < editor.Top + editor.Height / 2 || toolbarForm.Left != editor.Left || toolbarForm.Width != editor.ClientSize.Width) throw new Exception("Editor toolbar does not align with the full bottom edge: toolbar=" + toolbarForm?.Bounds + " editor=" + editor.Bounds);
                if (firstToolbarLocation != toolbarForm.Location) throw new Exception("Editor toolbar appeared at the wrong location on its first visible frame");
                var strip = (ToolStrip)manager.GetType().GetField("tsMain", flags).GetValue(manager);
                var selection = strip.Items.Cast<ToolStripItem>().First(x => x.Name == ShareX.ScreenCaptureLib.ShapeType.ToolSelect.ToString() && x.Visible);
                var tipPoint = new Point(selection.Bounds.Left + selection.Width / 2, selection.Bounds.Top + selection.Height / 2);
                typeof(Control).GetMethod("OnMouseMove", flags).Invoke(strip, new object[] { new MouseEventArgs(MouseButtons.None, 0, tipPoint.X, tipPoint.Y, 0) });
                if ((string)manager.GetType().GetField("shotCabHoverDescription", flags).GetValue(manager) != selection.ToolTipText)
                    throw new Exception("Editor icon hover did not expose its action description");
                var historyToggle = strip.Items.Cast<ToolStripItem>().First(x => x.Name == "ShotCabHistory");
                historyToggle.PerformClick();
                var historyPanel = (Control)typeof(ShareX.ScreenCaptureLib.RegionCaptureForm).GetField("shotCabHistoryPanel", flags).GetValue(editor);
                if (!historyPanel.Visible || historyPanel.Right != editor.ClientSize.Width || editor.ClientArea.Right > historyPanel.Left) throw new Exception("Editor history did not open at the right without covering the working area");
                historyToggle.PerformClick();
                if (historyPanel.Visible || editor.ClientArea.Width != editor.ClientSize.Width) throw new Exception("Editor history did not fully collapse");

                // Borderless windows lose the native sizing border; the form answers WM_NCHITTEST itself.
                Func<Point, int> hitTest = point =>
                {
                    int packed = ((point.Y & 0xFFFF) << 16) | (point.X & 0xFFFF);
                    return Native.SendMessage(editor.Handle, 0x0084, IntPtr.Zero, new IntPtr(packed)).ToInt32();
                };
                Rectangle screen = editor.RectangleToScreen(editor.ClientRectangle);
                var edges = new Dictionary<int, Point>
                {
                    { 13, new Point(screen.Left + 1, screen.Top + 1) },        // HTTOPLEFT
                    { 10, new Point(screen.Left + 1, screen.Top + (screen.Height / 2)) },
                    { 11, new Point(screen.Right - 2, screen.Top + (screen.Height / 2)) },
                    { 15, new Point(screen.Left + (screen.Width / 2), screen.Bottom - 2) },
                    { 17, new Point(screen.Right - 2, screen.Bottom - 2) }     // HTBOTTOMRIGHT
                };
                foreach (var edge in edges) if (hitTest(edge.Value) != edge.Key) throw new Exception("Borderless editor lost resizing: expected " + edge.Key + ", got " + hitTest(edge.Value));
                int middle = hitTest(new Point(screen.Left + (screen.Width / 2), screen.Top + (screen.Height / 2)));
                if (middle >= 10 && middle <= 17) throw new Exception("Borderless editor reports a resize hit inside the client area: " + middle);

                Action click = () => typeof(Control).GetMethod("OnClick", flags).Invoke(buttons[1], new object[] { EventArgs.Empty });
                click();
                if (editor.WindowState != FormWindowState.Maximized) throw new Exception("Editor did not maximize");
                if (toolbarForm.Width != editor.ClientSize.Width || toolbarForm.Left != editor.Left) throw new Exception("Bottom toolbar did not follow maximize");
                if (editor.Bounds.Height > Screen.FromControl(editor).WorkingArea.Height) throw new Exception("Maximized editor covers the taskbar");
                var glyph = typeof(ShareX.ScreenCaptureLib.RegionCaptureForm).GetField("shotCabMaximizeButton", flags).GetValue(editor);
                if (glyph.GetType().GetProperty("Glyph", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(glyph).ToString() != "Restore") throw new Exception("Maximize button did not switch to the restore glyph");
                if (hitTest(new Point(editor.Left + 1, editor.Top + editor.Height / 2)) >= 10 && hitTest(new Point(editor.Left + 1, editor.Top + editor.Height / 2)) <= 17) throw new Exception("Maximized editor still reports a resize hit");
                click();
                if (editor.WindowState != FormWindowState.Normal) throw new Exception("Editor did not restore");
                if (toolbarForm.Width != editor.ClientSize.Width || toolbarForm.Left != editor.Left) throw new Exception("Bottom toolbar did not follow restore");
                typeof(Control).GetMethod("OnClick", flags).Invoke(buttons[0], new object[] { EventArgs.Empty });
                if (editor.WindowState != FormWindowState.Minimized) throw new Exception("Editor did not minimize");
                editor.WindowState = FormWindowState.Normal;
                System.Windows.Forms.Application.DoEvents();
                editor.Close();
            }
        }
        private static OcrCharacter Character(string text, int x, int y) => new OcrCharacter
        { Text = text, Score = 1, Points = new[] { new Point(x,y), new Point(x+10,y), new Point(x+10,y+12), new Point(x,y+12) } };
        private static void Mouse(OcrCanvas canvas, string method, int x, int y, float scale)
        {
            typeof(OcrCanvas).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas,
                new object[] { new MouseEventArgs(MouseButtons.Left, 1, (int)(x * scale), (int)(y * scale), 0) });
        }
    }
}
