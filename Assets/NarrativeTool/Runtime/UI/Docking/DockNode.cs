using UnityEngine.UIElements;

namespace NarrativeTool.UI.Docking
{
    /// <summary>
    /// Base class for the two kinds of nodes in a dock-zone tree:
    /// <see cref="DockArea"/> (leaf, holds a tab strip) and
    /// <see cref="DockSplit"/> (interior, two children + orientation).
    /// </summary>
    public abstract class DockNode
    {
        /// <summary>The single VisualElement that materialises this node into the
        /// VisualElement tree.</summary>
        public abstract VisualElement Element { get; }

        /// <summary>Parent in the dock tree (a <see cref="DockSplit"/> or null when
        /// this node is the root of its zone).</summary>
        public DockSplit Parent { get; internal set; }

        /// <summary>Zone this node belongs to. Set by <see cref="DockZone"/> when
        /// the node is mounted; useful for the drag manager.</summary>
        public DockZone Zone { get; internal set; }
    }
}
