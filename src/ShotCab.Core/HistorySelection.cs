using System;
using System.Collections.Generic;
using System.Linq;

namespace ShotCab.Core
{
    public sealed class HistorySelection
    {
        private readonly HashSet<string> selected = new HashSet<string>(StringComparer.Ordinal);
        private string anchor;

        public int Count => selected.Count;
        public bool Contains(string id) => id != null && selected.Contains(id);

        public IReadOnlyList<string> SelectedIds(IReadOnlyList<string> visibleIds) =>
            visibleIds.Where(selected.Contains).ToArray();

        public void Clear()
        {
            selected.Clear();
            anchor = null;
        }

        public void SetVisible(IReadOnlyList<string> visibleIds)
        {
            var visible = new HashSet<string>(visibleIds, StringComparer.Ordinal);
            selected.RemoveWhere(id => !visible.Contains(id));
            if (anchor != null && !visible.Contains(anchor)) anchor = null;
        }

        public void SelectForContext(IReadOnlyList<string> visibleIds, string id)
        {
            if (!Contains(id)) Click(visibleIds, id, false, false);
        }

        public void Click(IReadOnlyList<string> visibleIds, string id, bool control, bool shift)
        {
            int index = IndexOf(visibleIds, id);
            if (index < 0) throw new ArgumentException("The item must be visible.", nameof(id));

            if (shift)
            {
                int start = anchor == null ? -1 : IndexOf(visibleIds, anchor);
                if (!control) selected.Clear();
                if (start < 0) { selected.Add(id); anchor = id; return; }
                for (int i = Math.Min(start, index); i <= Math.Max(start, index); i++) selected.Add(visibleIds[i]);
            }
            else if (control)
            {
                if (!selected.Add(id)) selected.Remove(id);
                anchor = id;
            }
            else
            {
                selected.Clear();
                selected.Add(id);
                anchor = id;
            }
        }

        private static int IndexOf(IReadOnlyList<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++)
                if (string.Equals(ids[i], id, StringComparison.Ordinal)) return i;
            return -1;
        }
    }
}
