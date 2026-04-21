using NarrativeTool.Core;

namespace NarrativeTool.Data.Commands
{
    public sealed class RemoveEdgeCmd : ICommand
    {
        public string Name => $"Remove edge {edgeId}";

        private readonly GraphDocument graph;
        private readonly EventBus bus;
        private readonly string edgeId;
        private Edge removed;

        public RemoveEdgeCmd(GraphDocument graph, EventBus bus, string edgeId)
        {
            this.graph = graph; this.bus = bus; this.edgeId = edgeId;
        }

        public void Do()
        {
            var e = graph.FindEdge(edgeId);
            if (e == null) return;
            graph.Edges.Remove(e);
            removed = e;
            bus.Publish(new EdgeRemovedEvent(graph.Id, edgeId));
        }

        public void Undo()
        {
            if (removed == null) return;
            graph.Edges.Add(removed);
            bus.Publish(new EdgeAddedEvent(graph.Id, removed.Id));
        }

        public bool TryMerge(ICommand previous) => false;
    }
}