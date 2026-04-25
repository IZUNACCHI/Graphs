using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Graph;
using UnityEngine;

namespace NarrativeTool.Core.Commands
{
    /// <summary>
    /// Move a node to a new position. Mergeable with a previous MoveNodeCmd
    /// on the same node — a drag produces one undo entry.
    /// </summary>
    public sealed class MoveNodeCmd : ICommand
    {
        public string Name => $"Move {nodeId}";

        private readonly GraphData graph;
        private readonly EventBus bus;
        private readonly string nodeId;
        private Vector2 from;
        private Vector2 to;

        public MoveNodeCmd(GraphData graph, EventBus bus, string nodeId, Vector2 from, Vector2 to)
        {
            this.graph = graph; this.bus = bus; this.nodeId = nodeId;
            this.from = from; this.to = to;
        }

        public void Do()
        {
            var node = graph.FindNode(nodeId);
            if (node == null) return;
            node.Position = to;
            bus.Publish(new NodeMovedEvent(graph.Id, nodeId, from, to));
        }

        public void Undo()
        {
            var node = graph.FindNode(nodeId);
            if (node == null) return;
            node.Position = from;
            bus.Publish(new NodeMovedEvent(graph.Id, nodeId, to, from));
        }

        public bool TryMerge(ICommand previous)
        {
            if (previous is MoveNodeCmd prev &&
                prev.nodeId == nodeId &&
                ReferenceEquals(prev.graph, graph))
            {
                from = prev.from;
                return true;
            }
            return false;
        }
    }
}