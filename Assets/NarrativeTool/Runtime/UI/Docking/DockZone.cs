using System.Collections.Generic;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Docking
{
    /// <summary>
    /// One of the four fixed slots in <see cref="DockRoot"/> (Left, Right, Bottom,
    /// Center). Owns a single <see cref="DockNode"/> tree; supports inserting,
    /// splitting and collapsing areas inside it.
    /// </summary>
    public sealed class DockZone : VisualElement
    {
        private DockNode root;

        public DockZoneKind Kind { get; }
        public bool AllowsPinnedCenter => Kind == DockZoneKind.Center;
        public DockNode Root => root;

        /// <summary>Back-reference set by <see cref="DockRoot"/> so areas can
        /// publish events (e.g. "tab added") through to the drag manager.</summary>
        public DockRoot Owner { get; internal set; }

        public DockZone(DockZoneKind kind)
        {
            Kind = kind;
            AddToClassList("nt-dock-zone");
            AddToClassList("nt-dock-zone--" + kind.ToString().ToLowerInvariant());
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            // Start with an empty area so the zone has something to render.
            SetRoot(new DockArea());
        }

        /// <summary>
        /// Replaces the dock-tree with raw content. Used (Phase 2a) to host
        /// <c>GraphTabManager</c> in the center zone before it is migrated into a
        /// real <see cref="DockArea"/> in Phase 2c. While in custom-content mode
        /// the zone has no <see cref="Root"/> and panels cannot be added here.
        /// </summary>
        public void SetCustomContent(VisualElement content)
        {
            Clear();
            root = null;
            if (content != null) Add(content);
        }

        public void SetRoot(DockNode node)
        {
            // Clear is safe whether we were in dock-tree or custom-content mode.
            Clear();
            root = node;
            if (root != null)
            {
                AssignZoneRecursively(root);
                root.Parent = null;
                Add(root.Element);
            }
        }

        private void AssignZoneRecursively(DockNode n)
        {
            n.Zone = this;
            if (n is DockSplit s)
            {
                if (s.First  != null) AssignZoneRecursively(s.First);
                if (s.Second != null) AssignZoneRecursively(s.Second);
            }
        }

        // ─────────────────────────── Tree mutations ───────────────────────────

        /// <summary>Add a panel to the first available area. If the zone has no
        /// areas yet, creates one.</summary>
        public DockArea AddPanel(IDockablePanel panel)
        {
            var area = FindFirstArea();
            if (area == null)
            {
                area = new DockArea();
                SetRoot(area);
            }
            area.AddPanel(panel);
            return area;
        }

        /// <summary>Splits <paramref name="targetArea"/> by inserting a new area
        /// containing <paramref name="panel"/> on the given <paramref name="side"/>.</summary>
        public DockArea SplitArea(DockArea targetArea, DropSide side, IDockablePanel panel,
                                  float ratio = 0.5f)
        {
            if (targetArea == null || side == DropSide.Center) return null;
            var newArea = new DockArea();
            newArea.AddPanel(panel);

            var orientation = (side == DropSide.Left || side == DropSide.Right)
                ? DockOrientation.Horizontal
                : DockOrientation.Vertical;
            bool newOnFirst = (side == DropSide.Left || side == DropSide.Top);
            var first  = newOnFirst ? (DockNode)newArea : targetArea;
            var second = newOnFirst ? (DockNode)targetArea : newArea;

            // Approximate fixed-pane size from the zone's current dimensions
            float dim = orientation == DockOrientation.Horizontal
                ? Mathf01(resolvedStyle.width)  * ratio
                : Mathf01(resolvedStyle.height) * ratio;
            if (dim <= 0) dim = 200;

            var newSplit = new DockSplit(orientation, first, second,
                fixedPaneIndex: newOnFirst ? 0 : 1, fixedPaneStartDimension: dim);

            // Replace targetArea in the tree with newSplit.
            var parent = targetArea.Parent;
            if (parent == null)
            {
                SetRoot(newSplit);
            }
            else
            {
                parent.ReplaceChild(targetArea, newSplit);
                AssignZoneRecursively(newSplit);
            }
            return newArea;
        }

        private static float Mathf01(float v) => v <= 0 ? 0 : v;

        /// <summary>Removes an empty area, collapsing the parent split if any.
        /// If the area is the zone root the zone is left with an empty placeholder
        /// area (so the zone slot is preserved).</summary>
        public void CollapseArea(DockArea area)
        {
            if (area == null) return;
            var parent = area.Parent;
            if (parent == null)
            {
                // It IS the zone root; replace with a fresh empty area.
                SetRoot(new DockArea());
                return;
            }
            var sibling = parent.Sibling(area);
            // Replace the parent split with `sibling` in the grandparent.
            var grand = parent.Parent;
            if (grand == null)
            {
                SetRoot(sibling);
            }
            else
            {
                grand.ReplaceChild(parent, sibling);
                AssignZoneRecursively(sibling);
            }
        }

        // ─────────────────────────── Queries ───────────────────────────

        public DockArea FindFirstArea() => FindFirstArea(root);
        private static DockArea FindFirstArea(DockNode n)
        {
            switch (n)
            {
                case DockArea a: return a;
                case DockSplit s:
                    return FindFirstArea(s.First) ?? FindFirstArea(s.Second);
                default: return null;
            }
        }

        public DockArea FindAreaContaining(string panelId)
            => FindAreaContaining(root, panelId);
        private static DockArea FindAreaContaining(DockNode n, string id)
        {
            switch (n)
            {
                case DockArea a: return a.HasPanel(id) ? a : null;
                case DockSplit s:
                    return FindAreaContaining(s.First, id) ?? FindAreaContaining(s.Second, id);
                default: return null;
            }
        }

        public IEnumerable<DockArea> AllAreas()
        {
            foreach (var n in Walk(root))
                if (n is DockArea a) yield return a;
        }
        private static IEnumerable<DockNode> Walk(DockNode n)
        {
            if (n == null) yield break;
            yield return n;
            if (n is DockSplit s)
            {
                foreach (var c in Walk(s.First))  yield return c;
                foreach (var c in Walk(s.Second)) yield return c;
            }
        }
    }
}
