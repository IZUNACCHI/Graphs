using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Docking
{
    /// <summary>
    /// Detects tab-header drags and turns them into structural moves on
    /// <see cref="DockRoot"/>. Subscribes to <see cref="DockRoot.TabAdded"/> so
    /// pointer handlers are wired even on tabs born from a split operation.
    /// </summary>
    public sealed class DockDragManager
    {
        private const float DragThreshold = 5f;

        private readonly DockRoot root;
        private readonly DockDropOverlay overlay;

        // Drag state
        private bool dragging;
        private bool armed;          // pointer is down on a tab but threshold not yet met
        private IDockablePanel draggedPanel;
        private DockArea sourceArea;
        private Vector2 startPos;
        private DockArea hoverArea;
        private DropSide hoverSide;
        private int activePointerId = -1;
        private VisualElement capturedTab;

        public DockDragManager(DockRoot root)
        {
            this.root = root;
            overlay = new DockDropOverlay();
            root.Add(overlay);

            // Hook every existing tab and any added later.
            root.TabAdded += OnTabAdded;
            foreach (var z in root.AllZones())
                foreach (var area in z.AllAreas())
                    foreach (var panel in area.Panels)
                    {
                        var tab = area.GetTab(panel.Id);
                        if (tab != null) Attach(area, tab, panel);
                    }

            // Track movement at root level once a drag is armed.
            // (Bubble phase is fine — these events reach root after the original
            // target has already handled them, and we only act when armed=true.)
            root.RegisterCallback<PointerMoveEvent>(OnRootPointerMove);
            root.RegisterCallback<PointerUpEvent>(OnRootPointerUp);
            // NOTE: deliberately NOT subscribing to PointerCaptureOutEvent on root.
            // It bubbles up from any descendant releasing capture (e.g. a button
            // inside a panel), and a stale Cancel() there would clobber unrelated
            // state. Drag state is solely managed by Down/Move/Up.
        }

        // ───────────────────── Tab attachment ─────────────────────

        private void OnTabAdded(DockArea area, Tab tab, IDockablePanel panel)
            => Attach(area, tab, panel);

        private void Attach(DockArea area, Tab tab, IDockablePanel panel)
        {
            // Register on the Tab's HEADER element only — not the whole Tab.
            // Registering on `tab` itself caused two bugs:
            //   1. Clicks inside the panel content (Variables row, Entities item,
            //      etc.) trickle through the Tab on their way to the deep target.
            //      With TrickleDown=true the handler armed a drag and captured
            //      the pointer for every panel-internal click — freezing the UI.
            //   2. The visible tab header (where the user actually clicks to
            //      drag) is `unity-tab__header`, a child of Tab. Without listening
            //      directly on it the click sometimes never reached us.
            // Tab's header is created in its constructor so the Q lookup is safe
            // here. Fall back to the Tab itself if the class name ever changes.
            VisualElement header = tab.Q(className: "unity-tab__header") ?? (VisualElement)tab;
            header.RegisterCallback<PointerDownEvent>(e => OnTabPointerDown(e, area, panel, tab));
        }

        // ───────────────────── Pointer handling ─────────────────────

        private void OnTabPointerDown(PointerDownEvent e, DockArea area, IDockablePanel panel, Tab tab)
        {
            if (e.button != 0) return;
            // Arm a potential drag. We don't start until the pointer moves enough,
            // so simple clicks still flow through to TabView for selection.
            armed = true;
            dragging = false;
            startPos = e.position;
            draggedPanel = panel;
            sourceArea = area;
            activePointerId = e.pointerId;
            // Capture on the actual currentTarget (the header element). Doing this
            // ensures move/up events keep coming to us even if the cursor leaves
            // the header on the way to the drop zone.
            capturedTab = e.currentTarget as VisualElement ?? tab;
            capturedTab.CapturePointer(e.pointerId);
        }

        private void OnRootPointerMove(PointerMoveEvent e)
        {
            if (!armed) return;
            if (e.pointerId != activePointerId) return;

            if (!dragging)
            {
                if (Vector2.Distance(e.position, startPos) < DragThreshold) return;
                dragging = true;
            }

            UpdateHover(e.position);
        }

        private void OnRootPointerUp(PointerUpEvent e)
        {
            if (!armed) return;
            if (e.pointerId != activePointerId) return;

            if (dragging)
            {
                Commit(e.position);
            }
            // else: it was a click, do nothing — TabView already selected the tab.

            Cancel();
        }

        private void Cancel()
        {
            if (capturedTab != null && activePointerId >= 0)
            {
                if (capturedTab.HasPointerCapture(activePointerId))
                    capturedTab.ReleasePointer(activePointerId);
            }
            armed = false;
            dragging = false;
            draggedPanel = null;
            sourceArea = null;
            hoverArea = null;
            activePointerId = -1;
            capturedTab = null;
            overlay.Hide();
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
