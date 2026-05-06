using UnityEngine.UIElements;

namespace NarrativeTool.UI.Docking
{
    public enum DockOrientation { Horizontal, Vertical }

    /// <summary>
    /// Interior dock node: a two-pane splitter holding two child <see cref="DockNode"/>s.
    /// Wraps Unity's native <see cref="TwoPaneSplitView"/> so edge-drag resizing
    /// is free.
    /// </summary>
    public sealed class DockSplit : DockNode
    {
        private readonly TwoPaneSplitView splitView;
        private DockNode first;
        private DockNode second;

        public override VisualElement Element => splitView;
        public DockOrientation Orientation { get; }
        public DockNode First  => first;
        public DockNode Second => second;

        public DockSplit(DockOrientation orientation, DockNode a, DockNode b,
                         int fixedPaneIndex = 0, float fixedPaneStartDimension = 200f)
        {
            Orientation = orientation;
            var unityOrientation = orientation == DockOrientation.Horizontal
                ? TwoPaneSplitViewOrientation.Horizontal
                : TwoPaneSplitViewOrientation.Vertical;

            splitView = new TwoPaneSplitView(fixedPaneIndex, fixedPaneStartDimension, unityOrientation);
            splitView.AddToClassList("nt-dock-split");
            splitView.style.flexGrow = 1;

            SetChildren(a, b);
        }

        public void SetChildren(DockNode a, DockNode b)
        {
            // Detach old
            if (first  != null) { first.Element.RemoveFromHierarchy();  first.Parent  = null; }
            if (second != null) { second.Element.RemoveFromHierarchy(); second.Parent = null; }
            splitView.Clear();

            first  = a;
            second = b;
            if (first  != null) { first.Parent  = this; first.Zone  = Zone; splitView.Add(first.Element);  }
            if (second != null) { second.Parent = this; second.Zone = Zone; splitView.Add(second.Element); }
        }

        /// <summary>Replaces one of the two children. Used when a sub-tree is
        /// rewritten (e.g. an empty area being collapsed away).</summary>
        public void ReplaceChild(DockNode oldChild, DockNode newChild)
        {
            if (first == oldChild)       SetChildren(newChild, second);
            else if (second == oldChild) SetChildren(first, newChild);
        }

        /// <summary>Returns the sibling of <paramref name="child"/>, or null.</summary>
        public DockNode Sibling(DockNode child)
            => child == first ? second : (child == second ? first : null);
    }
}
