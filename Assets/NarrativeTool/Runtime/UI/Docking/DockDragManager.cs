using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Docking
{
    /// <summary>
    /// Detects tab-header drags and turns them into structural moves on
    /// <see cref="DockRoot"/>.
    /// <para>
    /// Single global handler at the root, registered with <see cref="TrickleDown.TrickleDown"/>
    /// so we observe every <see cref="PointerDownEvent"/> before any descendant
    /// can call StopPropagation. We then walk the event target's ancestor chain
    /// to decide if the click was inside a Tab's header element. Clicks inside
    /// panel content (a row in Variables, the canvas, etc.) are ignored — no
    /// pointer capture is taken, so panel UI isn't blocked.
    /// </para>
    /// <para>
    /// Pointer capture is taken on the root <i>only after</i> the user crosses
    /// the drag threshold. That way simple clicks select the tab via the
    /// TabView's own logic and never freeze the UI; once the user is committed
    /// to a drag we capture so move/up events keep coming even if the cursor
    /// briefly leaves the source area on the way to the drop zone.
    /// </para>
    /// </summary>
    public sealed class DockDragManager
    {
        private const float DragThreshold = 5f;

        private readonly DockRoot root;
        private readonly DockDropOverlay overlay;

        // Drag state
        private bool armed;          // pointer is down on a tab header but threshold not yet met
        private bool dragging;
        private IDockablePanel draggedPanel;
        private DockArea sourceArea;
        private Vector2 startPos;
        private DockArea hoverArea;
        private DropSide hoverSide;
        private int activePointerId = -1;

        public DockDragManager(DockRoot root)
        {
            this.root = root;
            overlay = new DockDropOverlay();
            root.Add(overlay);

            // One trickle-phase handler for each pointer phase. TrickleDown wins
            // over any descendant handler that calls StopPropagation, and it
            // means we don't need per-Tab registration (which broke when Unity
            // patches change the tab-header USS class names).
            root.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerMoveEvent>(OnRootPointerMove, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(OnRootPointerUp,    TrickleDown.TrickleDown);
        }

        // ───────────────────── Pointer handling ─────────────────────

        private void OnRootPointerDown(PointerDownEvent e)
        {
            if (e.button != 0) return;
            if (!FindTabHeaderClick(e.target as VisualElement, out var tab, out var area, out var panel))
                return;

            // Arm a potential drag. Don't capture the pointer yet — that would
            // steal the up-event from TabView's tab-selection logic, breaking
            // simple clicks. Capture is taken in OnRootPointerMove once the
            // user crosses the drag threshold.
            armed = true;
            dragging = false;
            startPos = e.position;
            draggedPanel = panel;
            sourceArea = area;
            activePointerId = e.pointerId;
        }

        private void OnRootPointerMove(PointerMoveEvent e)
        {
            if (!armed) return;
            if (e.pointerId != activePointerId) return;

            if (!dragging)
            {
                if (Vector2.Distance(e.position, startPos) < DragThreshold) return;
                dragging = true;
                // Now the user is committed; capture so we keep getting move/up
                // even if the cursor leaves the source area.
                root.CapturePointer(activePointerId);
            }

            UpdateHover(e.position);
        }

        private void OnRootPointerUp(PointerUpEvent e)
        {
            if (!armed) return;
            if (e.pointerId != activePointerId) return;

            if (dragging) Commit(e.position);
            // else: simple click — let TabView handle selection. We never
            // captured, so its own up-handler runs normally.

            Cancel();
        }

        private void Cancel()
        {
            if (activePointerId >= 0 && root.HasPointerCapture(activePointerId))
                root.ReleasePointer(activePointerId);

            armed = false;
            dragging = false;
            draggedPanel = null;
            sourceArea = null;
            hoverArea = null;
            activePointerId = -1;
            overlay.Hide();
        }

        // ───────────────────── Header detection ─────────────────────

        /// <summary>
        /// Walks the ancestor chain of <paramref name="target"/> looking for a
        /// Tab. Returns true (with <paramref name="tab"/> / <paramref name="area"/>
        /// / <paramref name="panel"/> filled) iff the click landed on a
        /// tab-internal element that is NOT inside the Tab's content container —
        /// in practice that means the header strip. We use the structural check
        /// (<see cref="VisualElement.contentContainer"/>) instead of USS class
        /// names because the latter vary across Unity 6 patches and have already
        /// burned us once.
        /// </summary>
        private bool FindTabHeaderClick(VisualElement target,
            out Tab tab, out DockArea area, out IDockablePanel panel)
        {
            tab = null; area = null; panel = null;
            if (target == null) return false;

            // Pass 1: find the enclosing Tab (if any).
            VisualElement cur = target;
            int depth = 0;
            while (cur != null && depth < 100)
            {
                if (cur is Tab t) { tab = t; break; }
                cur = cur.parent;
                depth++;
            }
            if (tab == null) return false;

            // Pass 2: check whether the click went through the Tab's
            // contentContainer on its way up. If so it's a click on panel
            // content (Variables row, canvas, etc.) — NOT the header strip.
            // contentContainer is public API, so this works regardless of
            // any USS class-name churn between Unity 6 patches.
            var content = tab.contentContainer;
            if (content != null && content != tab)
            {
                var c = target;
                while (c != null && c != tab)
                {
                    if (c == content) return false; // content click → skip
                    c = c.parent;
                }
            }

            if (!(tab.userData is IDockablePanel p)) return false;

            foreach (var z in root.AllZones())
            {
                var a = z.FindAreaContaining(p.Id);
                if (a != null) { area = a; panel = p; return true; }
            }
            return false;
        }

        // ───────────────────── Hit-testing & commit ─────────────────────

        private void UpdateHover(Vector2 worldPos)
        {
            hoverArea = HitTestArea(worldPos);
            if (hoverArea == null) { overlay.Hide(); return; }
            hoverSide = ComputeSide(hoverArea.Element.worldBound, worldPos);
            overlay.ShowOver(root, hoverArea.Element.worldBound, hoverSide);
        }

        private DockArea HitTestArea(Vector2 worldPos)
        {
            bool wantCenter = draggedPanel?.IsPinnedCenter ?? false;
            foreach (var zone in root.AllZones())
            {
                bool isCenter = zone.Kind == DockZoneKind.Center;
                if (wantCenter != isCenter) continue;
                if (zone.Root == null) continue; // custom-content zone — skip
                foreach (var area in zone.AllAreas())
                    if (area.Element.worldBound.Contains(worldPos)) return area;
            }
            return null;
        }

        private static DropSide ComputeSide(Rect bounds, Vector2 worldPos)
        {
            // Normalise pointer to [-0.5, 0.5] inside the area.
            float nx = (worldPos.x - bounds.center.x) / bounds.width;
            float ny = (worldPos.y - bounds.center.y) / bounds.height;
            if (Mathf.Abs(nx) < 0.2f && Mathf.Abs(ny) < 0.2f) return DropSide.Center;
            if (Mathf.Abs(nx) > Mathf.Abs(ny))
                return nx > 0 ? DropSide.Right : DropSide.Left;
            return ny > 0 ? DropSide.Bottom : DropSide.Top;
        }

        private void Commit(Vector2 worldPos)
        {
            if (hoverArea == null || draggedPanel == null) return;
            root.MoveTab(draggedPanel.Id, hoverArea, hoverSide);
        }
    }
}
