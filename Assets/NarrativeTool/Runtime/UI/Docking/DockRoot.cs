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

        public DockRoot()
        {
            AddToClassList("nt-dock-root");
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Row;

            Left   = new DockZone(DockZoneKind.Left);
            Right  = new DockZone(DockZoneKind.Right);
            Bottom = new DockZone(DockZoneKind.Bottom);
            Center = new DockZone(DockZoneKind.Center);

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
    }
}
