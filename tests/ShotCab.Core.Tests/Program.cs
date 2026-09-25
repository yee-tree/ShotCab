using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ShotCab.Core;

internal static class Program
{
    private static int failures;

    private static int Main()
    {
        Run(nameof(SettingsDefaultsAndRoundTrip), SettingsDefaultsAndRoundTrip);
        Run(nameof(AddUpdateAndOcrAreVersionSafe), AddUpdateAndOcrAreVersionSafe);
        Run(nameof(QueryTagsTextFiltersAndSorting), QueryTagsTextFiltersAndSorting);
        Run(nameof(QueryPagingHiddenBakedAndCount), QueryPagingHiddenBakedAndCount);
        Run(nameof(StartupRemovesOnlySafeOrphanVersions), StartupRemovesOnlySafeOrphanVersions);
        Run(nameof(MigrationPreservesHistoryAndSource), MigrationPreservesHistoryAndSource);
        Run(nameof(TrashRestoreCleanupAndUsage), TrashRestoreCleanupAndUsage);
        Run(nameof(TagMaintenanceAndSafePurge), TagMaintenanceAndSafePurge);

        Console.WriteLine(failures == 0 ? "All ShotCab.Core tests passed." : failures + " test(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine("PASS " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("FAIL " + name + ": " + ex);
        }
    }

    private static void SettingsDefaultsAndRoundTrip()
    {
        WithTempRoot(root =>
        {
            var store = new SettingsStore(root);
            AppSettings value = store.Load();
            Equal(true, value.HistoryEnabled);
            Equal(7, value.RetentionDays);
            Equal(20, value.RecentCount);
            Equal(3, value.RecycleDays);
            Equal(2d, value.CapacityWarningGB);
            Equal(true, value.SidebarEnabled);
            Equal("zh-CN",value.Language);
            Equal(true, value.HideSidebarDuringCapture);
            Equal(false, value.EditAfterCapture);
            Equal(true, value.ShowCaptureResult);
            Equal(false, value.EditorCursorPreview);
            Equal(320, value.SidebarWidth);
            Equal(CardDragModifier.Alt, value.PreviewDragKey);
            Equal(SidebarDockSide.靠右, value.SidebarSide);
            Equal(.95d, value.SidebarOpacity);
            Equal(false, value.SidebarReserveSpace);
            Equal(0, value.SidebarScreen);
            Equal(false, value.SidebarAutoFold);
            Equal(0, value.FullscreenMode);
            Equal("png", value.ExportFormat);
            Equal(90, value.JpegQuality);
            Equal(false, value.EditAsNew);
            Equal(3, value.OcrPreviewSeconds);
            var autoOcr = typeof(AppSettings).GetProperty("AutoOcrOnEdit");
            True(autoOcr != null, "Auto OCR on edit setting is missing");
            Equal(false, autoOcr.GetValue(value));
            Equal("F6", value.CaptureShortcut);
            Equal("F7", value.PinShortcut);
            Equal("F8", value.SidebarShortcut);
            Equal("F9", value.OcrShortcut);
            Equal(false, value.StartWithWindows);

            value.RetentionDays = 0;
            value.SidebarSide = SidebarDockSide.靠左;
            value.SidebarWidth = 580;
            value.Language="en";
            value.HideSidebarDuringCapture = false;
            value.EditAfterCapture = true;
            value.ShowCaptureResult = false;
            value.EditorCursorPreview = true;
            value.PreviewDragKey = CardDragModifier.Ctrl;
            value.OcrExecutablePath = @"F:\tools\ocr.exe";
            autoOcr.SetValue(value, true);
            store.Save(value);
            AppSettings loaded = new SettingsStore(root).Load();
            Equal(0, loaded.RetentionDays);
            Equal(SidebarDockSide.靠左, loaded.SidebarSide);
            Equal(580, loaded.SidebarWidth);
            Equal("en",loaded.Language);
            Equal(false, loaded.HideSidebarDuringCapture);
            Equal(true, loaded.EditAfterCapture);
            Equal(false, loaded.ShowCaptureResult);
            Equal(true, loaded.EditorCursorPreview);
            Equal(CardDragModifier.Ctrl, loaded.PreviewDragKey);
            Equal(value.OcrExecutablePath, loaded.OcrExecutablePath);
            Equal(true, autoOcr.GetValue(loaded));
            True(File.Exists(Path.Combine(root, "settings.json")), "settings file was not written");

            string driveRoot = Path.GetPathRoot(root);
            Equal(driveRoot, new SettingsStore(driveRoot).RootPath);
        });
    }

    private static void QueryPagingHiddenBakedAndCount()
    {
        WithTempRoot(root =>
        {
            using (var store = new HistoryStore(root))
            {
                HistoryItem first = store.Add(Bytes("1"), Bytes("1"), "{}", false, false, false, 1, 1);
                HistoryItem hidden = store.Add(Bytes("22"), Bytes("22"), "{}", false, false, false, 1, 1);
                HistoryItem baked = store.Add(Bytes("333"), Bytes("333"), "{}", true, true, true, 1, 1);
                store.SetHidden(hidden.Id, true);
                store.SetTags(first.Id, new[] { "Work", "Alpha" });
                store.SetTags(hidden.Id, new[] { "work", "Beta" });
                store.SetTags(baked.Id, new[] { "WORK", "Gamma" });

                var query = new HistoryQuery
                {
                    Tags = new[] { "wOrK" },
                    ExcludeHidden = true,
                    SortField = HistorySortField.SizeBytes,
                    SortDirection = SortDirection.Ascending,
                    Offset = 1,
                    Limit = 1
                };
                Equal(2, store.Count(query));
                Equal(baked.Id, store.Query(query).Single().Id);
                Equal(hidden.Id, store.Query(new HistoryQuery { Filter = HistoryFilter.Hidden, ExcludeHidden = true }).Single().Id);
                Equal(baked.Id, store.Query(new HistoryQuery { Status = HistoryContentStatus.RedactionBaked }).Single().Id);
                Equal(1, store.Count(new HistoryQuery { Text = "ALP", Tags = new[] { "work" } }));
                Equal(2, store.Count(new HistoryQuery { Tags = new[] { "alpha", "gamma" }, MatchAllTags = false }));
            }
        });
    }

    private static void StartupRemovesOnlySafeOrphanVersions()
    {
        WithTempRoot(root =>
        {
            string liveId;
            string liveVersion;
            string oldVersion;
            string failedItem = Guid.NewGuid().ToString("D");
            string nonGuid = Path.Combine(root, "items", "keep-me");
            using (var store = new HistoryStore(root))
            {
                HistoryItem item = store.Add(Bytes("secret-original"), Bytes("redacted"), "{}", false, true, false, 1, 1);
                liveId = item.Id;
                oldVersion = Path.GetDirectoryName(item.CurrentPath);
                item = store.Update(item.Id, Bytes("redacted"), Bytes("redacted"), "{}", true, true, true, 1, 1);
                liveVersion = Path.GetDirectoryName(item.CurrentPath);
                True(!Directory.Exists(oldVersion), "permanent redaction left the old version on disk");
            }

            string obsolete = Path.Combine(root, "items", liveId, "obsolete");
            string failed = Path.Combine(root, "items", failedItem, "failed");
            Directory.CreateDirectory(obsolete);
            Directory.CreateDirectory(failed);
            Directory.CreateDirectory(nonGuid);
            File.WriteAllText(Path.Combine(obsolete, "original.png"), "secret");
            File.WriteAllText(Path.Combine(failed, "original.png"), "partial");

            using (var reopened = new HistoryStore(root))
                Equal(liveVersion, Path.GetDirectoryName(reopened.Get(liveId).CurrentPath));

            True(!Directory.Exists(obsolete), "obsolete live-item version survived startup recovery");
            True(!Directory.Exists(Path.Combine(root, "items", failedItem)), "failed GUID item survived startup recovery");
            True(Directory.Exists(nonGuid), "startup recovery removed an unknown directory");
        });
    }

    private static void MigrationPreservesHistoryAndSource()
    {
        WithTempRoot(root =>
        {
            string destination = root + "-migrated";
            try
            {
                using (var source = new HistoryStore(root))
                {
                    HistoryItem item = source.Add(Bytes("original"), Bytes("current"), "{}", false, false, false, 2, 3);
                    source.SetTags(item.Id, new[] { "migration" });
                    HistoryMigrationResult result = source.MigrateTo(destination);
                    using (result.Store)
                    {
                        Equal(item.Id, result.Store.Query(new HistoryQuery { Tags = new[] { "MIGRATION" } }).Single().Id);
                        Equal("current", File.ReadAllText(result.Store.Get(item.Id).CurrentPath));
                    }
                    Equal(item.Id, source.Get(item.Id).Id);
                }
            }
            finally
            {
                if (Directory.Exists(destination)) Directory.Delete(destination, true);
            }
        });
    }

    private static void AddUpdateAndOcrAreVersionSafe()
    {
        WithTempRoot(root =>
        {
            using (var store = new HistoryStore(root))
            {
                HistoryItem item = store.Add(Bytes("original-1"), Bytes("rendered-1"), "{\"v\":1}", false, false, false, 120, 80);
                Guid parsed;
                True(Guid.TryParse(item.Id, out parsed), "id is not a GUID");
                True(File.Exists(item.OriginalPath), "original missing");
                True(File.Exists(item.CurrentPath), "current missing");
                True(File.Exists(item.DocumentPath), "document missing");
                Equal("original-1", File.ReadAllText(item.OriginalPath));
                Equal("rendered-1", File.ReadAllText(item.CurrentPath));

                DateTime created = item.CreatedUtc;
                DateTime beforeUpdate = item.UpdatedUtc;
                True(store.SetOcr(item.Id, beforeUpdate, "first OCR"), "fresh OCR write rejected");
                item = store.Update(item.Id, Bytes("original-2"), Bytes("rendered-2"), "{\"v\":2}", true, true, false, 200, 100);
                Equal(created, item.CreatedUtc);
                Equal(item.Id, store.Get(item.Id).Id);
                Equal(null, item.OcrText);
                Equal("original-2", File.ReadAllText(item.OriginalPath));
                True(!store.SetOcr(item.Id, beforeUpdate, "stale OCR"), "stale OCR write was accepted");
                True(store.SetOcr(item.Id, item.UpdatedUtc, "fresh OCR"), "fresh OCR write rejected");
                Equal("fresh OCR", store.Get(item.Id).OcrText);

                HistoryItem copy = store.Update(item.Id, Bytes("copy-o"), Bytes("copy-r"), "{}", true, false, false, 1, 2, true);
                True(copy.Id != item.Id, "as-new update reused the id");
            }
        });
    }

    private static void QueryTagsTextFiltersAndSorting()
    {
        WithTempRoot(root =>
        {
            using (var store = new HistoryStore(root))
            {
                HistoryItem first = store.Add(Bytes("o1"), Bytes("r1"), "{}", false, false, false, 10, 20);
                HistoryItem second = store.Add(Bytes("o222"), Bytes("r222"), "{}", true, true, true, 20, 30);
                store.SetTags(first.Id, new[] { "work", "alpha" });
                store.SetTags(second.Id, new[] { "work", "beta" });
                store.SetFavorite(second.Id, true);
                store.SetHidden(first.Id, true);
                store.SetOcr(second.Id, store.Get(second.Id).UpdatedUtc, "invoice heliotrope");

                Equal(1, store.Query(new HistoryQuery { Filter = HistoryFilter.Favorite }).Count);
                Equal(first.Id, store.Query(new HistoryQuery { Filter = HistoryFilter.Hidden }).Single().Id);
                Equal(second.Id, store.Query(new HistoryQuery { Text = "heliotrope" }).Single().Id);
                Equal(2, store.Count(new HistoryQuery { Tags = new[] { "work" } }));
                Equal(1, store.Count(new HistoryQuery { Tags = new[] { "work", "alpha" }, MatchAllTags = true }));
                Equal(second.Id, store.Query(new HistoryQuery { Status = HistoryContentStatus.Redacted }).Single().Id);
                Equal(second.Id, store.Query(new HistoryQuery { SortField = HistorySortField.SizeBytes, SortDirection = SortDirection.Descending, Limit = 1 }).Single().Id);
                True(store.Get(second.Id).StatusLabels.Contains("已打码"), "calculated status label missing");
            }
        });
    }

    private static void TrashRestoreCleanupAndUsage()
    {
        WithTempRoot(root =>
        {
            using (var store = new HistoryStore(root))
            {
                HistoryItem old = store.Add(Bytes("old-o"), Bytes("old-r"), "{}", false, false, false, 1, 1);
                HistoryItem favorite = store.Add(Bytes("fav-o"), Bytes("fav-r"), "{}", false, false, false, 1, 1);
                store.SetFavorite(favorite.Id, true);
                DateTime now = DateTime.UtcNow.AddDays(10);
                store.Trash(new[] { old.Id }, now.AddDays(-4));
                string missing = Guid.NewGuid().ToString("D");
                RestoreResult restored = store.Restore(new[] { old.Id, missing }, now, 7);
                True(restored.RestoredIds.Contains(old.Id), "trash item was not restored");
                True(restored.ExpiredIds.Contains(old.Id), "expired restore was not reported");
                Equal(missing, restored.MissingIds.Single());
                Equal(old.CreatedUtc, store.Get(old.Id).CreatedUtc);

                store.Trash(new[] { old.Id }, now.AddDays(-4));
                CleanupResult result = store.Cleanup(now, 1, 3, new HashSet<string> { old.Id });
                True(result.SkippedBusyIds.Contains(old.Id), "busy item was not skipped");
                True(store.Get(favorite.Id) != null, "favorite was removed by retention");
                result = store.Cleanup(now, 1, 3, new HashSet<string>());
                True(result.PurgedIds.Contains(old.Id), "expired trash was not purged");
                HistoryItem expiring = store.Add(Bytes("expire-o"), Bytes("expire-r"), "{}", false, false, false, 1, 1);
                store.Cleanup(now, 1, 3, new HashSet<string>());
                True(store.Get(expiring.Id) == null, "automatic expiry must release files directly, not put them in trash");
                store.Trash(new[] { favorite.Id }, now.AddDays(-4));
                store.Cleanup(now, 1, 3, new HashSet<string>());
                True(store.Get(favorite.Id) == null, "manually deleted favorite must expire from trash");

                HistoryUsage usage = store.GetUsage();
                Equal(usage.TotalBytes, usage.OrdinaryBytes + usage.FavoriteBytes + usage.TrashBytes + usage.CacheBytes);
                True(store.GetOldestCandidates(10).All(x => !x.Favorite && x.DeletedUtc == null), "oldest candidate rules were ignored");
            }
        });
    }

    private static void TagMaintenanceAndSafePurge()
    {
        WithTempRoot(root =>
        {
            using (var store = new HistoryStore(root))
            {
                HistoryItem item = store.Add(Bytes("o"), Bytes("r"), "{}", false, false, false, 1, 1);
                store.SetTags(item.Id, new[] { "One", "Two", "one" });
                Equal(2, store.ListTags().Count);
                Equal(1, store.RenameTag("one", "Primary"));
                True(store.Get(item.Id).Tags.Contains("Primary"), "renamed tag missing");
                Equal(1, store.DeleteTag("Two"));
                Equal("Primary", store.ListTags().Single().Name);

                string versionDirectory = Path.GetDirectoryName(store.Get(item.Id).CurrentPath);
                store.Purge(new[] { item.Id, "..\\outside" });
                Equal(null, store.Get(item.Id));
                True(!Directory.Exists(versionDirectory), "purged version directory remains");
                True(Directory.Exists(root), "purge escaped the root");
            }
        });
    }

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    private static void WithTempRoot(Action<string> action)
    {
        string root = Path.Combine(Path.GetTempPath(), "ShotCab.Core.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { action(root); }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException("Expected <" + expected + ">, got <" + actual + ">.");
    }

    private static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
