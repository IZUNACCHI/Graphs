using System.Text;
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

        /// <summary>
        /// Temporary diagnostic switch. While true the manager logs every
        /// PointerDown it sees, the click target, its type, the ancestor chain,
        /// and which step (if any) rejected the click as "not a tab header
        /// drag". Used to debug why dragging doesn't arm; flip back to false
        /// once the root cause is fixed.
        /// </summary>
        public static bool LogDragDiagnostics = true;

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
            if (e.button != 0)
            {
                if (LogDragDiagnostics)
                    Debug.Log($"[DockDrag] PointerDown ignored — button={e.button} (only button 0 starts drags).");
                return;
            }

            if (LogDragDiagnostics) DiagnoseClick(e);

            if (!FindTabHeaderClick(e.target as VisualElement, out var tab, out var area, out var panel))
                return;

            if (LogDragDiagnostics)
                Debug.Log($"[DockDrag] ARMED on panel='{panel.Id}' area={area.GetHashCode()} pointer={e.pointerId}");

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

        // ───────────────────── Diagnostics ─────────────────────

        private static void DiagnoseClick(PointerDownEvent e)
        {
            var target = e.target as VisualElement;
            var sb = new StringBuilder();
            sb.Append("[DockDrag] PointerDown @ ").Append(e.position).Append("\n");
            sb.Append("  target = ").Append(Describe(target)).Append("\n");

            // Walk ancestors so we can see what's between target and the panel root.
            sb.Append("  ancestors:");
            int i = 0;
            for (var cur = target?.parent; cur != null && i < 30; cur = cur.parent, i++)
                sb.Append("\n    [").Append(i).Append("] ").Append(Describe(cur));
            if (i == 30) sb.Append("\n    … (truncated)");

            // If a Tab is in the chain, report whether the click landed inside its
            // contentContainer (panel content) vs outside (header).
            Tab tab = null;
            for (var cur = target; cur != null; cur = cur.parent)
                if (cur is Tab t) { tab = t; break; }
            if (tab == null)
            {
                sb.Append("\n  -> no enclosing Tab in ancestor chain (click was on splitter/canvas/etc).");
            }
            else
            {
                var content = tab.contentContainer;
                sb.Append("\n  enclosing Tab = ").Append(Describe(tab));
                sb.Append("\n  Tab.contentContainer = ").Append(Describe(content));
                sb.Append("\n  Tab.userData = ").Append(tab.userData?.GetType().FullName ?? "<null>");
                bool hitsContent = false;
                if (content != null && content != tab)
                {
                    for (var c = target; c != null && c != tab; c = c.parent)
                        if (c == content) { hitsContent = true; break; }
                }
                sb.Append("\n  -> click traverses contentContainer? ").Append(hitsContent)
                  .Append(hitsContent ? " (panel content — drag skipped)" : " (header strip — should ARM)");
            }
            Debug.Log(sb.ToString());
        }

        private static string Describe(VisualElement el)
        {
            if (el == null) return "<null>";
            var sb = new StringBuilder();
            sb.Append(el.GetType().Name);
            if (!string.IsNullOrEmpty(el.name)) sb.Append("#").Append(el.name);
            // First few class names
            int count = 0;
            foreach (var cls in el.GetClasses())
            {
                if (count == 0) sb.Append("  classes=[");
                if (count > 0) sb.Append(",");
                sb.Append(cls);
                if (++count >= 6) { sb.Append(",…"); break; }
            }
            if (count > 0) sb.Append("]");
            return sb.ToString();
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
        /// Returns true iff the click landed on a Tab header. Detection: walk
        /// ancestors of the click target looking for an element tagged with
        /// <see cref="DockArea.HeaderMarkerClass"/>; that element's userData is
        /// the owning <see cref="IDockablePanel"/>.
        /// <para>
        /// We can't infer the owning Tab from the parent chain because in
        /// Unity 6.0.3 the Tab's header element is reparented out of the Tab
        /// and into the TabView's `unity-tab-view__header-container`, leaving
        /// header and content as cousins (different parents) rather than
        /// siblings of one Tab. <see cref="DockArea.AddPanel"/> stamps each
        /// freshly created header with the marker class so we can identify it.
        /// </para>
        /// </summary>
        private bool FindTabHeaderClick(VisualElement target,
            out Tab tab, out DockArea area, out IDockablePanel panel)
        {
            tab = null; area = null; panel = null;
            if (target == null) return false;

            VisualElement headerEl = null;
            for (var cur = target; cur != null; cur = cur.parent)
            {
                if (cur.ClassListContains(DockArea.HeaderMarkerClass))
                {
                    headerEl = cur;
                    break;
                }
            }
            if (headerEl == null) return false;
            if (!(headerEl.userData is IDockablePanel p)) return false;

            foreach (var z in root.AllZones())
            {
                var a = z.FindAreaContaining(p.Id);
                if (a != null)
                {
                    area = a;
                    tab = a.GetTab(p.Id);
                    panel = p;
                    return true;
                }
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
