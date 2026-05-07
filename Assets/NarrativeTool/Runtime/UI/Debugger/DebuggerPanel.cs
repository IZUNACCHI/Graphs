using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Core.Runtime;
using NarrativeTool.Data.Project;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Debugger
{
    /// <summary>
    /// Dockable Debugger panel with three internal tabs: Watch, Log, Breakpoints.
    /// <para>
    /// Tab strip uses Unity 6's native <see cref="TabView"/> / <see cref="Tab"/>,
    /// but content is mounted in our own <c>contentHost</c> sibling instead of
    /// going through <c>tab.Add(...)</c> — the latter routes through opaque
    /// TabView content-viewport machinery that fails to render in 6.0.3.
    /// Same workaround as <c>DockArea</c>.
    /// </para>
    /// </summary>
    public sealed class DebuggerPanel : VisualElement
    {
        private readonly TabView tabView;
        private readonly VisualElement contentHost;
        private readonly Tab watchHost, logHost, breakpointsHost;

        private WatchTab watchTab;
        private LogTab logTab;
        private BreakpointsTab breakpointsTab;

        public DebuggerPanel()
        {
            AddToClassList("debugger-panel");
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            // Tab strip — header only, no content goes through TabView.
            tabView = new TabView();
            tabView.AddToClassList("debugger-panel__tabview");
            tabView.style.flexGrow = 0;
            tabView.style.flexShrink = 0;

            watchHost       = new Tab("Watch");
            logHost         = new Tab("Log");
            breakpointsHost = new Tab("Breakpoints");
            tabView.Add(watchHost);
            tabView.Add(logHost);
            tabView.Add(breakpointsHost);

            Add(tabView);

            // Content host: actual tab bodies live here, with display
            // toggled to match the active tab.
            contentHost = new VisualElement();
            contentHost.AddToClassList("debugger-panel__content");
            contentHost.style.flexGrow = 1;
            contentHost.style.flexDirection = FlexDirection.Column;
            Add(contentHost);

            tabView.activeTabChanged += (_, _) => UpdateActive();
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

            watchTab.style.flexGrow = 1;
            logTab.style.flexGrow = 1;
            breakpointsTab.style.flexGrow = 1;

            // Mount in contentHost as siblings; visibility is driven by
            // active-tab state below.
            contentHost.Add(watchTab);
            contentHost.Add(logTab);
            contentHost.Add(breakpointsTab);

            tabView.activeTab = watchHost;
            UpdateActive();
        }

        public void Unbind()
        {
            watchTab?.Dispose();
            logTab?.Dispose();
            breakpointsTab?.Dispose();
            watchTab = null;
            logTab = null;
            breakpointsTab = null;

            contentHost.Clear();
        }

        private void UpdateActive()
        {
            // Translate active-tab to which content element to show.
            VisualElement active =
                tabView.activeTab == logHost         ? (VisualElement)logTab :
                tabView.activeTab == breakpointsHost ? (VisualElement)breakpointsTab :
                                                      (VisualElement)watchTab;

            if (watchTab       != null) watchTab.style.display       = (active == watchTab)       ? DisplayStyle.Flex : DisplayStyle.None;
            if (logTab         != null) logTab.style.display         = (active == logTab)         ? DisplayStyle.Flex : DisplayStyle.None;
            if (breakpointsTab != null) breakpointsTab.style.display = (active == breakpointsTab) ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
