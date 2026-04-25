using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Graph;
using UnityEngine;

namespace NarrativeTool.Core.Commands
{
    /// <summary>
    /// Move a waypoint. Mergeable with a previous MoveWaypointCmd on the
    /// same (edge, index) so a drag produces one undo entry.
    /// </summary>
    public sealed class MoveWaypointCmd : ICommand
    {
        public string Name => $"Move waypoint {index} on {edgeId}";

        private readonly GraphData graph;
        private readonly EventBus bus;
        private readonly string edgeId;
        private readonly int index;
        private Vector2 from;
        private Vector2 to;

        public MoveWaypointCmd(GraphData graph, EventBus bus, string edgeId,
                               int index, Vector2 from, Vector2 to)
        {
            this.graph = graph; this.bus = bus; this.edgeId = edgeId;
            this.index = index; this.from = from; this.to = to;
        }

        public void Do()
        {
            var e = graph.FindEdge(edgeId);
            if (e == null || index < 0 || index >= e.Waypoints.Count) return;
            e.Waypoints[index].Position = to;
            bus.Publish(new WaypointMovedEvent(graph.Id, edgeId, index, from, to));
        }

        public void Undo()
        {
            var e = graph.FindEdge(edgeId);
            if (e == null || index < 0 || index >= e.Waypoints.Count) return;
            e.Waypoints[index].Position = from;
            bus.Publish(new WaypointMovedEvent(graph.Id, edgeId, index, to, from));
        }

        public bool TryMerge(ICommand previous)
        {
            if (previous is MoveWaypointCmd prev &&
                prev.edgeId == edgeId &&
                prev.index == index &&
                ReferenceEquals(prev.graph, graph))
            {
                from = prev.from;
                return true;
            }
            return false;
        }
    }
}