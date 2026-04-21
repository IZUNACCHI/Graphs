using NarrativeTool.Core;
using UnityEngine;

namespace NarrativeTool.Data.Commands
{
    public sealed class RemoveWaypointCmd : ICommand
    {
        public string Name => $"Remove waypoint {index} from {edgeId}";

        private readonly GraphDocument graph;
        private readonly EventBus bus;
        private readonly string edgeId;
        private readonly int index;

        // Captured on Do so Undo restores the exact position.
        private Vector2 capturedPosition;
        private bool captured;

        public RemoveWaypointCmd(GraphDocument graph, EventBus bus, string edgeId, int index)
        {
            this.graph = graph; this.bus = bus; this.edgeId = edgeId; this.index = index;
        }

        public void Do()
        {
            var e = graph.FindEdge(edgeId);
            if (e == null) return;
            if (index < 0 || index >= e.Waypoints.Count) return;

            capturedPosition = e.Waypoints[index].Position;
            captured = true;

            e.Waypoints.RemoveAt(index);
            bus.Publish(new WaypointRemovedEvent(graph.Id, edgeId, index));
        }

        public void Undo()
        {
            if (!captured) return;
            var e = graph.FindEdge(edgeId);
            if (e == null) return;
            int i = Mathf.Clamp(index, 0, e.Waypoints.Count);
            e.Waypoints.Insert(i, new Waypoint(capturedPosition));
            bus.Publish(new WaypointAddedEvent(graph.Id, edgeId, i));
        }

        public bool TryMerge(ICommand previous) => false;
    }
}