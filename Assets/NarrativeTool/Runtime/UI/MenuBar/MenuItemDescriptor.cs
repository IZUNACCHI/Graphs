using System;

namespace NarrativeTool.UI.MenuBar
{
    /// <summary>
    /// One entry in a top-level menu (File / Edit / Tools / Settings / …).
    /// Path may contain forward slashes to nest items into submenus, mirroring
    /// <see cref="UnityEngine.UIElements.GenericDropdownMenu"/> conventions.
    /// </summary>
    public sealed class MenuItemDescriptor
    {
        /// <summary>Top-level menu name e.g. "File".</summary>
        public string Menu;

        /// <summary>Item path inside the menu, e.g. "Save" or "Recent/Project A".</summary>
        public string Path;

        /// <summary>Display-only shortcut hint, e.g. "Ctrl S".</summary>
        public string Shortcut;

        public int Order;

        public Action Action;
        public Func<bool> IsEnabled;
        public Func<bool> IsChecked;

        /// <summary>If true a separator is drawn after this item.</summary>
        public bool IsSeparatorAfter;
    }
}
