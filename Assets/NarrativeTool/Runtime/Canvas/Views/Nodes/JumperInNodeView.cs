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
    [NodeViewOf(typeof(JumperInNodeData))]
    public sealed class JumperInNodeView : NodeView
    {
        private DropdownField targetDropdown;
        private System.IDisposable nodeAddedSub;

        public JumperInNodeView(NodeData node, GraphView canvas) : base(node, canvas)
        {
            // ── Dropdown to select the target Out jumper ──
            targetDropdown = new DropdownField("Target Out");
            targetDropdown.AddToClassList("nt-prop-field");
            extrasContainer.Add(targetDropdown);

            RegisterCallback<GeometryChangedEvent>(_ => PopulateDropdown());

            targetDropdown.RegisterValueChangedCallback(evt =>
            {
                var inNode = (JumperInNodeData)Node;
                var outNode = FindOutNodeByName(evt.newValue);
                if (outNode == null) return;
                string newId = outNode.Id;
                if (newId == inNode.TargetOutNodeId) return;

                Canvas.Commands.Execute(new SetPropertyCommand(
                    "TargetOutNodeId",
                    v => inNode.TargetOutNodeId = (string)v,
                    inNode.TargetOutNodeId,
                    newId,
                    Canvas.Bus));
            });

            // ── Double‑click navigates to the paired Out node ──
            RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 0 && e.clickCount == 2)
                {
                    var inNode = (JumperInNodeData)Node;
                    if (!string.IsNullOrEmpty(inNode.TargetOutNodeId))
                    {
                        var outNode = Canvas.Graph.Nodes.OfType<JumperOutNodeData>().FirstOrDefault(n => n.Id == inNode.TargetOutNodeId);
                        if (outNode != null)
                        {
                            Canvas.NavigateToNode(outNode.Id);
                            e.StopPropagation();
                        }
                    }
                }
            });

            // ── Listen for new Out nodes being added to the graph ──
            nodeAddedSub = canvas.Bus.Subscribe<NodeAddedEvent>(e =>
            {
                if (canvas.Graph == null || e.GraphId != canvas.Graph.Id) return;
                var addedNode = canvas.Graph.FindNode(e.NodeId);
                if (addedNode is JumperOutNodeData)
                    PopulateDropdown();
            });

            // Clean up subscription when the view is removed
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                nodeAddedSub?.Dispose();
                nodeAddedSub = null;
            });
        }

        private void PopulateDropdown()
        {
            var inNode = (JumperInNodeData)Node;
            var allOutNodes = Canvas.Graph.Nodes.OfType<JumperOutNodeData>().ToList();
            var choices = allOutNodes.Select(n => n.Title).ToList();
            targetDropdown.choices = choices;

            // Set current value without triggering change event
            if (!string.IsNullOrEmpty(inNode.TargetOutNodeId))
            {
                var current = allOutNodes.FirstOrDefault(n => n.Id == inNode.TargetOutNodeId);
                targetDropdown.SetValueWithoutNotify(current != null ? current.Title : inNode.TargetOutNodeId);
            }
            else if (choices.Count > 0)
            {
                // No target assigned yet – automatically assign the first Out node
                var firstOut = allOutNodes[0];
                targetDropdown.SetValueWithoutNotify(firstOut.Title);
                Canvas.Commands.Execute(new SetPropertyCommand(
                    "TargetOutNodeId",
                    v => inNode.TargetOutNodeId = (string)v,
                    "",
                    firstOut.Id,
                    Canvas.Bus));
            }
            else
            {
                // No Out nodes exist, leave empty
                targetDropdown.SetValueWithoutNotify("");
            }
        }

        private JumperOutNodeData FindOutNodeByName(string name)
        {
            return Canvas.Graph.Nodes.OfType<JumperOutNodeData>().FirstOrDefault(n => n.Title == name);
        }

        protected override void BuildCustomBody() { }
    }
}