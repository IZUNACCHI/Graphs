using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas.Core
{
    public sealed class GraphTabManager : VisualElement
    {
        private readonly VisualElement tabStrip;
        private readonly VisualElement contentArea;

        private readonly List<GraphTab> tabs = new();
        private GraphTab activeTab;

        public GraphTab ActiveTab => activeTab;

        /// <summary>All currently open tabs, in order of creation.</summary>
        public IReadOnlyList<GraphTab> AllTabs => tabs;

        private readonly SessionState session;
        private readonly ContextMenuController contextMenu;

        public event System.Action OnActiveTabChanged;

        // dictionary mapping tab to its tab element + the name label for rename updates
        private readonly Dictionary<GraphTab, VisualElement> tabElements = new();
        private readonly Dictionary<GraphTab, Label> tabNameLabels = new();

        public GraphTabManager(SessionState session, ContextMenuController contextMenu)
        {
            this.session = session;
            this.contextMenu = contextMenu;
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            tabStrip = new VisualElement();
            tabStrip.AddToClassList("nt-tabstrip");
            tabStrip.style.flexDirection = FlexDirection.Row;
            Add(tabStrip);

            contentArea = new VisualElement();
            contentArea.style.flexGrow = 1;
            Add(contentArea);
        }

        public GraphTab OpenGraph(LazyGraph lazy)
        {
            var existing = tabs.FirstOrDefault(t => t.Lazy.Id == lazy.Id);
            if (existing != null)
            {
                SetActiveTab(existing);
                return existing;
            }

            var tab = new GraphTab(lazy, session, contextMenu);
            // Subscribe to navigation requests from the new tab's view
            tab.View.OnNavigateToGraph += graphId =>
            {
                var lazy = session.Project?.Graphs.Items.FirstOrDefault(g => g.Id == graphId);
                if (lazy != null) OpenGraph(lazy);
            };

            tab.View.OnNavigateToNode += nodeId =>
            {
                // For now, simply frame the node in the current graph.
                // (We assume the node is in the same graph – if cross‑graph, you’d open the correct graph first.)
                tab.View.FrameNode(nodeId);
            };
            tabs.Add(tab);
            AddTabToStrip(tab);
            SetActiveTab(tab);
            return tab;
        }

        public void CloseTab(GraphTab tab, bool saveBeforeClose = true)
        {
            if (saveBeforeClose && tab.IsDirty)
                tab.Save();

            // Remove visual element
            if (tabElements.TryGetValue(tab, out var el))
            {
                el.RemoveFromHierarchy();
                tabElements.Remove(tab);
                tabNameLabels.Remove(tab);
            }

            tabs.Remove(tab);

            if (activeTab == tab)
            {
                activeTab = null;
                if (tab.View != null)
                    tab.View.style.display = DisplayStyle.None;
            }

            tab.Dispose();

            if (tabs.Count > 0)
                SetActiveTab(tabs[0]);
        }

        private void AddTabToStrip(GraphTab tab)
        {
            // Outer container  flex row so name + close sit side by side
            var container = new VisualElement();
            container.AddToClassList("nt-tab-btn");
            container.focusable = true;

            // Graph name
            var nameLabel = new Label(tab.Lazy.Name);
            nameLabel.AddToClassList("nt-tab-label");
            container.Add(nameLabel);

            // Close button  fixed small size, no stretching
            var closeBtn = new Label("x");
            closeBtn.AddToClassList("nt-tab-close");
            closeBtn.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 0)
                {
                    CloseTab(tab);
                    e.StopPropagation();          // don't trigger the tab switch
                }
            });
            container.Add(closeBtn);

            // Click anywhere else on the container to switch to that tab
            container.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 0)
                {
                    SetActiveTab(tab);
                    // no need to stop propagation (only one tab strip)
                }
            });

            tabElements[tab] = container;
            tabNameLabels[tab] = nameLabel;
            tabStrip.Add(container);
        }

        private void SetActiveTab(GraphTab tab)
        {
            if (activeTab?.View != null)
                activeTab.View.style.display = DisplayStyle.None;

            activeTab = tab;
            if (tab?.View != null)
            {
                if (!contentArea.Contains(tab.View))
                    contentArea.Add(tab.View);
                tab.View.style.display = DisplayStyle.Flex;
            }

            foreach (var kv in tabElements)
                kv.Value.EnableInClassList("active-tab", kv.Key == tab);

            OnActiveTabChanged?.Invoke();
        }

        public void SaveActiveTab() => activeTab?.Save();

        // Event bus subscriptions 
        private IDisposable graphRemovedSub;
        private IDisposable graphRenamedSub;

        public void SubscribeToEvents(EventBus bus)
        {
            graphRemovedSub = bus.Subscribe<GraphRemovedEvent>(e =>
            {
                var tab = tabs.FirstOrDefault(t => t.Lazy.Id == e.GraphId);
                if (tab != null)
                    CloseTab(tab, saveBeforeClose: false);
            });

            graphRenamedSub = bus.Subscribe<GraphRenamedEvent>(e =>
            {
                var tab = tabs.FirstOrDefault(t => t.Lazy.Id == e.GraphId);
                if (tab != null && tabNameLabels.TryGetValue(tab, out var lbl))
                    lbl.text = e.NewName;
            });
        }

        public void UnsubscribeEvents()
        {
            graphRemovedSub?.Dispose(); graphRemovedSub = null;
            graphRenamedSub?.Dispose(); graphRenamedSub = null;
        }
    }
}