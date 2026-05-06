using System;
using System.Collections.Generic;

namespace NarrativeTool.UI.Toolbar
{
    /// <summary>
    /// Static provider populated by callers (built-ins or extensions). The
    /// <see cref="Toolbar"/> control listens to <see cref="Changed"/> and rebuilds
    /// its layout whenever the set of registered items changes.
    /// </summary>
    public static class ToolbarRegistry
    {
        private static readonly List<ToolbarItemDescriptor> items = new();

        public static event Action Changed;

        public static IReadOnlyList<ToolbarItemDescriptor> All => items;

        public static void Register(ToolbarItemDescriptor d)
        {
            if (d == null || string.IsNullOrEmpty(d.Id)) return;
            // Replace by id.
            items.RemoveAll(x => x.Id == d.Id);
            items.Add(d);
            Changed?.Invoke();
        }

        public static void Unregister(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            int n = items.RemoveAll(x => x.Id == id);
            if (n > 0) Changed?.Invoke();
        }

        public static void Clear()
        {
            if (items.Count == 0) return;
            items.Clear();
            Changed?.Invoke();
        }
    }
}
