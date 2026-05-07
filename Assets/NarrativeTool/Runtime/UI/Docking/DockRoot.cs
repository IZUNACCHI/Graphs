using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
// Disambiguate: both UnityEngine and UnityEngine.UIElements define Cursor.
using UICursor = UnityEngine.UIElements.Cursor;

namespace NarrativeTool.UI.Docking
{
    /// <summary>
    /// Top-level dock host with four fixed zones (Left, Center, Right, Bottom).
    /// Layout (using nested <see cref="TwoPaneSplitView"/>):
    /// <code>
    ///   ┌─────────────────────────────────────────┐
    ///   │ Left │       Center        │  Right     │
    ///   │      │─────────────────────│            │
    ///   │      │       Bottom        │            │
    ///   └─────────────────────────────────────────┘
    /// </code>
    /// </summary>
    public sealed class DockRoot : VisualElement
    {
        public DockZone Left   { get; }
        public DockZone Right  { get; }
        public DockZone Bottom { get; }
        public DockZone Center { get; }

        /// <summary>Fired whenever a Tab is added to any DockArea anywhere in the
        /// tree. The drag manager subscribes to register pointer handlers on
        /// newly-created tabs (e.g. after a split).</summary>
        public event Action<DockArea, Tab, IDockablePanel> TabAdded;
        internal void RaiseTabAdded(DockArea a, Tab t, IDockablePanel p) => TabAdded?.Invoke(a, t, p);

        // Side-zone splitters are kept around so we can collapse / uncollapse a
        // whole zone when it becomes empty (or refilled). Each non-center zone is
        // the *collapsible* pane of exactly one of these splitters.
        private readonly TwoPaneSplitView outerSplit;     // pane 0 = Left
        private readonly TwoPaneSplitView midRowSplit;    // pane 1 = Right
        private readonly TwoPaneSplitView centerColSplit; // pane 1 = Bottom

        public DockRoot()
        {
            AddToClassList("nt-dock-root");
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Row;

            Left   = new DockZone(DockZoneKind.Left)   { Owner = this };
            Right  = new DockZone(DockZoneKind.Right)  { Owner = this };
            Bottom = new DockZone(DockZoneKind.Bottom) { Owner = this };
            Center = new DockZone(DockZoneKind.Center) { Owner = this };

            // center column (center top + bottom).
            centerColSplit = new TwoPaneSplitView(1, 200f, TwoPaneSplitViewOrientation.Vertical);
            centerColSplit.AddToClassList("nt-dock-root__center-col");
            centerColSplit.style.flexGrow = 1;
            centerColSplit.Add(Center);
            centerColSplit.Add(Bottom);

            // middle row (center column + right zone)
            midRowSplit = new TwoPaneSplitView(1, 280f, TwoPaneSplitViewOrientation.Horizontal);
            midRowSplit.AddToClassList("nt-dock-root__mid-row");
            midRowSplit.style.flexGrow = 1;
            midRowSplit.Add(centerColSplit);
            midRowSplit.Add(Right);

            // outer row (left + middle row)
            outerSplit = new TwoPaneSplitView(0, 200f, TwoPaneSplitViewOrientation.Horizontal);
            outerSplit.AddToClassList("nt-dock-root__outer");
            outerSplit.style.flexGrow = 1;
            outerSplit.Add(Left);
            outerSplit.Add(midRowSplit);

            Add(outerSplit);

            // First zone-visibility pass once layout has measured (resolved
            // styles aren't available synchronously in the constructor).
            schedule.Execute(RefreshZoneVisibility).ExecuteLater(0);

            // TwoPaneSplitView's USS sets a named OS resize-cursor on its
            // drag-line anchors; the runtime panel only renders cursor
            // *textures* and spams "Runtime cursors other than the default
            // cursor need to be defined using a texture" every frame
            // (UpdatePanels). StyleKeyword.None doesn't satisfy the validator
            // — we have to provide an actual Texture2D. Re-applied a few
            // frames later in case the dragline-anchor children are created
            // lazily after first layout.
            schedule.Execute(SuppressDragLineCursors).ExecuteLater(0);
            schedule.Execute(SuppressDragLineCursors).ExecuteLater(100);
            schedule.Execute(SuppressDragLineCursors).ExecuteLater(500);
        }

        // 1×1 transparent cursor texture, shared. Setting any element's cursor
        // to this is enough for the runtime panel to consider it a "valid
        // texture cursor" and stop logging the warning. Visual cost: cursor
        // disappears for a few px when hovering the dragline (acceptable —
        // resize affordance is still discoverable by the visible drag-bar).
        private static Texture2D s_transparentCursorTex;
        private static Texture2D GetTransparentCursorTex()
        {
            if (s_transparentCursorTex == null)
            {
                s_transparentCursorTex = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
                s_transparentCursorTex.SetPixel(0, 0, new Color(0, 0, 0, 0));
                s_transparentCursorTex.Apply();
                s_transparentCursorTex.hideFlags = HideFlags.HideAndDontSave;
            }
            return s_transparentCursorTex;
        }

        private void SuppressDragLineCursors()
        {
            var cursor = new UICursor { texture = GetTransparentCursorTex(), hotspot = Vector2.zero };
            foreach (var anchor in this.Query(className: "unity-two-pane-split-view__dragline-anchor").ToList())
                anchor.style.cursor = new StyleCursor(cursor);
        }

        // ─────────────────────────── Zone visibility ───────────────────────────

        /// <summary>
        /// Collapses any side zone (Left / Right / Bottom) that has no panels and
        /// uncollapses it again once panels return. Center zone is never collapsed.
        /// Call after any structural mutation (open / close / move / load).
        /// </summary>
        public void RefreshZoneVisibility()
        {
            SetCollapsed(outerSplit,     0, IsZoneEmpty(Left));
            SetCollapsed(midRowSplit,    1, IsZoneEmpty(Right));
            SetCollapsed(centerColSplit, 1, IsZoneEmpty(Bottom));
            // Collapse/Uncollapse can rebuild dragline-anchor children — re-apply.
            SuppressDragLineCursors();
        }

        private static void SetCollapsed(TwoPaneSplitView split, int paneIdx, bool collapse)
        {
            if (split == null) return;
            if (collapse) split.CollapseChild(paneIdx);
            else          split.UnCollapse();
        }

        private static bool IsZoneEmpty(DockZone zone)
        {
            if (zone == null) return true;
            // The center zone keeps an empty DockArea so the placeholder hint
            // can render — but for collapse purposes we still report true if
            // it has no panels. RefreshZoneVisibility never collapses Center
            // anyway, so this only affects the drag-driven ghost styling.
            if (zone.Root == null) return true;
            foreach (var area in zone.AllAreas())
                if (!area.IsEmpty) return false;
            return true;
        }

        // ────────────── Drag-driven empty-zone exposure (Issue 2) ──────────────
        //
        // While a tab drag is active, every empty side zone is uncollapsed and
        // tagged with `nt-dock-zone--ghost` so it appears as a faint placeholder
        // strip and can accept a drop. On drag end, any zone that's still empty
        // is re-collapsed and the ghost class is removed.

        private bool dragActive;

        public void BeginDrag()
        {
            if (dragActive) return;
            dragActive = true;
            // Uncollapse all side splits unconditionally; only ones whose pane
            // was actually collapsed will visibly change. Tag empty zones.
            outerSplit?.UnCollapse();
            midRowSplit?.UnCollapse();
            centerColSplit?.UnCollapse();
            foreach (var z in AllZones())
            {
                if (z.Kind == DockZoneKind.Center) continue;
                if (IsZoneEmpty(z)) z.AddToClassList("nt-dock-zone--ghost");
            }
            SuppressDragLineCursors();
        }

        public void EndDrag()
        {
            if (!dragActive) return;
            dragActive = false;
            foreach (var z in AllZones())
                z.RemoveFromClassList("nt-dock-zone--ghost");
            // Collapse anything that's still empty.
            RefreshZoneVisibility();
        }

        public DockZone GetZone(DockZoneKind kind) => kind switch
        {
            DockZoneKind.Left   => Left,
            DockZoneKind.Right  => Right,
            DockZoneKind.Bottom => Bottom,
            DockZoneKind.Center => Center,
            _ => Left,
        };

        public IEnumerable<DockZone> AllZones()
        {
            yield return Left;
            yield return Center;
            yield return Right;
            yield return Bottom;
        }

        // ─────────────────────────── Panel operations ───────────────────────────

        /// <summary>Adds a panel to its default zone (or another zone if specified).</summary>
        public DockArea OpenPanel(IDockablePanel panel, DockZoneKind? zone = null)
        {
            if (panel == null) return null;
            var z = zone ?? (panel.IsPinnedCenter ? DockZoneKind.Center : DockZoneKind.Left);
            var area = GetZone(z).AddPanel(panel);
            RefreshZoneVisibility();
            return area;
        }

        /// <summary>Removes a panel by id from whatever area currently holds it.</summary>
        public IDockablePanel ClosePanel(string id)
        {
            foreach (var zone in AllZones())
            {
                var area = zone.FindAreaContaining(id);
                if (area == null) continue;
                var p = area.DetachPanel(id);
                if (area.IsEmpty) zone.CollapseArea(area);
                RefreshZoneVisibility();
                return p;
            }
            return null;
        }

        public bool IsOpen(string id)
        {
            foreach (var zone in AllZones())
                if (zone.FindAreaContaining(id) != null) return true;
            return false;
        }

        public DockArea FindArea(string panelId)
        {
            foreach (var zone in AllZones())
            {
                var a = zone.FindAreaContaining(panelId);
                if (a != null) return a;
            }
            return null;
        }

        /// <summary>Moves a panel from its current area to <paramref name="targetArea"/>.
        /// If <paramref name="side"/> is Center the panel becomes a new tab in the
        /// target. Otherwise the target is split in two and the panel goes into the
        /// new sibling area.</summary>
        public bool MoveTab(string panelId, DockArea targetArea, DropSide side)
        {
            if (string.IsNullOrEmpty(panelId) || targetArea == null) return false;
            var sourceArea = FindArea(panelId);
            if (sourceArea == null) return false;

            // No-op: dropping on the same single-area-with-one-panel.
            if (sourceArea == targetArea && side == DropSide.Center) return false;

            var panel = sourceArea.GetPanel(panelId);
            if (panel == null) return false;

            // Validate pinned-center constraints.
            var targetZone = targetArea.Zone;
            if (panel.IsPinnedCenter && targetZone.Kind != DockZoneKind.Center) return false;
            if (!panel.IsPinnedCenter && targetZone.Kind == DockZoneKind.Center) return false;

            // Detach (also disposes nothing — content is preserved).
            sourceArea.DetachPanel(panelId);

            if (side == DropSide.Center)
            {
                targetArea.AddPanel(panel);
                targetArea.SelectPanel(panelId);
            }
            else
            {
                var newArea = targetZone.SplitArea(targetArea, side, panel);
                newArea?.SelectPanel(panelId);
            }

            // Collapse the source area if it became empty.
            if (sourceArea.IsEmpty) sourceArea.Zone?.CollapseArea(sourceArea);
            RefreshZoneVisibility();
            return true;
        }

        /// <summary>Reorder a panel inside an area, or move it to a different
        /// area's tab strip at a specific index (drag-onto-header behaviour).
        /// Pinned-center constraints still apply.</summary>
        public bool ReorderTab(string panelId, DockArea targetArea, int targetIndex)
        {
            if (string.IsNullOrEmpty(panelId) || targetArea == null) return false;
            var sourceArea = FindArea(panelId);
            if (sourceArea == null) return false;

            var panel = sourceArea.GetPanel(panelId);
            if (panel == null) return false;

            var targetZone = targetArea.Zone;
            if (panel.IsPinnedCenter && targetZone.Kind != DockZoneKind.Center) return false;
            if (!panel.IsPinnedCenter && targetZone.Kind == DockZoneKind.Center) return false;

            if (sourceArea == targetArea)
            {
                targetArea.MoveTabToIndex(panelId, targetIndex);
            }
            else
            {
                sourceArea.DetachPanel(panelId);
                targetArea.AddPanel(panel);
                targetArea.MoveTabToIndex(panelId, targetIndex);
                if (sourceArea.IsEmpty) sourceArea.Zone?.CollapseArea(sourceArea);
                RefreshZoneVisibility();
            }
            return true;
        }
    }
}
