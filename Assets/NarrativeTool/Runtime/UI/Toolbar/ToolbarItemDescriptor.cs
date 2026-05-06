using System;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Toolbar
{
    public enum ToolbarSide { Left, Center, Right }

    /// <summary>
    /// Describes a single item in the application toolbar. Items are sorted by
    /// (Side, Group, Order) and separators are inserted between distinct groups
    /// inside the same side.
    /// </summary>
    public sealed class ToolbarItemDescriptor
    {
        /// <summary>Stable id used for re-registration / removal.</summary>
        public string Id;

        /// <summary>Logical bucket (e.g. "navigation", "project", "run"). Items that
        /// share a Group are rendered next to each other; a separator is drawn
        /// between groups on the same Side.</summary>
        public string Group;

        public int Order;

        public ToolbarSide Side = ToolbarSide.Left;

        /// <summary>Factory called every time the toolbar refreshes. Must return a
        /// freshly created VisualElement.</summary>
        public Func<VisualElement> Build;

        /// <summary>Optional predicate. If supplied and returns false the item is
        /// hidden on the next Refresh().</summary>
        public Func<bool> IsVisible;
    }
}
