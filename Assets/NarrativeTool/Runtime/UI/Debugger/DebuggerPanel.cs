using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Core.Runtime;
using NarrativeTool.Data.Project;
using System;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Debugger
{
    /// <summary>
    /// Dockable Debugger panel with three tabs: Watch, Log, Breakpoints.
    /// Built and shown alongside the RuntimePanel during a run.
    /// </summary>
    public sealed class DebuggerPanel : VisualElement
    {
        private readonly Button tabWatch, tabLog, tabBreakpoints;
        private readonly VisualElement contentArea;

        private WatchTab watchTab;
        private LogTab logTab;
        private BreakpointsTab breakpointsTab;

        private ProjectModel project;
        private RuntimeVariableStore variables;
        private RuntimeEntityStore entities;
        private BreakpointStore breakpoints;
        private EventBus bus;
        private ContextMenuController contextMenu;

        private enum Tab { Watch, Log, Breakpoints }

        public DebuggerPanel()
        {
            AddToClassList("debugger-panel");
            style.flexGrow = 1;

            // Title bar with tab buttons
            var titleBar = new VisualElement();
            titleBar.AddToClassList("debugger-panel__titlebar");

            var title = new Label("Debugger");
            title.AddToClassList("debugger-panel__title");
            titleBar.Add(title);

            var tabs = new VisualElement();
            tabs.AddToClassList("debugger-panel__tabs");

            tabWatch = MakeTabButton("Watch", () => Switch(Tab.Watch));
            tabLog = MakeTabButton("Log", () => Switch(Tab.Log));
            tabBreakpoints = MakeTabButton("Breakpoints", () => Switch(Tab.Breakpoints));

            tabs.Add(tabWatch);
            tabs.Add(tabLog);
            tabs.Add(tabBreakpoints);
            titleBar.Add(tabs);

            Add(titleBar);

            contentArea = new VisualElement();
            contentArea.style.flexGrow = 1;
            contentArea.style.flexDirection = FlexDirection.Column;
            Add(contentArea);
        }

        private static Button MakeTabButton(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("debugger-tab");
            return b;
        }

        public void Bind(ProjectModel project,
                         RuntimeVariableStore variables,
                         RuntimeEntityStore entities,
                         BreakpointStore breakpoints,
                         EventBus bus,
                         ContextMenuController contextMenu)
        {
            Unbind();
            this.project = project;
            this.variables = variables;
            this.entities = entities;
            this.breakpoints = breakpoints;
            this.bus = bus;
            this.contextMenu = contextMenu;

            watchTab = new WatchTab(project, variables, entities, bus, contextMenu);
            logTab = new LogTab(bus);
            breakpointsTab = new BreakpointsTab(project, breakpoints, bus, contextMenu);

            Switch(Tab.Watch);
        }

        public void Unbind()
        {
            watchTab?.Dispose();
            logTab?.Dispose();
            breakpointsTab?.Dispose();
            watchTab = null;
            logTab = null;
            breakpointsTab = null;
            contentArea.Clear();
            project = null;
            variables = null;
            entities = null;
            breakpoints = null;
            bus = null;
            contextMenu = null;
        }

        private void Switch(Tab tab)
        {
            contentArea.Clear();

            tabWatch.EnableInClassList("debugger-tab--active", tab == Tab.Watch);
            tabLog.EnableInClassList("debugger-tab--active", tab == Tab.Log);
            tabBreakpoints.EnableInClassList("debugger-tab--active", tab == Tab.Breakpoints);

            VisualElement panel = tab switch
            {
                Tab.Watch => watchTab,
                Tab.Log => logTab,
                Tab.Breakpoints => breakpointsTab,
                _ => null,
            };
            if (panel != null) contentArea.Add(panel);
        }
    }
}
