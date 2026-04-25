using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Graph;

namespace NarrativeTool.Core.Commands
{
    public sealed class AddEdgeCmd : ICommand
    {
        public string Name => $"Add edge {edge.Id}";

        private readonly GraphData graph;
        private readonly EventBus bus;
        private readonly Edge edge;

        public AddEdgeCmd(GraphData graph, EventBus bus, Edge edge)
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