using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace ShotCab.Core
{
    public sealed class HistoryStore : IDisposable
    {
        private const int SchemaVersion = 1;
        private readonly object gate = new object();
        private readonly string databasePath;
        private bool disposed;

        public HistoryStore(string root)
        {
            RootPath = StoragePath.NormalizeRoot(root);
            Directory.CreateDirectory(RootPath);
            databasePath = Path.Combine(RootPath, "history.db");
            InitializeDatabase();
            RecoverOrphanVersions();
        }

        public string RootPath { get; }
        public string DatabasePath => databasePath;

        public HistoryItem Add(byte[] originalPng, byte[] renderedPng, string documentJson, bool edited, bool redacted,
            bool baked, int width, int height)
        {
            ValidatePayload(originalPng, renderedPng, width, height);
            lock (gate)
            {
                ThrowIfDisposed();
                string id = Guid.NewGuid().ToString("D");
                DateTime now = DateTime.UtcNow;
                return AddCore(id, now, originalPng, renderedPng, documentJson, edited, redacted, baked, width, height);
            }
        }

        public HistoryItem Update(string id, byte[] originalPng, byte[] renderedPng, string documentJson, bool edited,
            bool redacted, bool baked, int width, int height, bool asNew = false)
        {
            ValidatePayload(originalPng, renderedPng, width, height);
            lock (gate)
            {
                ThrowIfDisposed();
                if (asNew) return AddCore(Guid.NewGuid().ToString("D"), DateTime.UtcNow, originalPng, renderedPng,
                    documentJson, edited, redacted, baked, width, height);

                string normalizedId = NormalizeId(id);
                HistoryItem existing = GetCore(normalizedId);
                if (existing == null) throw new KeyNotFoundException("History item was not found: " + id);

                DateTime updated = NextUtc(existing.UpdatedUtc);
                VersionFiles files = WriteVersion(normalizedId, updated, originalPng, renderedPng, documentJson);
                bool committed = false;
                try
                {
                    using (SqliteConnection connection = OpenConnection())
                    using (SqliteTransaction transaction = connection.BeginTransaction())
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"UPDATE items SET updated_utc=$updated, edited=$edited, redacted=$redacted,
                            redaction_baked=$baked, ocr_text=NULL, width=$width, height=$height, size_bytes=$size,
                            current_rel=$current, original_rel=$original, document_rel=$document WHERE id=$id";
                        command.Parameters.AddWithValue("$updated", updated.Ticks);
                        command.Parameters.AddWithValue("$edited", edited ? 1 : 0);
                        command.Parameters.AddWithValue("$redacted", redacted ? 1 : 0);
                        command.Parameters.AddWithValue("$baked", baked ? 1 : 0);
                        command.Parameters.AddWithValue("$width", width);
                        command.Parameters.AddWithValue("$height", height);
                        command.Parameters.AddWithValue("$size", files.SizeBytes);
                        command.Parameters.AddWithValue("$current", files.CurrentRelative);
                        command.Parameters.AddWithValue("$original", files.OriginalRelative);
                        command.Parameters.AddWithValue("$document", files.DocumentRelative);
                        command.Parameters.AddWithValue("$id", normalizedId);
                        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("History item changed during update.");
                        transaction.Commit();
                        committed = true;
                    }
                }
                finally
                {
                    if (!committed) DeleteDirectoryIfPresent(files.VersionDirectory);
                }

                DeleteOldVersion(existing);
                return GetCore(normalizedId);
            }
        }

        public HistoryItem Get(string id)
        {
            lock (gate)
            {
                ThrowIfDisposed();
                string normalizedId;
                if (!TryNormalizeId(id, out normalizedId)) return null;
                return GetCore(normalizedId);
            }
        }

        public IReadOnlyList<HistoryItem> Query(HistoryQuery query)
        {
            lock (gate)
            {
                ThrowIfDisposed();
                query = query ?? new HistoryQuery();
                return QueryCore(query);
            }
        }

        public int Count(HistoryQuery query)
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return CountCore(query ?? new HistoryQuery());
            }
        }

        public bool SetHidden(string id, bool hidden) => SetBoolean(id, "hidden", hidden);
        public bool SetFavorite(string id, bool favorite) => SetBoolean(id, "favorite", favorite);

        public bool SetTags(string id, IEnumerable<string> tags)
        {
            if (tags == null) throw new ArgumentNullException(nameof(tags));
            List<string> normalized = NormalizeTags(tags);
            lock (gate)
            {
                ThrowIfDisposed();
                string normalizedId;
                if (!TryNormalizeId(id, out normalizedId)) return false;
                using (SqliteConnection connection = OpenConnection())
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "UPDATE items SET tags_json=$tags WHERE id=$id";
                    command.Parameters.AddWithValue("$tags", SerializeTags(normalized));
                    command.Parameters.AddWithValue("$id", normalizedId);
                    return command.ExecuteNonQuery() == 1;
                }
            }
        }

        public int RenameTag(string oldName, string newName)
        {
            string oldTag = NormalizeTag(oldName);
            string newTag = NormalizeTag(newName);
            lock (gate)
            {
                ThrowIfDisposed();
                List<HistoryItem> items = ReadAllItems();
                int changed = 0;
                using (SqliteConnection connection = OpenConnection())
                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    foreach (HistoryItem item in items)
                    {
                        if (!item.Tags.Any(x => string.Equals(x, oldTag, StringComparison.OrdinalIgnoreCase))) continue;
                        List<string> tags = item.Tags.Select(x => string.Equals(x, oldTag, StringComparison.OrdinalIgnoreCase) ? newTag : x)
                            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        UpdateTags(connection, transaction, item.Id, tags);
                        changed++;
                    }
                    transaction.Commit();
                }
                return changed;
            }
        }

        public int DeleteTag(string name)
        {
            string tag = NormalizeTag(name);
            lock (gate)
            {
                ThrowIfDisposed();
                List<HistoryItem> items = ReadAllItems();
                int changed = 0;
                using (SqliteConnection connection = OpenConnection())
                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    foreach (HistoryItem item in items)
                    {
                        List<string> tags = item.Tags.Where(x => !string.Equals(x, tag, StringComparison.OrdinalIgnoreCase)).ToList();
                        if (tags.Count == item.Tags.Count) continue;
                        UpdateTags(connection, transaction, item.Id, tags);
                        changed++;
                    }
                    transaction.Commit();
                }
                return changed;
            }
        }

        public IReadOnlyList<TagInfo> ListTags()
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return ReadAllItems().SelectMany(x => x.Tags)
                    .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new TagInfo { Name = x.First(), Count = x.Count() })
                    .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            }
        }

        public int Trash(IEnumerable<string> ids, DateTime nowUtc)
        {
            return SetDeleted(ids, EnsureUtc(nowUtc).Ticks, false);
        }

        public RestoreResult Restore(IEnumerable<string> ids, DateTime? nowUtc = null, int retentionDays = 7)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            DateTime now = EnsureUtc(nowUtc ?? DateTime.UtcNow);
            HashSet<string> requested = NormalizeIds(ids);
            var restored = new List<string>();
            var expired = new List<string>();
            var missing = new List<string>();
            lock (gate)
            {
                ThrowIfDisposed();
                using (SqliteConnection connection = OpenConnection())
                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    foreach (string id in requested)
                    {
                        HistoryItem item = GetCore(connection, id);
                        if (item == null)
                        {
                            missing.Add(id);
                            continue;
                        }
                        if (item.DeletedUtc.HasValue)
                        {
                            using (SqliteCommand command = connection.CreateCommand())
                            {
                                command.Transaction = transaction;
                                command.CommandText = "UPDATE items SET deleted_utc=NULL WHERE id=$id";
                                command.Parameters.AddWithValue("$id", id);
                                command.ExecuteNonQuery();
                            }
                            restored.Add(id);
                            if (retentionDays > 0 && item.CreatedUtc.AddDays(retentionDays) <= now) expired.Add(id);
                        }
                    }
                    transaction.Commit();
                }
            }
            return new RestoreResult { RestoredIds = restored, ExpiredIds = expired, MissingIds = missing };
        }

        public int Purge(IEnumerable<string> ids)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            HashSet<string> normalized = NormalizeIds(ids);
            lock (gate)
            {
                ThrowIfDisposed();
                var existingIds = new List<string>();
                using (SqliteConnection connection = OpenConnection())
                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    foreach (string id in normalized)
                    {
                        using (SqliteCommand command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = "DELETE FROM items WHERE id=$id";
                            command.Parameters.AddWithValue("$id", id);
                            if (command.ExecuteNonQuery() == 1) existingIds.Add(id);
                        }
                    }
                    transaction.Commit();
                }
                foreach (string id in existingIds)
                    DeleteDirectoryIfPresent(StoragePath.ResolveUnderRoot(RootPath, Path.Combine("items", id)));
                return existingIds.Count;
            }
        }

        public CleanupResult Cleanup(DateTime nowUtc, int retentionDays, int recycleDays, ISet<string> busyIds)
        {
            if (retentionDays < 0) throw new ArgumentOutOfRangeException(nameof(retentionDays));
            if (recycleDays < 0) throw new ArgumentOutOfRangeException(nameof(recycleDays));
            DateTime now = EnsureUtc(nowUtc);
            HashSet<string> busy = NormalizeIds(busyIds ?? new HashSet<string>());
            var trashed = new List<string>();
            var purged = new List<string>();
            var skipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            lock (gate)
            {
                ThrowIfDisposed();
                List<HistoryItem> items = ReadAllItems();
                foreach (HistoryItem item in items)
                {
                    bool oldTrash = item.DeletedUtc.HasValue && item.DeletedUtc.Value.AddDays(recycleDays) <= now;
                    bool oldLive = !item.DeletedUtc.HasValue && retentionDays > 0 && item.CreatedUtc.AddDays(retentionDays) <= now;
                    if (!oldTrash && !oldLive) continue;
                    if (item.Favorite && !item.DeletedUtc.HasValue) continue;
                    if (busy.Contains(item.Id))
                    {
                        skipped.Add(item.Id);
                        continue;
                    }
                    purged.Add(item.Id);
                }
                if (trashed.Count > 0) Trash(trashed, now);
                if (purged.Count > 0) Purge(purged);
            }
            return new CleanupResult
            {
                TrashedIds = trashed,
                PurgedIds = purged,
                SkippedBusyIds = skipped.ToList()
            };
        }

        public bool SetOcr(string id, DateTime expectedUpdatedUtc, string text)
        {
            lock (gate)
            {
                ThrowIfDisposed();
                string normalizedId;
                if (!TryNormalizeId(id, out normalizedId)) return false;
                using (SqliteConnection connection = OpenConnection())
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "UPDATE items SET ocr_text=$text WHERE id=$id AND updated_utc=$updated";
                    command.Parameters.AddWithValue("$text", (object)text ?? DBNull.Value);
                    command.Parameters.AddWithValue("$id", normalizedId);
                    command.Parameters.AddWithValue("$updated", EnsureUtc(expectedUpdatedUtc).Ticks);
                    return command.ExecuteNonQuery() == 1;
                }
            }
        }

        public HistoryUsage GetUsage()
        {
            lock (gate)
            {
                ThrowIfDisposed();
                var usage = new HistoryUsage();
                var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (HistoryItem item in ReadAllItems())
                {
                    long itemBytes = 0;
                    itemBytes += AddFileSize(item.CurrentPath, referenced, value => usage.CurrentBytes += value);
                    itemBytes += AddFileSize(item.OriginalPath, referenced, value => usage.OriginalBytes += value);
                    itemBytes += AddFileSize(item.DocumentPath, referenced, value => usage.DocumentBytes += value);
                    if (item.DeletedUtc.HasValue) usage.TrashBytes += itemBytes;
                    else if (item.Favorite) usage.FavoriteBytes += itemBytes;
                    else usage.OrdinaryBytes += itemBytes;
                }

                foreach (string path in EnumerateFilesSafely(RootPath))
                {
                    string full = Path.GetFullPath(path);
                    if (referenced.Contains(full)) continue;
                    long length = new FileInfo(full).Length;
                    if (IsDatabaseFile(full)) usage.DatabaseBytes += length;
                    else usage.OtherBytes += length;
                    usage.CacheBytes += length;
                }
                return usage;
            }
        }

        public IReadOnlyList<HistoryStorageCandidate> GetOldestCandidates(int limit)
        {
            if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit));
            lock (gate)
            {
                ThrowIfDisposed();
                return ReadAllItems().Where(x => !x.Favorite && !x.DeletedUtc.HasValue)
                    .OrderBy(x => x.CreatedUtc).Take(limit)
                    .Select(x => new HistoryStorageCandidate
                    {
                        Id = x.Id,
                        CreatedUtc = x.CreatedUtc,
                        SizeBytes = x.SizeBytes,
                        Favorite = x.Favorite,
                        Hidden = x.Hidden,
                        DeletedUtc = x.DeletedUtc
                    }).ToList();
            }
        }

        public HistoryMigrationResult MigrateTo(string destinationRoot)
        {
            string destination = StoragePath.NormalizeRoot(destinationRoot);
            string rootPrefix = RootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string destinationPrefix = destination.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (destination.Equals(RootPath, StringComparison.OrdinalIgnoreCase) ||
                destinationPrefix.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ||
                rootPrefix.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Migration roots may not contain one another.", nameof(destinationRoot));
            if (Directory.Exists(destination) || File.Exists(destination))
                throw new IOException("Migration destination already exists: " + destination);

            string parent = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(parent);
            string stage = Path.Combine(parent, "." + Path.GetFileName(destination) + ".migration-" + Guid.NewGuid().ToString("N"));
            int count = 0;
            long bytes = 0;
            lock (gate)
            {
                ThrowIfDisposed();
                CheckpointDatabase();
                try
                {
                    Directory.CreateDirectory(stage);
                    foreach (string source in EnumerateFilesSafely(RootPath))
                    {
                        string relative = MakeRelativePath(RootPath, source);
                        if (relative.EndsWith("history.db-wal", StringComparison.OrdinalIgnoreCase) ||
                            relative.EndsWith("history.db-shm", StringComparison.OrdinalIgnoreCase)) continue;
                        string target = StoragePath.ResolveUnderRoot(stage, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        File.Copy(source, target, false);
                        VerifyFileHash(source, target);
                        count++;
                        bytes += new FileInfo(source).Length;
                    }
                    Directory.Move(stage, destination);
                }
                finally
                {
                    if (Directory.Exists(stage)) Directory.Delete(stage, true);
                }
            }
            return new HistoryMigrationResult
            {
                DestinationRoot = destination,
                FileCount = count,
                BytesCopied = bytes,
                Store = new HistoryStore(destination)
            };
        }

        public void Dispose()
        {
            lock (gate) disposed = true;
        }

        private HistoryItem AddCore(string id, DateTime createdUtc, byte[] originalPng, byte[] renderedPng,
            string documentJson, bool edited, bool redacted, bool baked, int width, int height)
        {
            VersionFiles files = WriteVersion(id, createdUtc, originalPng, renderedPng, documentJson);
            bool committed = false;
            try
            {
                using (SqliteConnection connection = OpenConnection())
                using (SqliteTransaction transaction = connection.BeginTransaction())
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"INSERT INTO items
                        (id,created_utc,updated_utc,hidden,favorite,deleted_utc,tags_json,edited,redacted,redaction_baked,
                         ocr_text,width,height,size_bytes,current_rel,original_rel,document_rel)
                        VALUES($id,$created,$updated,0,0,NULL,'[]',$edited,$redacted,$baked,NULL,$width,$height,$size,$current,$original,$document)";
                    command.Parameters.AddWithValue("$id", id);
                    command.Parameters.AddWithValue("$created", createdUtc.Ticks);
                    command.Parameters.AddWithValue("$updated", createdUtc.Ticks);
                    command.Parameters.AddWithValue("$edited", edited ? 1 : 0);
                    command.Parameters.AddWithValue("$redacted", redacted ? 1 : 0);
                    command.Parameters.AddWithValue("$baked", baked ? 1 : 0);
                    command.Parameters.AddWithValue("$width", width);
                    command.Parameters.AddWithValue("$height", height);
                    command.Parameters.AddWithValue("$size", files.SizeBytes);
                    command.Parameters.AddWithValue("$current", files.CurrentRelative);
                    command.Parameters.AddWithValue("$original", files.OriginalRelative);
                    command.Parameters.AddWithValue("$document", files.DocumentRelative);
                    command.ExecuteNonQuery();
                    transaction.Commit();
                    committed = true;
                }
            }
            finally
            {
                if (!committed) DeleteDirectoryIfPresent(files.VersionDirectory);
            }
            return GetCore(id);
        }

        private List<HistoryItem> QueryCore(HistoryQuery query)
        {
            using (SqliteConnection connection = OpenConnection())
            using (SqliteCommand command = connection.CreateCommand())
            {
                string where = BuildQueryWhere(command, query);
                string sortColumn;
                switch (query.SortField)
                {
                    case HistorySortField.CreatedUtc: sortColumn = "created_utc"; break;
                    case HistorySortField.UpdatedUtc: sortColumn = "updated_utc"; break;
                    case HistorySortField.SizeBytes: sortColumn = "size_bytes"; break;
                    default: throw new ArgumentOutOfRangeException(nameof(query.SortField));
                }
                string direction;
                switch (query.SortDirection)
                {
                    case SortDirection.Ascending: direction = "ASC"; break;
                    case SortDirection.Descending: direction = "DESC"; break;
                    default: throw new ArgumentOutOfRangeException(nameof(query.SortDirection));
                }
                command.CommandText = SelectItems + where + " ORDER BY " + sortColumn + " " + direction + ", id ASC LIMIT $limit OFFSET $offset";
                command.Parameters.AddWithValue("$limit", Math.Max(0, query.Limit));
                command.Parameters.AddWithValue("$offset", Math.Max(0, query.Offset));
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    var items = new List<HistoryItem>();
                    while (reader.Read()) items.Add(ReadItem(reader));
                    return items;
                }
            }
        }

        private int CountCore(HistoryQuery query)
        {
            using (SqliteConnection connection = OpenConnection())
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM items" + BuildQueryWhere(command, query);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private static string BuildQueryWhere(SqliteCommand command, HistoryQuery query)
        {
            var clauses = new List<string>();
            switch (query.Filter)
            {
                case HistoryFilter.All: clauses.Add("deleted_utc IS NULL"); break;
                case HistoryFilter.Hidden: clauses.Add("hidden=1 AND deleted_utc IS NULL"); break;
                case HistoryFilter.Favorite: clauses.Add("favorite=1 AND deleted_utc IS NULL"); break;
                case HistoryFilter.Trash: clauses.Add("deleted_utc IS NOT NULL"); break;
                default: throw new ArgumentOutOfRangeException(nameof(query.Filter));
            }
            if (query.ExcludeHidden && query.Filter != HistoryFilter.Hidden) clauses.Add("hidden=0");
            if (!string.IsNullOrWhiteSpace(query.Text))
            {
                command.Parameters.AddWithValue("$text", query.Text.Trim());
                clauses.Add("(instr(lower(coalesce(ocr_text,'')),lower($text))>0 OR EXISTS (SELECT 1 FROM json_each(items.tags_json) WHERE instr(lower(CAST(value AS TEXT)),lower($text))>0))");
            }
            List<string> tags = NormalizeTags(query.Tags ?? Array.Empty<string>());
            if (tags.Count > 0)
            {
                var tagClauses = new List<string>();
                for (int i = 0; i < tags.Count; i++)
                {
                    string parameter = "$tag" + i.ToString(CultureInfo.InvariantCulture);
                    command.Parameters.AddWithValue(parameter, tags[i]);
                    tagClauses.Add("EXISTS (SELECT 1 FROM json_each(items.tags_json) WHERE lower(CAST(value AS TEXT))=lower(" + parameter + "))");
                }
                clauses.Add("(" + string.Join(query.MatchAllTags ? " AND " : " OR ", tagClauses) + ")");
            }
            switch (query.Status)
            {
                case HistoryContentStatus.Any: break;
                case HistoryContentStatus.Original: clauses.Add("edited=0 AND redacted=0 AND redaction_baked=0"); break;
                case HistoryContentStatus.Edited: clauses.Add("edited=1"); break;
                case HistoryContentStatus.Redacted: clauses.Add("redacted=1"); break;
                case HistoryContentStatus.OcrAvailable: clauses.Add("ocr_text IS NOT NULL AND length(trim(ocr_text))>0"); break;
                case HistoryContentStatus.RedactionBaked: clauses.Add("redaction_baked=1"); break;
                default: throw new ArgumentOutOfRangeException(nameof(query.Status));
            }
            AddOptionalRange(command, clauses, "$from", "created_utc>=", query.DateFromUtc.HasValue ? (object)EnsureUtc(query.DateFromUtc.Value).Ticks : null);
            AddOptionalRange(command, clauses, "$to", "created_utc<=", query.DateToUtc.HasValue ? (object)EnsureUtc(query.DateToUtc.Value).Ticks : null);
            AddOptionalRange(command, clauses, "$minSize", "size_bytes>=", query.MinSizeBytes);
            AddOptionalRange(command, clauses, "$maxSize", "size_bytes<=", query.MaxSizeBytes);
            return clauses.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", clauses);
        }

        private static void AddOptionalRange(SqliteCommand command, List<string> clauses, string parameter, string expression, object value)
        {
            if (value == null) return;
            clauses.Add(expression + parameter);
            command.Parameters.AddWithValue(parameter, value);
        }

        private const string SelectItems = "SELECT id,created_utc,updated_utc,hidden,favorite,deleted_utc,tags_json,edited,redacted,redaction_baked,ocr_text,width,height,size_bytes,current_rel,original_rel,document_rel FROM items";

        private List<HistoryItem> ReadAllItems()
        {
            using (SqliteConnection connection = OpenConnection())
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = SelectItems;
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    var items = new List<HistoryItem>();
                    while (reader.Read()) items.Add(ReadItem(reader));
                    return items;
                }
            }
        }

        private HistoryItem GetCore(string normalizedId)
        {
            using (SqliteConnection connection = OpenConnection()) return GetCore(connection, normalizedId);
        }

        private HistoryItem GetCore(SqliteConnection connection, string normalizedId)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = SelectItems + " WHERE id=$id";
                command.Parameters.AddWithValue("$id", normalizedId);
                using (SqliteDataReader reader = command.ExecuteReader()) return reader.Read() ? ReadItem(reader) : null;
            }
        }

        private HistoryItem ReadItem(SqliteDataReader reader)
        {
            return new HistoryItem
            {
                Id = reader.GetString(0),
                CreatedUtc = FromTicks(reader.GetInt64(1)),
                UpdatedUtc = FromTicks(reader.GetInt64(2)),
                Hidden = reader.GetInt64(3) != 0,
                Favorite = reader.GetInt64(4) != 0,
                DeletedUtc = reader.IsDBNull(5) ? (DateTime?)null : FromTicks(reader.GetInt64(5)),
                Tags = DeserializeTags(reader.GetString(6)),
                Edited = reader.GetInt64(7) != 0,
                Redacted = reader.GetInt64(8) != 0,
                RedactionBaked = reader.GetInt64(9) != 0,
                OcrText = reader.IsDBNull(10) ? null : reader.GetString(10),
                Width = reader.GetInt32(11),
                Height = reader.GetInt32(12),
                SizeBytes = reader.GetInt64(13),
                CurrentPath = StoragePath.ResolveUnderRoot(RootPath, reader.GetString(14)),
                OriginalPath = StoragePath.ResolveUnderRoot(RootPath, reader.GetString(15)),
                DocumentPath = StoragePath.ResolveUnderRoot(RootPath, reader.GetString(16))
            };
        }

        private bool SetBoolean(string id, string column, bool value)
        {
            lock (gate)
            {
                ThrowIfDisposed();
                string normalizedId;
                if (!TryNormalizeId(id, out normalizedId)) return false;
                using (SqliteConnection connection = OpenConnection())
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "UPDATE items SET " + column + "=$value WHERE id=$id";
                    command.Parameters.AddWithValue("$value", value ? 1 : 0);
                    command.Parameters.AddWithValue("$id", normalizedId);
                    return command.ExecuteNonQuery() == 1;
                }
            }
        }

        private int SetDeleted(IEnumerable<string> ids, long deletedTicks, bool requireDeleted)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            HashSet<string> normalized = NormalizeIds(ids);
            lock (gate)
            {
                ThrowIfDisposed();
                int changed = 0;
                using (SqliteConnection connection = OpenConnection())
                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    foreach (string id in normalized)
                    {
                        using (SqliteCommand command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE items SET deleted_utc=$deleted WHERE id=$id" + (requireDeleted ? " AND deleted_utc IS NOT NULL" : string.Empty);
                            command.Parameters.AddWithValue("$deleted", deletedTicks);
                            command.Parameters.AddWithValue("$id", id);
                            changed += command.ExecuteNonQuery();
                        }
                    }
                    transaction.Commit();
                }
                return changed;
            }
        }

        private void InitializeDatabase()
        {
            lock (gate)
            {
                using (SqliteConnection connection = OpenConnection())
                {
                    int version;
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "PRAGMA user_version";
                        version = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                    }
                    if (version > SchemaVersion) throw new InvalidDataException("History database schema is newer than this application.");
                    if (version < SchemaVersion)
                    {
                        if (DatabaseHasUserTables(connection)) BackupForMigration(connection, version);
                        using (SqliteTransaction transaction = connection.BeginTransaction())
                        using (SqliteCommand command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = @"CREATE TABLE IF NOT EXISTS items (
                                id TEXT PRIMARY KEY NOT NULL,
                                created_utc INTEGER NOT NULL,
                                updated_utc INTEGER NOT NULL,
                                hidden INTEGER NOT NULL DEFAULT 0,
                                favorite INTEGER NOT NULL DEFAULT 0,
                                deleted_utc INTEGER NULL,
                                tags_json TEXT NOT NULL DEFAULT '[]',
                                edited INTEGER NOT NULL DEFAULT 0,
                                redacted INTEGER NOT NULL DEFAULT 0,
                                redaction_baked INTEGER NOT NULL DEFAULT 0,
                                ocr_text TEXT NULL,
                                width INTEGER NOT NULL,
                                height INTEGER NOT NULL,
                                size_bytes INTEGER NOT NULL,
                                current_rel TEXT NOT NULL,
                                original_rel TEXT NOT NULL,
                                document_rel TEXT NOT NULL);
                                CREATE INDEX IF NOT EXISTS ix_items_created ON items(created_utc);
                                CREATE INDEX IF NOT EXISTS ix_items_updated ON items(updated_utc);
                                CREATE INDEX IF NOT EXISTS ix_items_deleted ON items(deleted_utc);
                                PRAGMA user_version=1;";
                            command.ExecuteNonQuery();
                            transaction.Commit();
                        }
                    }
                }
            }
        }

        private SqliteConnection OpenConnection()
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            };
            var connection = new SqliteConnection(builder.ToString());
            connection.Open();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA busy_timeout=5000";
                command.ExecuteNonQuery();
            }
            return connection;
        }

        private static bool DatabaseHasUserTables(SqliteConnection connection)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
            }
        }

        private void BackupForMigration(SqliteConnection connection, int version)
        {
            string backupPath = databasePath + ".schema-v" + version + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + ".bak";
            var builder = new SqliteConnectionStringBuilder { DataSource = backupPath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false };
            using (var backup = new SqliteConnection(builder.ToString()))
            {
                backup.Open();
                connection.BackupDatabase(backup);
            }
        }

        private void CheckpointDatabase()
        {
            using (SqliteConnection connection = OpenConnection())
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
                command.ExecuteNonQuery();
            }
        }

        private VersionFiles WriteVersion(string id, DateTime stamp, byte[] originalPng, byte[] renderedPng, string documentJson)
        {
            string revision = stamp.Ticks.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string relativeDirectory = Path.Combine("items", id, revision);
            string directory = StoragePath.ResolveUnderRoot(RootPath, relativeDirectory);
            Directory.CreateDirectory(directory);
            string originalRelative = Path.Combine(relativeDirectory, "original.png");
            string currentRelative = Path.Combine(relativeDirectory, "current.png");
            string documentRelative = Path.Combine(relativeDirectory, "document.json");
            byte[] document = System.Text.Encoding.UTF8.GetBytes(documentJson ?? string.Empty);
            try
            {
                StoragePath.AtomicWrite(StoragePath.ResolveUnderRoot(RootPath, originalRelative), originalPng);
                StoragePath.AtomicWrite(StoragePath.ResolveUnderRoot(RootPath, currentRelative), renderedPng);
                StoragePath.AtomicWrite(StoragePath.ResolveUnderRoot(RootPath, documentRelative), document);
            }
            catch
            {
                DeleteDirectoryIfPresent(directory);
                throw;
            }
            return new VersionFiles
            {
                VersionDirectory = directory,
                OriginalRelative = originalRelative,
                CurrentRelative = currentRelative,
                DocumentRelative = documentRelative,
                SizeBytes = originalPng.LongLength + renderedPng.LongLength + document.LongLength
            };
        }

        private void DeleteOldVersion(HistoryItem item)
        {
            string directory = Path.GetDirectoryName(item.CurrentPath);
            DeleteDirectoryIfPresent(directory);
        }

        private void RecoverOrphanVersions()
        {
            string itemsRoot = StoragePath.ResolveUnderRoot(RootPath, "items");
            if (!Directory.Exists(itemsRoot) || IsLink(itemsRoot)) return;
            var live = ReadAllItems().ToDictionary(x => x.Id, x => Path.GetDirectoryName(x.CurrentPath), StringComparer.OrdinalIgnoreCase);
            foreach (string itemDirectory in Directory.EnumerateDirectories(itemsRoot))
            {
                if (IsLink(itemDirectory) || !Guid.TryParseExact(Path.GetFileName(itemDirectory), "D", out _)) continue;
                if (!live.TryGetValue(Path.GetFileName(itemDirectory), out string current))
                {
                    DeleteDirectoryIfPresent(itemDirectory);
                    continue;
                }
                foreach (string version in Directory.EnumerateDirectories(itemDirectory))
                    if (!string.Equals(version, current, StringComparison.OrdinalIgnoreCase) && !IsLink(version)) DeleteDirectoryIfPresent(version);
            }
        }

        private static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

        private static void DeleteDirectoryIfPresent(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
            // Never traverse junctions or symbolic links when removing internal data.
            var pending = new Stack<string>(); pending.Push(directory);
            while (pending.Count > 0)
            {
                string candidate = pending.Pop();
                if (IsLink(candidate)) throw new IOException("内部记录含有链接，已停止清理：" + candidate);
                foreach (var entry in Directory.EnumerateFileSystemEntries(candidate))
                {
                    if (IsLink(entry)) throw new IOException("内部记录含有链接，已停止清理：" + entry);
                    if (Directory.Exists(entry)) pending.Push(entry);
                }
            }
            Directory.Delete(directory, true);
        }

        private static void UpdateTags(SqliteConnection connection, SqliteTransaction transaction, string id, List<string> tags)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "UPDATE items SET tags_json=$tags WHERE id=$id";
                command.Parameters.AddWithValue("$tags", SerializeTags(tags));
                command.Parameters.AddWithValue("$id", id);
                command.ExecuteNonQuery();
            }
        }

        private static List<string> NormalizeTags(IEnumerable<string> tags)
        {
            return tags.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string NormalizeTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) throw new ArgumentException("A non-empty tag is required.", nameof(tag));
            return tag.Trim();
        }

        private static string SerializeTags(List<string> tags) => JsonConvert.SerializeObject(tags, Formatting.None);

        private static List<string> DeserializeTags(string json)
        {
            return NormalizeTags(JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>());
        }

        private static void ValidatePayload(byte[] originalPng, byte[] renderedPng, int width, int height)
        {
            if (originalPng == null) throw new ArgumentNullException(nameof(originalPng));
            if (renderedPng == null) throw new ArgumentNullException(nameof(renderedPng));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        }

        private static string NormalizeId(string id)
        {
            string normalized;
            if (!TryNormalizeId(id, out normalized)) throw new ArgumentException("A valid history GUID is required.", nameof(id));
            return normalized;
        }

        private static bool TryNormalizeId(string id, out string normalized)
        {
            Guid guid;
            bool valid = Guid.TryParse(id, out guid);
            normalized = valid ? guid.ToString("D") : null;
            return valid;
        }

        private static HashSet<string> NormalizeIds(IEnumerable<string> ids)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string id in ids)
            {
                string normalized;
                if (TryNormalizeId(id, out normalized)) result.Add(normalized);
            }
            return result;
        }

        private static DateTime NextUtc(DateTime previous)
        {
            DateTime now = DateTime.UtcNow;
            return now > previous ? now : previous.AddTicks(1);
        }

        private static DateTime EnsureUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc) return value;
            if (value.Kind == DateTimeKind.Unspecified) return DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return value.ToUniversalTime();
        }

        private static DateTime FromTicks(long ticks) => new DateTime(ticks, DateTimeKind.Utc);

        private static long AddFileSize(string path, HashSet<string> referenced, Action<long> addDetail)
        {
            if (!File.Exists(path)) return 0;
            string full = Path.GetFullPath(path);
            if (!referenced.Add(full)) return 0;
            long length = new FileInfo(full).Length;
            addDetail(length);
            return length;
        }

        private bool IsDatabaseFile(string path)
        {
            return path.Equals(databasePath, StringComparison.OrdinalIgnoreCase) ||
                path.Equals(databasePath + "-wal", StringComparison.OrdinalIgnoreCase) ||
                path.Equals(databasePath + "-shm", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> EnumerateFilesSafely(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                string directory = pending.Pop();
                foreach (string file in Directory.EnumerateFiles(directory)) yield return file;
                foreach (string child in Directory.EnumerateDirectories(directory))
                {
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) pending.Push(child);
                }
            }
        }

        private static string MakeRelativePath(string root, string fullPath)
        {
            var rootUri = new Uri(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            var fileUri = new Uri(fullPath);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static void VerifyFileHash(string source, string destination)
        {
            byte[] sourceHash;
            byte[] destinationHash;
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(source)) sourceHash = algorithm.ComputeHash(stream);
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(destination)) destinationHash = algorithm.ComputeHash(stream);
            if (!sourceHash.SequenceEqual(destinationHash)) throw new IOException("Migration verification failed for " + source);
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(HistoryStore));
        }

        private sealed class VersionFiles
        {
            public string VersionDirectory { get; set; }
            public string OriginalRelative { get; set; }
            public string CurrentRelative { get; set; }
            public string DocumentRelative { get; set; }
            public long SizeBytes { get; set; }
        }
    }
}
