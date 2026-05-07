using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Docking
{
    /// <summary>
    /// Leaf dock node: a TabView (used solely as a tab-header strip) plus a
    /// content host that we manage ourselves.
    /// <para>
    /// Unity 6.0.3's <c>TabView</c> is opaque about where Tab content actually
    /// lives — calling <c>tab.Add(content)</c> routes through internal
    /// content-viewport machinery that either crashes (when the Tab isn't yet
    /// parented) or silently parks the content somewhere it isn't rendered.
    /// We sidestep that entirely: <c>Tab</c> objects only ever exist for their
    /// header. The actual panel <c>Content</c> goes into our own
    /// <c>contentHost</c> sibling element, with display toggled to match the
    /// active tab. This also makes split / reorder / detach trivial since
    /// content is just a regular child of contentHost.
    /// </para>
    /// </summary>
    public sealed class DockArea : DockNode
    {
        private readonly VisualElement element;       // wrapper
        private readonly TabView tabView;             // header strip only
        private readonly VisualElement contentHost;   // hosts panel content
        private readonly Label placeholder;           // overlay for empty areas
        private readonly List<IDockablePanel> panels = new();
        private readonly Dictionary<string, Tab> tabsById = new();

        public override VisualElement Element => element;
        public TabView TabView => tabView;
        public IReadOnlyList<IDockablePanel> Panels => panels;

        /// <summary>Fires after a panel has been removed via <see cref="DetachPanel"/>.
        /// Used by <see cref="NarrativeTool.Canvas.Core.GraphCenterController"/>
        /// to dispose the underlying GraphView when the user clicks the X on a
        /// graph tab.</summary>
        public event Action<IDockablePanel> PanelClosed;

        public DockArea()
        {
            element = new VisualElement();
            element.AddToClassList("nt-dock-area");
            element.style.flexGrow = 1;
            element.style.flexDirection = FlexDirection.Column;

            // TabView purely renders the header strip; content goes in our
            // contentHost below. flexGrow=0 so it doesn't try to take vertical
            // space — the height is driven by the header.
            tabView = new TabView();
            tabView.AddToClassList("nt-dock-area__tabs");
            tabView.style.flexGrow = 0;
            tabView.style.flexShrink = 0;
            element.Add(tabView);

            contentHost = new VisualElement();
            contentHost.AddToClassList("nt-dock-area__content");
            contentHost.style.flexGrow = 1;
            contentHost.style.flexDirection = FlexDirection.Column;
            element.Add(contentHost);

            placeholder = new Label("");
            placeholder.AddToClassList("nt-dock-area__placeholder");
            placeholder.pickingMode = PickingMode.Ignore;
            placeholder.style.position = Position.Absolute;
            placeholder.style.left = 0; placeholder.style.right = 0;
            placeholder.style.top = 0;  placeholder.style.bottom = 0;
            placeholder.style.unityTextAlign = TextAnchor.MiddleCenter;
            placeholder.style.display = DisplayStyle.None;
            element.Add(placeholder);

            // Swap visible content when the user clicks a different tab.
            tabView.activeTabChanged += OnActiveTabChanged;
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
            tab.userData = p;
            // Tab is parented to TabView. We deliberately don't call tab.Add(...)
            // — Tab content lives in our own contentHost.
            tabView.Add(tab);

            // Mount panel content in our content host. Visibility is driven by
            // active-tab state (see UpdateActiveContent).
            p.Content.RemoveFromHierarchy();
            p.Content.style.flexGrow = 1;
            contentHost.Add(p.Content);

            panels.Add(p);
            tabsById[p.Id] = tab;

            // Header is reparented out of Tab into TabView's header-container
            // post-construction; tag it on the next tick so drag detection /
            // close button / reorder all work.
            int headerIndex = panels.Count - 1;
            tabView.schedule.Execute(() => MarkHeader(headerIndex, p)).ExecuteLater(0);

            UpdateActiveContent();
            RefreshPlaceholder();
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

            tabView.Remove(tab);
            panel?.Content.RemoveFromHierarchy();
            tabsById.Remove(id);
            panels.RemoveAll(p => p.Id == id);

            UpdateActiveContent();
            RefreshPlaceholder();
            if (panel != null) PanelClosed?.Invoke(panel);
            return panel;
        }

        public bool IsEmpty => panels.Count == 0;

        public void SelectPanel(string id)
        {
            if (!tabsById.TryGetValue(id, out var tab)) return;
            tabView.activeTab = tab;
            UpdateActiveContent();
        }

        /// <summary>Updates the visible label of an existing tab. Used when the
        /// underlying graph is renamed.</summary>
        public void SetTabTitle(string id, string newTitle)
        {
            if (!tabsById.TryGetValue(id, out var tab)) return;
            tab.label = newTitle ?? "";
        }

        // ────────────── Tab reorder support (Issue 1) ──────────────

        public int IndexOfHeaderAt(Vector2 worldPos)
        {
            var hc = HeaderContainer;
            if (hc == null || hc.childCount == 0) return -1;
            if (!hc.worldBound.Contains(worldPos)) return -1;

            for (int i = 0; i < hc.childCount; i++)
            {
                var h = hc[i];
                var b = h.worldBound;
                float midX = b.x + b.width * 0.5f;
                if (worldPos.x < midX) return i;
            }
            return hc.childCount;
        }

        public void MoveTabToIndex(string id, int targetIndex)
        {
            if (!tabsById.TryGetValue(id, out var tab)) return;
            int currentIndex = panels.FindIndex(p => p.Id == id);
            if (currentIndex < 0) return;
            if (targetIndex > currentIndex) targetIndex--;
            if (targetIndex < 0) targetIndex = 0;
            if (targetIndex >= panels.Count) targetIndex = panels.Count - 1;
            if (targetIndex == currentIndex) return;

            var p = panels[currentIndex];
            panels.RemoveAt(currentIndex);
            panels.Insert(targetIndex, p);

            tabView.Remove(tab);
            tabView.Insert(targetIndex, tab);

            tabView.schedule.Execute(RemarkAllHeaders).ExecuteLater(0);
            tabView.activeTab = tab;
            UpdateActiveContent();
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

        // ────────────── Active-content management ──────────────

        private void OnActiveTabChanged(Tab prev, Tab cur) => UpdateActiveContent();

        private void UpdateActiveContent()
        {
            // Whichever panel matches the active tab is shown; everyone else
            // is display:None so they don't render or take layout space.
            var activePanel = tabView.activeTab?.userData as IDockablePanel;
            if (activePanel == null && panels.Count > 0) activePanel = panels[0];
            foreach (var p in panels)
            {
                p.Content.style.display = (p == activePanel)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
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
