using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Graph;

namespace NarrativeTool.Core.Commands
{
    public sealed class AddNodeCmd : ICommand
    {
        public string Name => $"Add node {node.Id}";

        private readonly GraphData graph;
        private readonly EventBus bus;
        private readonly NodeData node;

        public AddNodeCmd(GraphData graph, EventBus bus, NodeData node)
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