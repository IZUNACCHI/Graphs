using System.Collections.Generic;
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

        public override VisualElement Element => tabView;
        public TabView TabView => tabView;
        public IReadOnlyList<IDockablePanel> Panels => panels;

        public DockArea()
        {
            tabView = new TabView();
            tabView.AddToClassList("nt-dock-area");
            tabView.style.flexGrow = 1;
        }

        public bool HasPanel(string id) => tabsById.ContainsKey(id);

        public Tab GetTab(string id) => tabsById.TryGetValue(id, out var t) ? t : null;

        public IDockablePanel GetPanel(string id) => panels.Find(p => p.Id == id);

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

            // Notify root (no-op subscribers today; kept for future extensions).
            Zone?.Owner?.RaiseTabAdded(this, tab, p);
        }

        /// <summary>USS class added to each Tab's header element so
        /// <c>DockDragManager</c> can detect header clicks via ancestor walk.</summary>
        public const string HeaderMarkerClass = "nt-dock-tab-header";

        private void MarkHeader(int index, IDockablePanel p)
        {
            var hc = tabView.Q(className: "unity-tab-view__header-container");
            if (hc == null || index < 0 || index >= hc.childCount) return;
            var header = hc[index];
            header.AddToClassList(HeaderMarkerClass);
            header.userData = p;
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
            return panel;
        }

        public bool IsEmpty => panels.Count == 0;

        public void SelectPanel(string id)
        {
            if (!tabsById.TryGetValue(id, out var tab)) return;
            tabView.activeTab = tab;
        }
    }
}
