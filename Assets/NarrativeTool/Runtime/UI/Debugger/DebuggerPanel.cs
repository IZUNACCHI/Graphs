using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Core.Runtime;
using NarrativeTool.Data.Project;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Debugger
{
    /// <summary>
    /// Dockable Debugger panel with three internal tabs: Watch, Log, Breakpoints.
    /// Phase 3: tabs migrated from custom buttons to Unity 6 native
    /// <see cref="TabView"/> / <see cref="Tab"/>.
    /// </summary>
    public sealed class DebuggerPanel : VisualElement
    {
        private readonly TabView tabView;
        private Tab watchHost, logHost, breakpointsHost;

        private WatchTab watchTab;
        private LogTab logTab;
        private BreakpointsTab breakpointsTab;

        public DebuggerPanel()
        {
            AddToClassList("debugger-panel");
            style.flexGrow = 1;

            tabView = new TabView();
            tabView.AddToClassList("debugger-panel__tabview");
            tabView.style.flexGrow = 1;

            // Hosts are created up-front so the TabView has stable children;
            // their inner content is (re)attached on Bind().
            watchHost       = new Tab("Watch");
            logHost         = new Tab("Log");
            breakpointsHost = new Tab("Breakpoints");
            tabView.Add(watchHost);
            tabView.Add(logHost);
            tabView.Add(breakpointsHost);

            Add(tabView);
        }

        public void Bind(ProjectModel project,
                         RuntimeVariableStore variables,
                         RuntimeEntityStore entities,
                         BreakpointStore breakpoints,
                         EventBus bus,
                         ContextMenuController contextMenu)
        {
            Unbind();

            watchTab       = new WatchTab(project, variables, entities, bus, contextMenu);
            logTab         = new LogTab(bus);
            breakpointsTab = new BreakpointsTab(project, breakpoints, bus, contextMenu);

            watchHost.Add(watchTab);
            logHost.Add(logTab);
            breakpointsHost.Add(breakpointsTab);

            tabView.activeTab = watchHost;
        }

        public void Unbind()
        {
            watchTab?.Dispose();
            logTab?.Dispose();
            breakpointsTab?.Dispose();
            watchTab = null;
            logTab = null;
            breakpointsTab = null;

            watchHost.Clear();
            logHost.Clear();
            breakpointsHost.Clear();
        }
    }
}
