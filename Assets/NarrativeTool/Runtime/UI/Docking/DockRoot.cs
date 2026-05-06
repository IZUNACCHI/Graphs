using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

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
            var centerCol = new TwoPaneSplitView(1, 200f, TwoPaneSplitViewOrientation.Vertical);
            centerCol.AddToClassList("nt-dock-root__center-col");
            centerCol.style.flexGrow = 1;
            centerCol.Add(Center);
            centerCol.Add(Bottom);

            // middle row (center column + right zone)
            var midRow = new TwoPaneSplitView(1, 280f, TwoPaneSplitViewOrientation.Horizontal);
            midRow.AddToClassList("nt-dock-root__mid-row");
            midRow.style.flexGrow = 1;
            midRow.Add(centerCol);
            midRow.Add(Right);

            // outer row (left + middle row)
            var outer = new TwoPaneSplitView(0, 200f, TwoPaneSplitViewOrientation.Horizontal);
            outer.AddToClassList("nt-dock-root__outer");
            outer.style.flexGrow = 1;
            outer.Add(Left);
            outer.Add(midRow);

            Add(outer);
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
            return GetZone(z).AddPanel(panel);
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
            return true;
        }
    }
}
