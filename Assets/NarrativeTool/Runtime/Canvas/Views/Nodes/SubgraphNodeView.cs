using NarrativeTool.Canvas;
using NarrativeTool.Core.Commands;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas.Views
{
    [NodeViewOf(typeof(SubgraphNodeData))]
    public sealed class SubgraphNodeView : NodeView
    {
        private DropdownField graphDropdown;
        private System.IDisposable graphRenamedSub, graphAddedSub, graphRemovedSub;

        public SubgraphNodeView(NodeData node, GraphView canvas) : base(node, canvas)
        {
            // ── Dropdown for selecting the target graph ──
            graphDropdown = new DropdownField("Graph");
            graphDropdown.AddToClassList("nt-prop-field");
            graphDropdown.AddToClassList("nt-subgraph-dropdown");

            // Populate when the node first appears
            RegisterCallback<GeometryChangedEvent>(_ => PopulateDropdown());
            PopulateDropdown();

            graphDropdown.RegisterValueChangedCallback(evt =>
            {
                var sub = (SubgraphNodeData)Node;
                var project = Canvas.Session?.Project;
                if (project == null) return;

                var chosen = project.Graphs.Items.FirstOrDefault(g => g.Name == evt.newValue);
                if (chosen == null) return;

                string newId = chosen.Id;
                if (newId == sub.ReferencedGraphId) return;

                Canvas.Commands.Execute(new SetPropertyCommand(
                    "ReferencedGraphId",
                    v => sub.ReferencedGraphId = (string)v,
                    sub.ReferencedGraphId,
                    newId,
                    Canvas.Bus));
            });

            extrasContainer.Add(graphDropdown);

            // ── Double‑click navigates to the referenced graph ──
            RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 0 && e.clickCount == 2)
                {
                    var sub = (SubgraphNodeData)Node;
                    if (!string.IsNullOrEmpty(sub.ReferencedGraphId))
                    {
                        Canvas.NavigateToGraph(sub.ReferencedGraphId);
                        e.StopPropagation();
                    }
                }
            });

            // ── Subscribe to graph rename events to keep dropdown in sync ──
            graphRenamedSub = canvas.Bus.Subscribe<GraphRenamedEvent>(e =>
            {
                PopulateDropdown();
            });

            // Also refresh if graphs are added/removed
            graphAddedSub = canvas.Bus.Subscribe<GraphAddedEvent>(_ => PopulateDropdown());
            graphRemovedSub = canvas.Bus.Subscribe<GraphRemovedEvent>(_ => PopulateDropdown());

            // Clean up subscriptions when the view is removed
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                graphRenamedSub?.Dispose();
                graphAddedSub?.Dispose();
                graphRemovedSub?.Dispose();
            });
        }

        private void PopulateDropdown()
        {
            var sub = (SubgraphNodeData)Node;
            var project = Canvas.Session?.Project;
            if (project == null) return;

            // All graphs are now allowed (including the current one)
            var choices = project.Graphs.Items
                .Select(g => g.Name)
                .ToList();

            graphDropdown.choices = choices;

            // Set current value without triggering change event
            if (!string.IsNullOrEmpty(sub.ReferencedGraphId))
            {
                var target = project.Graphs.Items.FirstOrDefault(g => g.Id == sub.ReferencedGraphId);
                graphDropdown.SetValueWithoutNotify(target != null ? target.Name : "(missing)");
            }
            else if (choices.Count > 0)
                graphDropdown.SetValueWithoutNotify(choices[0]);
        }

        protected override void BuildCustomBody() { }
    }
}