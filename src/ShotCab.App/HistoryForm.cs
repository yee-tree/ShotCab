using ShotCab.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ShotCab.App
{
    internal sealed class HistoryForm : Form
    {
        private readonly CabinetContext app;
        private readonly ListView list;
        private readonly Label status, usage;
        private readonly PictureBox preview;
        private readonly Label previewHint;
        private readonly SoftEntry text, tags;
        private readonly TextBox storagePath;
        private readonly SoftChoice filter, state, sort;
        private readonly SoftToggle matchAll, fromEnabled, toEnabled;
        private readonly DateTimePicker from, to;
        private readonly NumericUpDown minSize, maxSize;
        private readonly SoftToggle limitSize;
        private int page;
        private const int PageSize = 50;
        public HistoryForm(CabinetContext app)
        {
            this.app = app; Ui.Style(this); Text = "ShotCab · 完整历史与存储管理"; Size = new Size(1180, 820); MinimumSize = new Size(900, 620);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12), BackColor = Ui.Background };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 146));
            var search = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(12, 8, 12, 6), BackColor = Ui.Surface };
            var searchTitle = new Label { Text = "筛选记录", Dock = DockStyle.Top, Height = 26, Font = Ui.Font(10, FontStyle.Bold), ForeColor = Ui.Text };
            var searchPrimary = FilterRow(34);
            var searchMeta = FilterRow(34);
            var searchDates = FilterRow(34);
            filter = Combo(new[] { "全部历史", "隐藏项目", "收藏", "回收站" });
            state = Combo(new[] { "全部状态", "原图", "已编辑", "已打码", "已识别文字", "遮挡已固化" });
            sort = Combo(new[] { "最新优先", "最旧优先", "占用最大优先" });
            text = SearchEntry(190); tags = SearchEntry(140);
            tags.Input.AccessibleDescription = "多个标签以逗号分隔";
            matchAll = FilterToggle("匹配全部标签", true);
            var filterButton = Ui.GlassButton("筛选", () => { page = 0; Reload(); });
            filterButton.AutoSize = false; filterButton.MinimumSize = Size.Empty; filterButton.Size = new Size(72, 30); filterButton.Margin = new Padding(3, 0, 3, 0);
            searchPrimary.Controls.AddRange(new Control[] { filter, state, sort, FilterLabel("搜索文字"), text, filterButton });
            fromEnabled = FilterToggle("从", false); toEnabled = FilterToggle("至", false);
            from = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd", Width = 154, Enabled = false };
            to = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd", Width = 154, Enabled = false };
            fromEnabled.CheckedChanged += (s,e) => from.Enabled = fromEnabled.Checked;
            toEnabled.CheckedChanged += (s,e) => to.Enabled = toEnabled.Checked;
            minSize = new NumericUpDown { Minimum = 0, Maximum = 100000, DecimalPlaces = 1, Width = 80 };
            maxSize = new NumericUpDown { Minimum = 0, Maximum = 100000, DecimalPlaces = 1, Width = 80, Value = 100, Enabled = false };
            limitSize = FilterToggle("限制最大占用", false);
            limitSize.CheckedChanged += (s, e) => maxSize.Enabled = limitSize.Checked;
            searchMeta.Controls.AddRange(new Control[] { FilterLabel("标签"), tags, matchAll, FilterLabel("至少 MB"), minSize, limitSize, maxSize });
            searchDates.Controls.AddRange(new Control[] { FilterLabel("日期范围"), fromEnabled, from, toEnabled, to });
            search.Controls.Add(searchDates); search.Controls.Add(searchMeta); search.Controls.Add(searchPrimary); search.Controls.Add(searchTitle);
            foreach (var combo in new[] { filter, state, sort }) combo.SelectedIndexChanged += (s, e) => { page = 0; Reload(); };
            var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(1120, 420), Margin = new Padding(0, 10, 0, 10), SplitterDistance = 795, FixedPanel = FixedPanel.Panel2, Panel2MinSize = 220, Panel1MinSize = 400, BackColor = Ui.Background };
            split.Resize += (s,e) =>
            {
                if (split.ClientSize.Width < 650) return;
                int previewWidth = Math.Max(220, Math.Min(320, (int)(split.ClientSize.Width * 0.28)));
                int distance = split.ClientSize.Width - split.SplitterWidth - previewWidth;
                if (distance >= split.Panel1MinSize && distance != split.SplitterDistance) split.SplitterDistance = distance;
            };
            list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = true, BackColor = Ui.Surface, ForeColor = Ui.Text, BorderStyle = BorderStyle.None, OwnerDraw = true, Font = Ui.Font(9) };
            list.Columns.Add("截图时间", 190); list.Columns.Add("状态", 100); list.Columns.Add("标签", 130); list.Columns.Add("尺寸", 100); list.Columns.Add("占用", 100); list.Columns.Add("收藏/隐藏", 110);
            list.DrawColumnHeader += DrawHistoryHeader;
            list.DrawItem += (s,e) => { };
            list.DrawSubItem += DrawHistoryCell;
            list.Resize += (s,e) => FitHistoryColumns();
            list.SelectedIndexChanged += (s, e) => Preview();
            list.DoubleClick += (s, e) => { var item = Selected().FirstOrDefault(); if (item != null && !item.DeletedUtc.HasValue) app.Safe(() => app.Edit(Images.Load(item.OriginalPath), item)); };
            list.MouseClick += (s, e) => { if (e.Button != MouseButtons.Right) return; var hit = list.GetItemAt(e.X,e.Y); if (hit == null) return; foreach (ListViewItem row in list.Items) row.Selected = row == hit; hit.Focused = true; if (Selected().Count == 1 && !Selected()[0].DeletedUtc.HasValue) app.ItemMenu(Selected()[0]).Show(list, e.Location); };
            preview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Ui.Surface };
            previewHint = new Label { Dock = DockStyle.Fill, Text = "选择一张截图以预览\n双击列表可编辑", TextAlign = ContentAlignment.MiddleCenter, ForeColor = Ui.Muted };
            var previewPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = Ui.Surface };
            var previewTitle = new Label { Dock = DockStyle.Top, Height = 30, Text = "图片预览", Font = Ui.Font(10, FontStyle.Bold), ForeColor = Ui.Text };
            previewPanel.Controls.Add(preview); previewPanel.Controls.Add(previewHint); previewPanel.Controls.Add(previewTitle);
            split.Panel1.Controls.Add(list); split.Panel2.Controls.Add(previewPanel);
            var actions = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(10, 5, 10, 5), BackColor = Ui.Surface };
            var paging = FilterRow(); var batch = FilterRow();
            paging.Controls.Add(Ui.GlassButton("上一页", () => { if (page > 0) { page--; Reload(); } }));
            paging.Controls.Add(Ui.GlassButton("下一页", () => { if ((page + 1) * PageSize < app.Store.Count(Query())) { page++; Reload(); } }));
            paging.Controls.Add(Ui.GlassButton("选中本页", () => { foreach (ListViewItem item in list.Items) item.Selected = true; }));
            status = new Label { AutoSize = true, Margin = new Padding(16, 10, 3, 3), ForeColor = Ui.Muted }; paging.Controls.Add(status);
            batch.Controls.Add(MenuButton("收藏…", new[] { MenuAction("收藏", () => Batch(x => app.Store.SetFavorite(x.Id, true))), MenuAction("取消收藏", () => Batch(x => app.Store.SetFavorite(x.Id, false))) }));
            batch.Controls.Add(MenuButton("标签…", new[] { MenuAction("设置标签", () => app.EditTags(Selected().Select(x => x.Id).ToArray())), MenuAction("追加标签", () => AddTags(false)), MenuAction("移除标签", () => AddTags(true)), MenuAction("管理标签", ManageTags) }));
            batch.Controls.Add(MenuButton("显示…", new[] { MenuAction("隐藏", () => Batch(x => app.Store.SetHidden(x.Id, true))), MenuAction("取消隐藏", () => Batch(x => app.Store.SetHidden(x.Id, false))) }));
            batch.Controls.Add(Ui.GlassButton("批量导出", ExportSelected));
            batch.Controls.Add(Ui.GlassButton("删除到回收站", () => app.Trash(Selected().Select(x => x.Id).ToArray())));
            batch.Controls.Add(Ui.GlassButton("恢复", RestoreSelected));
            batch.Controls.Add(Ui.GlassButton("立即释放空间", () => Purge(Selected().ToArray())));
            actions.Controls.Add(batch); actions.Controls.Add(paging);
            var storage = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0), Padding = new Padding(10, 7, 10, 7), BackColor = Ui.Surface };
            var storageTitle = new Label { Text = "存储管理", Dock = DockStyle.Top, Height = 25, Font = Ui.Font(10, FontStyle.Bold) };
            storagePath = new TextBox { Dock = DockStyle.Top, ReadOnly = true, BorderStyle = BorderStyle.FixedSingle };
            usage = new Label { Dock = DockStyle.Top, Height = 30, Padding = new Padding(0, 6, 0, 0), ForeColor = Ui.Muted };
            var tools = FilterRow();
            tools.Controls.Add(Ui.GlassButton("打开保存位置", () => app.Safe(() => Process.Start(new ProcessStartInfo(app.Store.RootPath) { UseShellExecute = true }))));
            tools.Controls.Add(Ui.GlassButton("清理最旧的未收藏截图…", PreviewCleanup));
            tools.Controls.Add(Ui.GlassButton("清空回收站…", () => app.Safe(() => Purge(app.Store.Query(new HistoryQuery { Filter = HistoryFilter.Trash, Limit = 100000 }).ToArray()))));
            tools.Controls.Add(Ui.GlassButton("迁移数据目录…", Migrate));
            storage.Controls.Add(tools); storage.Controls.Add(usage); storage.Controls.Add(storagePath); storage.Controls.Add(storageTitle);
            root.Controls.Add(search, 0, 0); root.Controls.Add(split, 0, 1); root.Controls.Add(actions, 0, 2); root.Controls.Add(storage, 0, 3);
            Controls.Add(root);
            Shown += (s, e) => Reload();
        }
        private static FlowLayoutPanel FilterRow(int height = 44) => new FlowLayoutPanel { Dock = DockStyle.Top, Height = height, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = false, Margin = Padding.Empty, BackColor = Color.Transparent };
        private static Label FilterLabel(string value) => new Label { Text = value, AutoSize = true, Margin = new Padding(6, 8, 3, 0), ForeColor = Ui.Muted };
        private static SoftEntry SearchEntry(int width) => new SoftEntry { Width = width, Height = 32, Margin = new Padding(3, 0, 3, 0) };
        private static SoftToggle FilterToggle(string title, bool value) => new SoftToggle { Text = title, Checked = value, AutoSize = true, Margin = new Padding(3, 2, 3, 2) };
        private static ToolStripMenuItem MenuAction(string title, Action action) => new ToolStripMenuItem(Localize.T(title), null, (s,e) => action());
        private Control MenuButton(string title, ToolStripMenuItem[] items)
        {
            var menu = new ContextMenuStrip(); menu.Items.AddRange(items); Ui.StyleMenu(menu);
            Control button = null;
            button = Ui.GlassButton(title, () => menu.Show(button, new Point(0, button.Height)));
            Disposed += (s,e) => menu.Dispose();
            return button;
        }
        private void FitHistoryColumns()
        {
            if (list.Columns.Count != 6) return;
            int width = Math.Max(600, list.ClientSize.Width - 4);
            bool compact = width < 730;
            int date = compact ? 205 : 220, stateWidth = compact ? 80 : 100;
            int imageSize = compact ? 80 : 100, bytes = compact ? 80 : 100, flags = compact ? 75 : 95;
            list.Columns[0].Width = date;
            list.Columns[1].Width = stateWidth;
            list.Columns[2].Width = Math.Max(80, width - date - stateWidth - imageSize - bytes - flags);
            list.Columns[3].Width = imageSize;
            list.Columns[4].Width = bytes;
            list.Columns[5].Width = flags;
        }
        private static void DrawHistoryHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var brush = new SolidBrush(Ui.Dark ? Color.FromArgb(42,42,44) : Color.FromArgb(228,234,242))) e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, Ui.Font(9, FontStyle.Bold), Rectangle.Inflate(e.Bounds, -8, 0), Ui.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (var pen = new Pen(Ui.Dark ? Color.FromArgb(69,69,72) : Color.FromArgb(201,210,222))) e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom-1, e.Bounds.Right, e.Bounds.Bottom-1);
        }
        private static void DrawHistoryCell(object sender, DrawListViewSubItemEventArgs e)
        {
            var fill = e.Item.Selected ? (Ui.Dark ? Color.FromArgb(69,69,73) : Color.FromArgb(215,231,250)) : Ui.Surface;
            using (var brush = new SolidBrush(fill)) e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, e.Item.Font, Rectangle.Inflate(e.Bounds, -7, 0), Ui.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            using (var pen = new Pen(Ui.Dark ? Color.FromArgb(48,48,50) : Color.FromArgb(225,230,236))) e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom-1, e.Bounds.Right, e.Bounds.Bottom-1);
        }
        private static SoftChoice Combo(string[] options) { var box = new SoftChoice { Width = 143, Height = 32, Margin = new Padding(3, 0, 3, 0) }; foreach (var option in options) box.Items.Add(option); box.SelectedIndex = 0; return box; }
        private List<HistoryItem> Selected() => list.SelectedItems.Cast<ListViewItem>().Select(x => (HistoryItem)x.Tag).ToList();
        private HistoryQuery Query() => new HistoryQuery
        {
            Filter = (HistoryFilter)filter.SelectedIndex, Status = (HistoryContentStatus)state.SelectedIndex, Text = text.Input.Text,
            Tags = tags.Input.Text.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray(), MatchAllTags = matchAll.Checked,
            DateFromUtc = fromEnabled.Checked ? (DateTime?)from.Value.Date.ToUniversalTime() : null,
            DateToUtc = toEnabled.Checked ? (DateTime?)to.Value.Date.AddDays(1).AddTicks(-1).ToUniversalTime() : null,
            MinSizeBytes = (long)(minSize.Value * 1024 * 1024), MaxSizeBytes = limitSize.Checked ? (long?)(maxSize.Value * 1024 * 1024) : null, SortField = sort.SelectedIndex == 2 ? HistorySortField.SizeBytes : HistorySortField.CreatedUtc,
            SortDirection = sort.SelectedIndex == 1 ? SortDirection.Ascending : SortDirection.Descending, Limit = PageSize, Offset = page * PageSize
        };
        public void Reload()
        {
            if (list == null || IsDisposed) return;
            app.Safe(() =>
            {
                var selected = new HashSet<string>(Selected().Select(x => x.Id));
                var query = Query(); int count = app.Store.Count(query); if (query.Offset >= count && page > 0) { page = Math.Max(0, (count - 1) / PageSize); query.Offset = page * PageSize; }
                list.BeginUpdate(); list.Items.Clear();
                foreach (var item in app.Store.Query(query))
                {
                    var row = new ListViewItem(item.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")) { Tag = item };
                    row.SubItems.Add(string.Join("、", PhotoStrip.Labels(item).Except(item.Tags).Select(Localize.T))); row.SubItems.Add(string.Join(", ", item.Tags)); row.SubItems.Add(item.Width + "×" + item.Height); row.SubItems.Add(FormatBytes(item.SizeBytes)); row.SubItems.Add((item.Favorite ? "★ " : "") + (item.Hidden ? Localize.T("隐藏") : "")); row.Selected = selected.Contains(item.Id); list.Items.Add(row);
                }
                list.EndUpdate(); status.Text = Localize.English ? $"{count} images · Page {page+1}" : $"{count} 张 · 第 {page + 1} 页";
                var space = app.Store.GetUsage(); usage.Text = Localize.English ? $"Total {FormatBytes(space.TotalBytes)}    Ordinary {FormatBytes(space.OrdinaryBytes)}    Favorites {FormatBytes(space.FavoriteBytes)}    Recycle bin {FormatBytes(space.TrashBytes)}    Cache/index {FormatBytes(space.CacheBytes)}" : $"总占用 {FormatBytes(space.TotalBytes)}    普通 {FormatBytes(space.OrdinaryBytes)}    收藏 {FormatBytes(space.FavoriteBytes)}    回收站 {FormatBytes(space.TrashBytes)}    缓存与索引 {FormatBytes(space.CacheBytes)}";
                storagePath.Text = app.Store.RootPath; Preview();
            });
        }
        public static string FormatBytes(long bytes) => bytes >= 1073741824 ? (bytes / 1073741824d).ToString("0.00") + " GB" : (bytes / 1048576d).ToString("0.00") + " MB";
        private void Preview() { preview.Image?.Dispose(); preview.Image = null; var item = Selected().FirstOrDefault(); previewHint.Visible = item == null; if (item != null) app.Safe(() => { using (var bitmap = Images.Load(item.CurrentPath)) { double scale = Math.Min(1, 700d / Math.Max(bitmap.Width, bitmap.Height)); preview.Image = new Bitmap(bitmap, Math.Max(1, (int)(bitmap.Width * scale)), Math.Max(1, (int)(bitmap.Height * scale))); } }); }
        private void Batch(Action<HistoryItem> change) => app.Safe(() => { foreach (var item in Selected()) change(item); app.Refresh(); });
        private void AddTags(bool remove)
        {
            var input = Ui.Prompt(remove ? "批量移除标签" : "批量追加标签", "多个标签以逗号分隔"); if (input == null) return;
            var values = input.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
            Batch(x => app.Store.SetTags(x.Id, remove ? x.Tags.Except(values, StringComparer.OrdinalIgnoreCase) : x.Tags.Concat(values)));
        }
        private void ManageTags()
        {
            var names = string.Join("、", app.Store.ListTags().Select(x => x.Name));
            var old = Ui.Prompt("管理标签", "现有：" + names + "\n输入要重命名或删除的标签"); if (string.IsNullOrWhiteSpace(old)) return;
            var next = Ui.Prompt("管理标签", "输入新名称；留空删除此标签（不删除图片）。", old); if (next == null) return;
            app.Safe(() => { if (string.IsNullOrWhiteSpace(next)) app.Store.DeleteTag(old); else app.Store.RenameTag(old, next); app.Refresh(); });
        }
        private void RestoreSelected() => app.Safe(() =>
        {
            var result = app.Store.Restore(Selected().Select(x => x.Id).ToArray(), DateTime.UtcNow, app.Settings.RetentionDays);
            if (result.ExpiredIds.Count > 0 && Ui.Confirm("其中 " + result.ExpiredIds.Count + " 张已超过保留天数，下次到期清理仍会删除。是否将它们收藏以保留？")) foreach (var id in result.ExpiredIds) app.Store.SetFavorite(id, true);
            app.Refresh();
        });
        private void ExportSelected() => app.Safe(() =>
        {
            using (var folder = new FolderBrowserDialog { Description = "选择批量导出目录" }) if (folder.ShowDialog() == DialogResult.OK)
                foreach (var item in Selected()) using (var image = Images.Load(item.CurrentPath)) Images.Export(image, Path.Combine(folder.SelectedPath, "ShotCab-" + item.CreatedUtc.ToLocalTime().ToString("yyyyMMdd-HHmmss") + "-" + item.Id.Substring(0, 8) + "." + app.Settings.ExportFormat), app.Settings.JpegQuality);
        });
        private void Purge(HistoryItem[] selected) => app.Safe(() =>
        {
            var targets = selected.Where(x => !app.BusyIds.Contains(x.Id)).ToArray(); if (targets.Length == 0) return;
            using (var previewForm = new CleanupPreview(targets)) if (previewForm.ShowDialog(this) == DialogResult.OK) { app.Store.Purge(previewForm.SelectedIds); app.Refresh(); }
        });
        private void PreviewCleanup() => app.Safe(() =>
        {
            var input = Ui.Prompt("清理最旧的未收藏截图", "预览最旧的多少张？下一步可逐张取消。", "50"); if (input == null) return;
            if (!int.TryParse(input, out int count) || count < 1 || count > 10000) throw new ArgumentException("请输入 1–10000。");
            var candidates = app.Store.GetOldestCandidates(count).Where(x => !app.BusyIds.Contains(x.Id)).Select(x => app.Store.Get(x.Id)).Where(x => x != null && !x.Favorite).ToArray();
            if (candidates.Length == 0) { app.Notify("没有可清理的未收藏记录。"); return; } Purge(candidates);
        });
        private void Migrate() => app.Safe(() =>
        {
            using (var folder = new FolderBrowserDialog { Description = "选择父目录，将新建 ShotCab.Data-Migrated 子目录" })
                if (folder.ShowDialog() == DialogResult.OK) { app.Migrate(Path.Combine(folder.SelectedPath, "ShotCab.Data-Migrated")); MessageBox.Show("数据已校验并切换到新目录。旧目录保留为备份，可确认后自行删除。", "ShotCab"); }
        });
        protected override void Dispose(bool disposing) { if (disposing) preview.Image?.Dispose(); base.Dispose(disposing); }
    }

    internal sealed class CleanupPreview : Form
    {
        private readonly CheckedListBox list; private readonly HistoryItem[] items; private readonly Label total;
        public string[] SelectedIds => list.CheckedIndices.Cast<int>().Select(i => items[i].Id).ToArray();
        public CleanupPreview(HistoryItem[] items)
        {
            this.items = items; Ui.Style(this); Text = "清理预览 · 永久释放空间"; Size = new Size(660, 510);
            list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, BackColor = Ui.Surface, ForeColor = Ui.Text, BorderStyle = BorderStyle.None };
            foreach (var item in items) list.Items.Add(item.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + "  " + HistoryForm.FormatBytes(item.SizeBytes) + "  " + string.Join(" / ", item.Tags) + (item.Favorite ? " ★收藏" : ""), true);
            total = new Label { Dock = DockStyle.Top, Height = 48, Padding = new Padding(12), ForeColor = Ui.Muted };
            var button = Ui.Button("永久删除选中项并释放空间", () => { if (SelectedIds.Length == 0) return; DialogResult = DialogResult.OK; Close(); }); button.Dock = DockStyle.Bottom;
            list.ItemCheck += (s, e) => BeginInvoke((Action)UpdateTotal); Controls.Add(list); Controls.Add(total); Controls.Add(button); UpdateTotal();
        }
        private void UpdateTotal() { total.Text = "将永久删除 " + list.CheckedItems.Count + " 张，预计释放 " + HistoryForm.FormatBytes(list.CheckedIndices.Cast<int>().Sum(i => items[i].SizeBytes)) + "。不进入回收站。"; }
    }
}
