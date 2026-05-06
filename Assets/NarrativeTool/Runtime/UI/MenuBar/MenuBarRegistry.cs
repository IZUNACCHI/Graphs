using System;
using System.Collections.Generic;

namespace NarrativeTool.UI.MenuBar
{
    /// <summary>
    /// Static provider for menu-bar items. Mirrors <see cref="Toolbar.ToolbarRegistry"/>.
    /// </summary>
    public static class MenuBarRegistry
    {
        private static readonly List<MenuItemDescriptor> items = new();

        public static event Action Changed;

        public static IReadOnlyList<MenuItemDescriptor> All => items;

        /// <summary>Top-level menus in first-registration order.</summary>
        public static IEnumerable<string> Menus
        {
            get
            {
                var seen = new HashSet<string>();
                foreach (var i in items)
                    if (!string.IsNullOrEmpty(i.Menu) && seen.Add(i.Menu))
                        yield return i.Menu;
            }
        }

        public static void Register(MenuItemDescriptor d)
        {
            if (d == null || string.IsNullOrEmpty(d.Menu) || string.IsNullOrEmpty(d.Path)) return;
            items.Add(d);
            Changed?.Invoke();
        }

        public static void Clear()
        {
            if (items.Count == 0) return;
            items.Clear();
            Changed?.Invoke();
        }
    }
}
