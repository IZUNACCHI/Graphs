using NarrativeTool.Canvas;
using NarrativeTool.Canvas.Manipulators;
using NarrativeTool.Canvas.Views;
using NarrativeTool.Core.Commands;
using NarrativeTool.Core.Utilities.Math;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Core.ContextMenu
{
    // ---------- Canvas (right-click empty canvas) ----------

    // NarrativeTool.Core/ContextMenu/Providers/ContextMenuProviders.cs
    // (section relevant to CanvasContextMenuProvider)

    public sealed class CanvasContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not CanvasContextTarget ctx) return null;
            var canvas = ctx.Canvas;
            var pos = ctx.WorldPosition;

            var registry = Services.TryGet<NodeRegistry>();
            if (registry == null) return null;

            var grouped = registry.GetAll()
                .GroupBy(d => d.Category)
                .OrderBy(g => g.Key);   // keep a stable order

            var items = new List<ContextMenuItem>();
            foreach (var group in grouped)
            {
                // Category header
                items.Add(ContextMenuItem.Header($"Nodes — {group.Key}"));

                foreach (var desc in group)
                {
                    items.Add(ContextMenuItem.Of(
                        $"New {desc.DisplayName}",
                        () => CreateNode(canvas, desc, pos)));
                }
                // Optional small separator between groups
                items.Add(ContextMenuItem.Separator());
            }

            // Remove trailing separator for aesthetics
            if (items.LastOrDefault()?.IsSeparator == true)
                items.RemoveAt(items.Count - 1);

            return items;
        }

        private static void CreateNode(GraphView canvas, NodeTypeDescriptor desc, Vector2 worldPos)
        {
            var node = desc.DataFactory(
                $"{desc.TypeId}_{System.Guid.NewGuid():N}".Substring(0, 16), worldPos);
            canvas.Commands.Execute(new AddNodeCmd(canvas.Graph, canvas.Bus, node));
            var view = canvas.GetNodeView(node.Id);
            if (view != null)
                canvas.Selection.SelectOnly(view);
        }
    }

    // ---------- Node (right-click node body) ----------

    public sealed class NodeContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not NodeContextTarget ctx) return null;
            var nv = ctx.NodeView;
            var canvas = nv.Canvas;

            var items = new List<ContextMenuItem>
        {
            ContextMenuItem.Of("Delete", canvas.DeleteSelected)
        };

            if (nv is ChoiceNodeView choiceView)
            {
                items.Insert(0, ContextMenuItem.Of("Add Option", () => choiceView.AddOption()));
                items.Insert(0, ContextMenuItem.Of(
                    choiceView.DataHasPreamble ? "Hide Preamble" : "Show Preamble",
                    () => TogglePreamble(choiceView.Data, choiceView)));
            }

            return items;
        }

        private void TogglePreamble(ChoiceNodeData data, ChoiceNodeView view)
        {
            // Use a SetPropertyCommand for undo
            var oldVal = data.HasPreamble;
            var newVal = !oldVal;
            view.Canvas.Commands.Execute(new SetPropertyCommand(
                "HasPreamble",
                v => data.HasPreamble = (bool)v,
                oldVal, newVal,
                view.Canvas.Bus));
            view.UpdatePreambleVisibility();   // immediate visual update
        }
    }

    // ---------- Edge (right-click edge body) ----------

    public sealed class EdgeContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not EdgeContextTarget ctx) return null;
            var ev = ctx.EdgeView;
            var canvas = ev.Canvas;
            var world = ctx.WorldPoint;

            var items = new List<ContextMenuItem>
            {
                // "Set label" — opens inline editor (even if empty)
                ContextMenuItem.Of(
                string.IsNullOrEmpty(ev.Edge.Label) ? "Set label…" : "Edit label…",
                ev.BeginEditLabel),

                // "Add waypoint here"
                ContextMenuItem.Of("Add waypoint here", () =>
                {
                    var anchors = ev.GetAnchors();
                    if (anchors.Count < 2) return;
                    var (segIndex, _, closest) = BezierMath.ClosestPointOnPath(world, anchors);
                    int waypointIndex = Mathf.Clamp(segIndex, 0, ev.Edge.Waypoints.Count);
                    canvas.Commands.Execute(new AddWaypointCmd(canvas.Graph, canvas.Bus, ev.Edge.Id, waypointIndex, closest));
                }),
                ContextMenuItem.Separator()
            };

            // Delete (uses DeleteSelected if the edge is part of a
            // multi-selection, otherwise deletes just this edge).
            int selectedCount = canvas.Selection.Count;
            string deleteLabel = selectedCount > 1 ? $"Delete {selectedCount} Items" : "Delete edge";
            items.Add(ContextMenuItem.Of(deleteLabel, canvas.DeleteSelected));

            return items;
        }
    }

    // ---------- Waypoint (right-click on a waypoint) ----------

    public sealed class WaypointContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not WaypointContextTarget ctx) return null;
            var canvas = ctx.EdgeView.Canvas;
            int selectedCount = canvas.Selection.Count;
            string label = selectedCount > 1 ? $"Delete {selectedCount} Items" : "Delete waypoint";
            return new List<ContextMenuItem>
            {
                ContextMenuItem.Of(label, canvas.DeleteSelected),
            };
        }
    }

    // ---------- Edge drop (drag from port to empty canvas) ----------

    /// <summary>
    /// When an edge-creation drag lands in empty canvas, this provider shows
    /// node types that have a matching flow port. Creating one spawns the
    /// node AND an auto-connect edge in one transaction.
    /// </summary>
    public sealed class EdgeDropContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not EdgeDropContextTarget ctx) return null;

            var canvas = ctx.Canvas;
            var sourcePort = ctx.SourcePort;
            var pos = ctx.WorldPosition;

            var registry = Services.TryGet<NodeRegistry>();
            if (registry == null) return null;

            var compatibles = registry.GetCompatibleNodes(sourcePort.Port.Direction,
                                                          sourcePort.Port.TypeTag);
            var items = new List<ContextMenuItem>();

            foreach (var desc in compatibles.OrderBy(d => d.DisplayName))
            {
                items.Add(ContextMenuItem.Of(
                    $"New {desc.DisplayName} (connect)",
                    () => CreateAndConnect(canvas, sourcePort, desc, pos)
                ));
            }

            return items.Count > 0 ? items : null;
        }

        private static void CreateAndConnect(GraphView canvas, PortView sourcePort,
                                             NodeTypeDescriptor targetDesc,
                                             Vector2 worldPos)
        {
            // Create node via registered factory
            var newNode = targetDesc.DataFactory(
                $"{targetDesc.TypeId}_{System.Guid.NewGuid():N}".Substring(0, 16),
                worldPos);

            // Find a compatible port on the new node
            PortDefinition targetPortDef;
            if (sourcePort.Port.Direction == PortDirection.Output)
                targetPortDef = targetDesc.Ports.First(
                    p => p.Direction == PortDirection.Input
                         && p.TypeTag == sourcePort.Port.TypeTag);
            else
                targetPortDef = targetDesc.Ports.First(
                    p => p.Direction == PortDirection.Output
                         && p.TypeTag == sourcePort.Port.TypeTag);

            // Build the edge (always From=output, To=input)
            string fromNodeId, fromPortId, toNodeId, toPortId;
            if (sourcePort.Port.Direction == PortDirection.Output)
            {
                fromNodeId = sourcePort.OwnerNode.Node.Id;
                fromPortId = sourcePort.Port.Id;
                toNodeId = newNode.Id;
                toPortId = targetPortDef.PortId;
            }
            else
            {
                fromNodeId = newNode.Id;
                fromPortId = targetPortDef.PortId;
                toNodeId = sourcePort.OwnerNode.Node.Id;
                toPortId = sourcePort.Port.Id;
            }

            var edge = new Edge(
                "e_" + System.Guid.NewGuid().ToString("N").Substring(0, 8),
                fromNodeId, fromPortId,
                toNodeId, toPortId);

            using (canvas.Commands.BeginTransaction("Create node + edge"))
            {
                canvas.Commands.Execute(new AddNodeCmd(canvas.Graph, canvas.Bus, newNode));
                canvas.Commands.Execute(new AddEdgeCmd(canvas.Graph, canvas.Bus, edge));
            }

            var nv = canvas.GetNodeView(newNode.Id);
            if (nv != null)
                canvas.Selection.SelectOnly(nv);
        }
    }
}