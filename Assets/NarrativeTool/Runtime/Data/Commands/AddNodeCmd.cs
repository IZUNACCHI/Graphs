using NarrativeTool.Core;

namespace NarrativeTool.Data.Commands
{
    public sealed class AddNodeCmd : ICommand
    {
        public string Name => $"Add node {node.Id}";

        private readonly GraphDocument graph;
        private readonly EventBus bus;
        private readonly Node node;

        public AddNodeCmd(GraphDocument graph, EventBus bus, Node node)
        {
            this.graph = graph; this.bus = bus; this.node = node;
        }

        public void Do()
        {
            if (graph.FindNode(node.Id) != null) return;
            graph.Nodes.Add(node);
            bus.Publish(new NodeAddedEvent(graph.Id, node.Id));
        }

        public void Undo()
        {
            if (graph.FindNode(node.Id) == null) return;
            graph.Nodes.Remove(node);
            bus.Publish(new NodeRemovedEvent(graph.Id, node.Id));
        }

        public bool TryMerge(ICommand previous) => false;
    }
}