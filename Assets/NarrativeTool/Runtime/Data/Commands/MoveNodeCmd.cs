using NarrativeTool.Core;
using UnityEngine;

namespace NarrativeTool.Data.Commands
{
    /// <summary>
    /// Moves a node to a new position. Mergeable: a second MoveNodeCmd on the
    /// same node swallows the first, so a drag produces one undo entry instead
    /// of dozens.
    /// </summary>
    public sealed class MoveNodeCmd : ICommand
    {
        public string Name => $"Move {nodeId}";

        private readonly GraphDocument graph;
        private readonly EventBus bus;
        private readonly string nodeId;
        private Vector2 from;  // mutable so merge can extend the range
        private Vector2 to;

        public MoveNodeCmd(GraphDocument graph, EventBus bus, string nodeId, Vector2 from, Vector2 to)
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
                // Keep the earliest origin so undo goes all the way back
                from = prev.from;
                return true;
            }
            return false;
        }
    }
}