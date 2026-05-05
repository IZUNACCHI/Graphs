using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Core.Runtime;
using NarrativeTool.Data.Project;
using System;
using System.Linq;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Debugger
{
    /// <summary>
    /// Breakpoints tab. Shows every breakpoint registered in <see cref="BreakpointStore"/>
    /// with a status dot (red = active, gray = disabled). Right-click a row to
    /// toggle or remove. New breakpoints are added via the graph-canvas
    /// node context menu.
    /// </summary>
    public sealed class BreakpointsTab : VisualElement, IDisposable
    {
        private readonly ProjectModel project;
        private readonly BreakpointStore breakpoints;
        private readonly EventBus bus;
        private readonly ContextMenuController contextMenu;

        private readonly VisualElement listContainer;
        private readonly Label footerSummary;

        private IDisposable subAdded, subRemoved, subToggled;

        public BreakpointsTab(ProjectModel project, BreakpointStore breakpoints,
            EventBus bus, ContextMenuController contextMenu)
        {
            this.project = project;
            this.breakpoints = breakpoints;
            this.bus = bus;
            this.contextMenu = contextMenu;

            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            listContainer = new VisualElement();
            listContainer.style.flexDirection = FlexDirection.Column;
            scroll.Add(listContainer);
            Add(scroll);

            var hint = new Label("Right-click a node on the canvas to add a breakpoint.");
            hint.AddToClassList("debugger-bp__hint");
            Add(hint);

            var footer = new VisualElement();
            footer.AddToClassList("debugger-panel__footer");
            footerSummary = new Label("0 breakpoints");
            footerSummary.AddToClassList("debugger-panel__footer-label");
            footer.Add(footerSummary);
            var clear = new Button(() => breakpoints?.Clear()) { text = "Clear all" };
            clear.AddToClassList("debugger-panel__footer-btn");
            footer.Add(clear);
            Add(footer);

            subAdded = bus?.Subscribe<BreakpointAddedEvent>(_ => Refresh());
            subRemoved = bus?.Subscribe<BreakpointRemovedEvent>(_ => Refresh());
            subToggled = bus?.Subscribe<BreakpointToggledEvent>(_ => Refresh());

            Refresh();
        }

        public void Dispose()
        {
            subAdded?.Dispose(); subRemoved?.Dispose(); subToggled?.Dispose();
            subAdded = subRemoved = subToggled = null;
        }

        public void Refresh()
        {
            listContainer.Clear();
            int active = 0, total = 0;
            if (breakpoints != null)
            {
                foreach (var key in breakpoints.GetAll())
                {
                    bool enabled = breakpoints.IsEnabled(key.GraphId, key.NodeId);
                    if (enabled) active++;
                    total++;
                    listContainer.Add(BuildRow(key, enabled));
                }
            }
            footerSummary.text = $"{total} breakpoints · {active} active";
        }

        private VisualElement BuildRow(BreakpointStore.Key key, bool enabled)
        {
            var row = new VisualElement();
            row.AddToClassList("debugger-bp__row");

            var dot = new VisualElement();
            dot.AddToClassList("debugger-bp__dot");
            dot.AddToClassList(enabled ? "debugger-bp__dot--active" : "debugger-bp__dot--disabled");
            dot.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button == 0)
                {
                    breakpoints?.SetEnabled(key.GraphId, key.NodeId, !enabled);
                    e.StopPropagation();
                }
            });
            row.Add(dot);

            var info = new VisualElement();
            info.AddToClassList("debugger-bp__info");

            var nodeName = new Label(ResolveNodeLabel(key));
            nodeName.AddToClassList("debugger-bp__node-name");
            if (!enabled) nodeName.AddToClassList("debugger-bp__node-name--disabled");
            info.Add(nodeName);

            var location = new Label($"{ResolveGraphLabel(key.GraphId)} · on enter");
            location.AddToClassList("debugger-bp__location");
            if (!enabled) location.AddToClassList("debugger-bp__location--disabled");
            info.Add(location);

            row.Add(info);

            var remove = new Button(() => breakpoints?.Remove(key.GraphId, key.NodeId)) { text = "✕" };
            remove.AddToClassList("debugger-bp__remove-btn");
            row.Add(remove);

            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 1)
                {
                    contextMenu?.Open(new BreakpointContextTarget(this, key, enabled), evt.mousePosition);
                    evt.StopPropagation();
                }
            });

            return row;
        }

        private string ResolveNodeLabel(BreakpointStore.Key key)
        {
            var graph = project?.Graphs.Items.FirstOrDefault(g => g.Id == key.GraphId);
            var node = graph?.GetGraph()?.Nodes.FirstOrDefault(n => n.Id == key.NodeId);
            return node != null && !string.IsNullOrEmpty(node.Title) ? node.Title : key.NodeId;
        }

        private string ResolveGraphLabel(string graphId)
        {
            var graph = project?.Graphs.Items.FirstOrDefault(g => g.Id == graphId);
            return graph != null ? graph.Name : graphId;
        }

        public void Toggle(BreakpointStore.Key key) =>
            breakpoints?.SetEnabled(key.GraphId, key.NodeId,
                !breakpoints.IsEnabled(key.GraphId, key.NodeId));

        public void Remove(BreakpointStore.Key key) =>
            breakpoints?.Remove(key.GraphId, key.NodeId);
    }

    public sealed class BreakpointContextTarget
    {
        public BreakpointsTab Tab { get; }
        public BreakpointStore.Key Key { get; }
        public bool Enabled { get; }
        public BreakpointContextTarget(BreakpointsTab tab, BreakpointStore.Key key, bool enabled)
        { Tab = tab; Key = key; Enabled = enabled; }
    }
}
