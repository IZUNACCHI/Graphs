using NarrativeTool.Core;

namespace NarrativeTool.Data.Commands
{
    public sealed class AddEdgeCmd : ICommand
    {
        public string Name => $"Add edge {edge.Id}";

        private readonly GraphDocument graph;
        private readonly EventBus bus;
        private readonly Edge edge;

        public AddEdgeCmd(GraphDocument graph, EventBus bus, Edge edge)
        {
            this.graph = graph; this.bus = bus; this.edge = edge;
        }

        public void Do()
        {
            if (graph.FindEdge(edge.Id) != null) return;
            graph.Edges.Add(edge);
            bus.Publish(new EdgeAddedEvent(graph.Id, edge.Id));
        }

        public void Undo()
        {
            if (graph.FindEdge(edge.Id) == null) return;
            graph.Edges.Remove(edge);
            bus.Publish(new EdgeRemovedEvent(graph.Id, edge.Id));
        }

        public bool TryMerge(ICommand previous) => false;
    }
}