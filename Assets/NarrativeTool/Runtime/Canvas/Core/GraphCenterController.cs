using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;
using NarrativeTool.UI.Docking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NarrativeTool.Canvas.Core
{
    /// <summary>
    /// Owns the lifecycle of <see cref="GraphPanel"/> instances inside the
    /// center <see cref="DockZone"/>. Replaces the old <c>GraphTabManager</c>:
    /// it doesn't render anything itself — the dock framework handles tab
    /// rendering, splitting, reorder, and drag uniformly with side panels.
    /// </summary>
    public sealed class GraphCenterController
    {
        private readonly DockRoot dock;
        private readonly SessionState session;
        private readonly ContextMenuController contextMenu;

        private readonly Dictionary<string, GraphPanel> panelsByGraphId = new();

        private IDisposable graphRemovedSub;
        private IDisposable graphRenamedSub;

        public IReadOnlyCollection<GraphPanel> AllPanels => panelsByGraphId.Values;

        /// <summary>Fires whenever the center has just transitioned from one
        /// graph open to none. Subscribers (MainWindow) re-open the Graphs
        /// sidebar panel so the user can find their way back.</summary>
        public event Action OnCenterEmpty;

        public GraphCenterController(DockRoot dock, SessionState session, ContextMenuController contextMenu)
        {
            this.dock = dock;
            this.session = session;
            this.contextMenu = contextMenu;

            // When a graph tab is closed via its X button (or any future
            // close path), drop our dictionary entry & dispose the view.
            foreach (var area in dock.Center.AllAreas())
                area.PanelClosed += OnPanelClosedInCenter;
            dock.TabAdded += OnTabAdded;
        }

        public GraphPanel ActivePanel
        {
            get
            {
                foreach (var area in dock.Center.AllAreas())
                {
                    if (area.TabView.activeTab?.userData is GraphPanel gp) return gp;
                }
                return null;
            }
        }

        public GraphPanel OpenGraph(LazyGraph lazy)
        {
            if (lazy == null) return null;
            if (panelsByGraphId.TryGetValue(lazy.Id, out var existing))
            {
                dock.FindArea(existing.Id)?.SelectPanel(existing.Id);
                return existing;
            }

            var panel = new GraphPanel(lazy, session, contextMenu);
            panel.View.OnNavigateToGraph += navId =>
            {
                var l = session.Project?.Graphs.Items.FirstOrDefault(g => g.Id == navId);
                if (l != null) OpenGraph(l);
            };
            panel.View.OnNavigateToNode += panel.View.FrameNode;
            panelsByGraphId[lazy.Id] = panel;

            dock.OpenPanel(panel, DockZoneKind.Center);
            dock.FindArea(panel.Id)?.SelectPanel(panel.Id);
            return panel;
        }

        public void CloseGraph(string graphId, bool saveBeforeClose = true)
        {
            if (!panelsByGraphId.TryGetValue(graphId, out var panel)) return;
            if (saveBeforeClose && panel.IsDirty) panel.Save();
            dock.ClosePanel(panel.Id);
            panel.Dispose();
            panelsByGraphId.Remove(graphId);
            if (panelsByGraphId.Count == 0) OnCenterEmpty?.Invoke();
        }

        public void SaveActiveTab() => ActivePanel?.Save();

        public void SaveAllTabs()
        {
            foreach (var p in panelsByGraphId.Values)
                if (p.IsDirty) p.Save();
        }

        public void SubscribeToEvents(EventBus bus)
        {
            graphRemovedSub = bus.Subscribe<GraphRemovedEvent>(e => CloseGraph(e.GraphId, saveBeforeClose: false));
            graphRenamedSub = bus.Subscribe<GraphRenamedEvent>(e =>
            {
                if (panelsByGraphId.TryGetValue(e.GraphId, out var panel))
                    dock.FindArea(panel.Id)?.SetTabTitle(panel.Id, e.NewName);
            });
        }

        public void UnsubscribeEvents()
        {
            graphRemovedSub?.Dispose(); graphRemovedSub = null;
            graphRenamedSub?.Dispose(); graphRenamedSub = null;
        }

        // ────────────── Wiring helpers ──────────────

        private void OnTabAdded(DockArea area, UnityEngine.UIElements.Tab tab, IDockablePanel panel)
        {
            // Only act on areas inside the center zone.
            if (area.Zone == null || area.Zone.Kind != DockZoneKind.Center) return;
            // PanelClosed is on the area itself — re-subscribe for newly created
            // areas (created by the user splitting the center).
            area.PanelClosed -= OnPanelClosedInCenter;
            area.PanelClosed += OnPanelClosedInCenter;
        }

        private void OnPanelClosedInCenter(IDockablePanel panel)
        {
            if (panel is GraphPanel gp && panelsByGraphId.ContainsKey(gp.Lazy.Id))
            {
                gp.Dispose();
                panelsByGraphId.Remove(gp.Lazy.Id);
                if (panelsByGraphId.Count == 0) OnCenterEmpty?.Invoke();
            }
        }
    }
}
