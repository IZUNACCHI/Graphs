using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Docking
{
    /// <summary>
    /// Leaf dock node: a TabView holding one Tab per docked panel.
    /// </summary>
    public sealed class DockArea : DockNode
    {
        private readonly TabView tabView;
        private readonly List<IDockablePanel> panels = new();
        private readonly Dictionary<string, Tab> tabsById = new();
        private Label placeholder;

        public override VisualElement Element => tabView;
        public TabView TabView => tabView;
        public IReadOnlyList<IDockablePanel> Panels => panels;

        /// <summary>Fires after a panel has been removed via <see cref="DetachPanel"/>.
        /// Used by <see cref="NarrativeTool.Canvas.Core.GraphCenterController"/>
        /// to dispose the underlying GraphView when the user clicks the X on a
        /// graph tab.</summary>
        public event Action<IDockablePanel> PanelClosed;

        public DockArea()
        {
            tabView = new TabView();
            tabView.AddToClassList("nt-dock-area");
            tabView.style.flexGrow = 1;

            // Placeholder shown when no panels are docked here. Text is
            // selected lazily based on Zone kind (see RefreshPlaceholder).
            placeholder = new Label("");
            placeholder.AddToClassList("nt-dock-area__placeholder");
            placeholder.pickingMode = PickingMode.Ignore;
            placeholder.style.position = Position.Absolute;
            placeholder.style.left = 0; placeholder.style.right = 0;
            placeholder.style.top = 0;  placeholder.style.bottom = 0;
            placeholder.style.unityTextAlign = TextAnchor.MiddleCenter;
            placeholder.style.display = DisplayStyle.None;
            tabView.Add(placeholder);
        }

        public bool HasPanel(string id) => tabsById.ContainsKey(id);

        public Tab GetTab(string id) => tabsById.TryGetValue(id, out var t) ? t : null;

        public IDockablePanel GetPanel(string id) => panels.Find(p => p.Id == id);

        /// <summary>Header container element of the underlying TabView, or null
        /// if not yet materialised. Cached lazily.</summary>
        public VisualElement HeaderContainer
            => tabView.Q(className: "unity-tab-view__header-container");

        public void AddPanel(IDockablePanel p)
        {
            if (p == null || tabsById.ContainsKey(p.Id)) return;

            var tab = new Tab(p.Title);
            tab.AddToClassList("nt-dock-tab");
            tab.userData = p; // used by serializer & drag manager
            // Re-parent panel content into the tab.
            p.Content.RemoveFromHierarchy();
            p.Content.style.flexGrow = 1;
            tab.Add(p.Content);

            tabView.Add(tab);
            panels.Add(p);
            tabsById[p.Id] = tab;

            // In Unity 6.0.3 the Tab's header element is reparented OUT of the
            // Tab and into the TabView's `unity-tab-view__header-container`,
            // so a header click cannot reach the owning Tab via the parent
            // chain. Tag the just-created header element with our own marker
            // class + userData so the drag manager can identify which panel
            // a header click belongs to.
            int headerIndex = panels.Count - 1;
            tabView.schedule.Execute(() => MarkHeader(headerIndex, p)).ExecuteLater(0);

            RefreshPlaceholder();
            // Notify root (drag manager etc.).
            Zone?.Owner?.RaiseTabAdded(this, tab, p);
        }

        /// <summary>USS class added to each Tab's header element so
        /// <c>DockDragManager</c> can detect header clicks via ancestor walk.</summary>
        public const string HeaderMarkerClass = "nt-dock-tab-header";

        /// <summary>USS class on each Tab's close button. Drag-detection skips
        /// clicks on these so the X actually closes instead of arming a drag.</summary>
        public const string CloseButtonClass = "nt-dock-tab__close";

        private void MarkHeader(int index, IDockablePanel p)
        {
            var hc = HeaderContainer;
            if (hc == null || index < 0 || index >= hc.childCount) return;
            var header = hc[index];
            header.AddToClassList(HeaderMarkerClass);
            header.userData = p;

            // Append a close X if the panel allows it.
            if (p.IsCloseable && header.Q(className: CloseButtonClass) == null)
            {
                var closeBtn = new Label("×");
                closeBtn.AddToClassList(CloseButtonClass);
                closeBtn.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 0) return;
                    string pid = p.Id;
                    DetachPanel(pid);
                    if (IsEmpty) Zone?.CollapseArea(this);
                    Zone?.Owner?.RefreshZoneVisibility();
                    e.StopImmediatePropagation();
                });
                header.Add(closeBtn);
            }
        }

        /// <summary>Removes the tab without disposing the panel content. Returns
        /// the panel so the caller can re-add it to a different area.</summary>
        public IDockablePanel DetachPanel(string id)
        {
            if (!tabsById.TryGetValue(id, out var tab)) return null;
            var panel = panels.Find(p => p.Id == id);
            // Remove tab from TabView; detach content so it can be re-parented.
            tabView.Remove(tab);
            panel?.Content.RemoveFromHierarchy();
            tabsById.Remove(id);
            panels.RemoveAll(p => p.Id == id);

            RefreshPlaceholder();
            if (panel != null) PanelClosed?.Invoke(panel);
            return panel;
        }

        public bool IsEmpty => panels.Count == 0;

        public void SelectPanel(string id)
        {
            if (!tabsById.TryGetValue(id, out var tab)) return;
            tabView.activeTab = tab;
        }

        /// <summary>Updates the visible label of an existing tab. Used when the
        /// underlying graph is renamed.</summary>
        public void SetTabTitle(string id, string newTitle)
        {
            if (!tabsById.TryGetValue(id, out var tab)) return;
            tab.label = newTitle ?? "";
        }

        // ────────────── Tab reorder support (Issue 1) ──────────────

        /// <summary>Returns the index of the tab header under
        /// <paramref name="worldPos"/>, or -1 if the cursor isn't over the
        /// header strip. The returned value is the *insertion index* — drop
        /// at this index to land left of the existing tab; drop at
        /// childCount to land at the end.</summary>
        public int IndexOfHeaderAt(Vector2 worldPos)
        {
            var hc = HeaderContainer;
            if (hc == null || hc.childCount == 0) return -1;
            if (!hc.worldBound.Contains(worldPos)) return -1;

            for (int i = 0; i < hc.childCount; i++)
            {
                var h = hc[i];
                var b = h.worldBound;
                // If pointer is in the LEFT half of header i, insert at i.
                // If in the RIGHT half, insert at i+1.
                float midX = b.x + b.width * 0.5f;
                if (worldPos.x < midX) return i;
            }
            return hc.childCount;
        }

        /// <summary>Repositions an existing panel's tab to the given slot. Used
        /// by drag-reorder. <paramref name="targetIndex"/> is the insertion
        /// index returned by <see cref="IndexOfHeaderAt"/>.</summary>
        public void MoveTabToIndex(string id, int targetIndex)
        {
            if (!tabsById.TryGetValue(id, out var tab)) return;
            int currentIndex = panels.FindIndex(p => p.Id == id);
            if (currentIndex < 0) return;
            // Adjust insertion index if we're moving the tab past itself.
            if (targetIndex > currentIndex) targetIndex--;
            if (targetIndex < 0) targetIndex = 0;
            if (targetIndex >= panels.Count) targetIndex = panels.Count - 1;
            if (targetIndex == currentIndex) return;

            // Move in the panels list.
            var p = panels[currentIndex];
            panels.RemoveAt(currentIndex);
            panels.Insert(targetIndex, p);

            // Re-order the tab in the TabView (TabView sorts header by
            // child order). Removing + re-inserting is the safe path here —
            // TabView doesn't expose a "move" API.
            tabView.Remove(tab);
            // Pick a tab position that maps to our targetIndex among all
            // current TabView children (placeholder is the LAST child of
            // tabView, never a Tab — so direct index works for tabs).
            // Insert after any tabs at indices < targetIndex.
            tabView.Insert(targetIndex, tab);

            // Re-stamp header markers — header order has changed.
            tabView.schedule.Execute(RemarkAllHeaders).ExecuteLater(0);
            tabView.activeTab = tab;
        }

        private void RemarkAllHeaders()
        {
            var hc = HeaderContainer;
            if (hc == null) return;
            for (int i = 0; i < hc.childCount && i < panels.Count; i++)
            {
                var h = hc[i];
                h.AddToClassList(HeaderMarkerClass);
                h.userData = panels[i];
            }
        }

        // ────────────── Placeholder ──────────────

        private void RefreshPlaceholder()
        {
            if (placeholder == null) return;
            if (panels.Count == 0)
            {
                placeholder.text = (Zone?.Kind == DockZoneKind.Center)
                    ? "No graph open. Double-click a graph in the Graphs panel."
                    : "Empty area — drag a tab here.";
                placeholder.style.display = DisplayStyle.Flex;
            }
            else
            {
                placeholder.style.display = DisplayStyle.None;
            }
        }
    }
}
