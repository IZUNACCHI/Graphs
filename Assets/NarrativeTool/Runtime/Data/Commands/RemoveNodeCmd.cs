using System.Collections.Generic;
using NarrativeTool.Core;

namespace NarrativeTool.Data.Commands
{
    /// <summary>
    /// Remove a node and, as a side effect, any edges touching it. Undo
    /// restores both.
    /// </summary>
    public sealed class RemoveNodeCmd : ICommand
    {
        public string Name => $"Remove node {nodeId}";

        private readonly GraphDocument graph;
        private readonly EventBus bus;
        private readonly string nodeId;

        private Node removedNode;
        private List<Edge> removedEdges;

        public RemoveNodeCmd(GraphDocument graph, EventBus bus, string nodeId)
        {
            this.graph = graph; this.bus = bus; this.nodeId = nodeId;
        }

        public void Do()
        {
            var node = graph.FindNode(nodeId);
            if (node == null) return;

            var connected = new List<Edge>();
            foreach (var e in graph.Edges)
                if (e.FromNodeId == nodeId || e.ToNodeId == nodeId)
                    connected.Add(e);

            foreach (var e in connected)
            {
                graph.Edges.Remove(e);
                bus.Publish(new EdgeRemovedEvent(graph.Id, e.Id));
            }

            graph.Nodes.Remove(node);
            bus.Publish(new NodeRemovedEvent(graph.Id, nodeId));

            removedNode = node;
            removedEdges = connected;
        }

        public void Undo()
        {
            if (removedNode == null) return;
            graph.Nodes.Add(removedNode);
            bus.Publish(new NodeAddedEvent(graph.Id, removedNode.Id));

            if (removedEdges != null)
            {
                foreach (var e in removedEdges)
                {
                    graph.Edges.Add(e);
                    bus.Publish(new EdgeAddedEvent(graph.Id, e.Id));
                }
            }
        }

        public bool TryMerge(ICommand previous) => false;
    }
}