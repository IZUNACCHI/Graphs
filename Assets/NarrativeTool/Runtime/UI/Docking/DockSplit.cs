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
            // Detach old children visually & logically (RemoveFromHierarchy is
            // enough; we deliberately don't call splitView.Clear() since
            // TwoPaneSplitView keeps internal references that don't survive a
            // generic Clear()).
            if (first  != null) { first.Element.RemoveFromHierarchy();  first.Parent  = null; }
            if (second != null) { second.Element.RemoveFromHierarchy(); second.Parent = null; }

            first  = a;
            second = b;
            if (first  != null) { first.Parent  = this; first.Zone  = Zone; splitView.Add(first.Element);  }
            if (second != null) { second.Parent = this; second.Zone = Zone; splitView.Add(second.Element); }
        }

        /// <summary>Replaces one of the two children. Used when a sub-tree is
        /// rewritten (e.g. an empty area being collapsed away, or a leaf being
        /// upgraded to a sub-split).
        /// <para>
        /// Implementation note: we do NOT route through <see cref="SetChildren"/>
        /// because in some scenarios (notably split-while-already-inside-a-split)
        /// the new child has already been re-parented to live inside oldChild's
        /// soon-to-be position. SetChildren's blanket
        /// <c>oldChild.Element.RemoveFromHierarchy()</c> would tear that down.
        /// Instead we do a surgical slot swap and only touch the visual tree
        /// where it's still consistent with our data model.
        /// </para>
        /// </summary>
        public void ReplaceChild(DockNode oldChild, DockNode newChild)
        {
            int slot;
            if (first == oldChild) slot = 0;
            else if (second == oldChild) slot = 1;
            else return;

            // Detach oldChild's element only if it is still under our splitView.
            // If something else has already re-parented it (e.g. into newChild's
            // own subtree), leave it alone — that other owner is now responsible.
            if (oldChild?.Element != null && oldChild.Element.parent == splitView)
                oldChild.Element.RemoveFromHierarchy();
            // We deliberately don't touch oldChild.Parent — it may already be
            // pointing at its new owner. The caller manages oldChild's lifecycle.

            if (slot == 0) first = newChild;
            else           second = newChild;

            if (newChild != null)
            {
                newChild.Parent = this;
                newChild.Zone = Zone;
                if (newChild.Element != null && newChild.Element.parent != splitView)
                {
                    newChild.Element.RemoveFromHierarchy();
                    if (slot == 0) splitView.Insert(0, newChild.Element);
                    else           splitView.Add(newChild.Element);
                }
            }
        }

        /// <summary>Returns the sibling of <paramref name="child"/>, or null.</summary>
        public DockNode Sibling(DockNode child)
            => child == first ? second : (child == second ? first : null);
    }
}
