using NarrativeTool.Core;
using UnityEngine;

namespace NarrativeTool.Data
{
    public sealed class AddWaypointCmd : ICommand
    {
        public string Name => $"Add waypoint to {edgeId}";

        private readonly GraphDocument graph;
        private readonly EventBus bus;
        private readonly string edgeId;
        private readonly int index;
        private readonly Vector2 position;

        public AddWaypointCmd(GraphDocument graph, EventBus bus, string edgeId,
                              int index, Vector2 position)
        {
            this.graph = graph; this.bus = bus; this.edgeId = edgeId;
            this.index = index; this.position = position;
        }

        public void Do()
        {
            var e = graph.FindEdge(edgeId);
            if (e == null) return;
            int i = Mathf.Clamp(index, 0, e.Waypoints.Count);
            e.Waypoints.Insert(i, new Waypoint(position));
            bus.Publish(new WaypointAddedEvent(graph.Id, edgeId, i));
        }

        public void Undo()
        {
            var e = graph.FindEdge(edgeId);
            if (e == null) return;
            int i = Mathf.Clamp(index, 0, e.Waypoints.Count - 1);
            if (i < 0 || i >= e.Waypoints.Count) return;
            e.Waypoints.RemoveAt(i);
            bus.Publish(new WaypointRemovedEvent(graph.Id, edgeId, i));
        }

        public bool TryMerge(ICommand previous) => false;
    }
}