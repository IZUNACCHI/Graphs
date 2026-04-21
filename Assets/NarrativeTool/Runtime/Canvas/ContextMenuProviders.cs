using NarrativeTool.Core;
using NarrativeTool.Data;
using NarrativeTool.Data.Commands;
using System.Collections.Generic;
using UnityEngine;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace NarrativeTool.Canvas
{
    // ---------- Canvas (right-click empty canvas) ----------

    public sealed class CanvasContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not CanvasContextTarget ctx) return null;
            var canvas = ctx.Canvas;
            var pos = ctx.WorldPosition;

            return new List<ContextMenuItem>
            {
                ContextMenuItem.Of("New Start Node", () => CreateStart(canvas, pos)),
                ContextMenuItem.Of("New Text Node",  () => CreateText (canvas, pos)),
                ContextMenuItem.Of("New End Node",   () => CreateEnd  (canvas, pos)),
            };
        }

        private static void CreateStart(GraphCanvas c, Vector2 pos)
        {
            var node = new StartNode(NewId("start"), pos);
            Create(c, node);
        }

        private static void CreateText(GraphCanvas c, Vector2 pos)
        {
            var node = new TextNode(NewId("text"), "Text Node", pos, "");
            Create(c, node);
        }

        private static void CreateEnd(GraphCanvas c, Vector2 pos)
        {
            var node = new EndNode(NewId("end"), pos);
            Create(c, node);
        }

        private static void Create(GraphCanvas c, Node node)
        {
            c.Commands.Execute(new AddNodeCmd(c.Graph, c.Bus, node));
            var view = c.GetNodeView(node.Id);
            if (view != null) c.Selection.SelectOnly(view);
        }

        private static string NewId(string prefix)
            => $"{prefix}_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";
    }

    // ---------- Node (right-click node body) ----------

    public sealed class NodeContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not NodeContextTarget ctx) return null;
            var nv = ctx.NodeView;
            var canvas = nv.Canvas;
            int selectedCount = canvas.Selection.Count;

            string label = selectedCount > 1
                ? $"Delete {selectedCount} Items"
                : "Delete";

            return new List<ContextMenuItem>
            {
                ContextMenuItem.Of(label, canvas.DeleteSelected),
            };
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

            var items = new List<ContextMenuItem>();

            // "Set label" — opens inline editor (even if empty)
            items.Add(ContextMenuItem.Of(
                string.IsNullOrEmpty(ev.Edge.Label) ? "Set label…" : "Edit label…",
                ev.BeginEditLabel));

            // "Add waypoint here"
            items.Add(ContextMenuItem.Of("Add waypoint here", () =>
            {
                var anchors = ev.GetAnchors();
                if (anchors.Count < 2) return;
                var (segIndex, _, closest) = BezierMath.ClosestPointOnPath(world, anchors);
                int waypointIndex = Mathf.Clamp(segIndex, 0, ev.Edge.Waypoints.Count);
                canvas.Commands.Execute(new AddWaypointCmd(canvas.Graph, canvas.Bus, ev.Edge.Id, waypointIndex, closest));
            }));

            items.Add(ContextMenuItem.Separator());

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

            var items = new List<ContextMenuItem>();

            // Source direction determines which node-types make sense:
            //  - If source is output: we need a node with a matching input.
            //  - If source is input: we need a node with a matching output.
            // For v1 the catalog is hardcoded (Start/Text/End).
            // When we add NodeTypeRegistry, this list becomes dynamic.

            if (sourcePort.Port.Direction == PortDirection.Output)
            {
                // Need an input on the new node. Text and End have flow inputs.
                items.Add(ContextMenuItem.Of("New Text Node (connect)",
                    () => CreateAndConnect(canvas, sourcePort, pos, "text")));
                items.Add(ContextMenuItem.Of("New End Node (connect)",
                    () => CreateAndConnect(canvas, sourcePort, pos, "end")));
            }
            else
            {
                // Need an output on the new node. Start and Text have flow outputs.
                items.Add(ContextMenuItem.Of("New Start Node (connect)",
                    () => CreateAndConnect(canvas, sourcePort, pos, "start")));
                items.Add(ContextMenuItem.Of("New Text Node (connect)",
                    () => CreateAndConnect(canvas, sourcePort, pos, "text")));
            }

            return items;
        }

        private static void CreateAndConnect(GraphCanvas canvas, PortView sourcePort, Vector2 pos, string kind)
        {
            Node newNode;
            string inPortId, outPortId;

            switch (kind)
            {
                case "start":
                    newNode = new StartNode(NewId("start"), pos);
                    inPortId = null; outPortId = StartNode.OutputPortId;
                    break;
                case "text":
                    newNode = new TextNode(NewId("text"), "Text Node", pos, "");
                    inPortId = TextNode.InputPortId; outPortId = TextNode.OutputPortId;
                    break;
                case "end":
                    newNode = new EndNode(NewId("end"), pos);
                    inPortId = EndNode.InputPortId; outPortId = null;
                    break;
                default: return;
            }

            // Determine edge direction (From = output, To = input).
            string fromNodeId, fromPortId, toNodeId, toPortId;
            if (sourcePort.Port.Direction == PortDirection.Output)
            {
                fromNodeId = sourcePort.OwnerNode.Node.Id;
                fromPortId = sourcePort.Port.Id;
                toNodeId = newNode.Id;
                toPortId = inPortId;
            }
            else
            {
                fromNodeId = newNode.Id;
                fromPortId = outPortId;
                toNodeId = sourcePort.OwnerNode.Node.Id;
                toPortId = sourcePort.Port.Id;
            }
            if (fromPortId == null || toPortId == null) return;

            var newEdge = new Edge(NewEdgeId(), fromNodeId, fromPortId, toNodeId, toPortId);

            using (canvas.Commands.BeginTransaction("Create node + edge"))
            {
                canvas.Commands.Execute(new AddNodeCmd(canvas.Graph, canvas.Bus, newNode));
                canvas.Commands.Execute(new AddEdgeCmd(canvas.Graph, canvas.Bus, newEdge));
            }

            var nv = canvas.GetNodeView(newNode.Id);
            if (nv != null) canvas.Selection.SelectOnly(nv);
        }

        private static string NewId(string prefix)
            => $"{prefix}_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";
        private static string NewEdgeId()
            => $"e_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";
    }
}