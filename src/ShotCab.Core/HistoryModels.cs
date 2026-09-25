using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ShotCab.Core
{
    public enum HistoryFilter
    {
        All,
        Hidden,
        Favorite,
        Trash
    }

    public enum HistoryContentStatus
    {
        Any,
        Original,
        Edited,
        Redacted,
        OcrAvailable,
        RedactionBaked
    }

    public enum HistorySortField
    {
        CreatedUtc,
        UpdatedUtc,
        SizeBytes
    }

    public enum SortDirection
    {
        Ascending,
        Descending
    }

    public sealed class HistoryQuery
    {
        public string Text { get; set; }
        public HistoryFilter Filter { get; set; } = HistoryFilter.All;
        public IReadOnlyCollection<string> Tags { get; set; } = Array.Empty<string>();
        public bool MatchAllTags { get; set; } = true;
        public bool ExcludeHidden { get; set; }
        public int Offset { get; set; }
        public int Limit { get; set; } = 100;
        public HistoryContentStatus Status { get; set; } = HistoryContentStatus.Any;
        public DateTime? DateFromUtc { get; set; }
        public DateTime? DateToUtc { get; set; }
        public long? MinSizeBytes { get; set; }
        public long? MaxSizeBytes { get; set; }
        public HistorySortField SortField { get; set; } = HistorySortField.CreatedUtc;
        public SortDirection SortDirection { get; set; } = SortDirection.Descending;
    }

    public sealed class HistoryItem
    {
        public string Id { get; internal set; }
        public DateTime CreatedUtc { get; internal set; }
        public DateTime UpdatedUtc { get; internal set; }
        public bool Hidden { get; internal set; }
        public bool Favorite { get; internal set; }
        public DateTime? DeletedUtc { get; internal set; }
        public List<string> Tags { get; internal set; } = new List<string>();
        public bool Edited { get; internal set; }
        public bool Redacted { get; internal set; }
        public bool RedactionBaked { get; internal set; }
        public string OcrText { get; internal set; }
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public long SizeBytes { get; internal set; }
        public string CurrentPath { get; internal set; }
        public string OriginalPath { get; internal set; }
        public string DocumentPath { get; internal set; }

        public IReadOnlyList<string> StatusLabels
        {
            get
            {
                var labels = new List<string>();
                if (Edited) labels.Add("已编辑");
                if (Redacted) labels.Add("已打码");
                if (RedactionBaked) labels.Add("已固化");
                if (labels.Count == 0) labels.Add("原图");
                return new ReadOnlyCollection<string>(labels);
            }
        }
    }

    public sealed class TagInfo
    {
        public string Name { get; internal set; }
        public int Count { get; internal set; }
    }

    public sealed class RestoreResult
    {
        public IReadOnlyList<string> RestoredIds { get; internal set; } = Array.Empty<string>();
        public IReadOnlyList<string> ExpiredIds { get; internal set; } = Array.Empty<string>();
        public IReadOnlyList<string> MissingIds { get; internal set; } = Array.Empty<string>();
    }

    public sealed class CleanupResult
    {
        public IReadOnlyList<string> TrashedIds { get; internal set; } = Array.Empty<string>();
        public IReadOnlyList<string> PurgedIds { get; internal set; } = Array.Empty<string>();
        public IReadOnlyList<string> SkippedBusyIds { get; internal set; } = Array.Empty<string>();
    }

    public sealed class HistoryUsage
    {
        public long OrdinaryBytes { get; internal set; }
        public long FavoriteBytes { get; internal set; }
        public long TrashBytes { get; internal set; }
        public long CacheBytes { get; internal set; }
        public long CurrentBytes { get; internal set; }
        public long OriginalBytes { get; internal set; }
        public long DocumentBytes { get; internal set; }
        public long DatabaseBytes { get; internal set; }
        public long OtherBytes { get; internal set; }
        public long TotalBytes => OrdinaryBytes + FavoriteBytes + TrashBytes + CacheBytes;
    }

    public sealed class HistoryStorageCandidate
    {
        public string Id { get; internal set; }
        public DateTime CreatedUtc { get; internal set; }
        public long SizeBytes { get; internal set; }
        public bool Favorite { get; internal set; }
        public bool Hidden { get; internal set; }
        public DateTime? DeletedUtc { get; internal set; }
    }

    public sealed class HistoryMigrationResult
    {
        public string DestinationRoot { get; internal set; }
        public int FileCount { get; internal set; }
        public long BytesCopied { get; internal set; }
        public HistoryStore Store { get; internal set; }
    }
}
