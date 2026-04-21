using NarrativeTool.Core;

namespace NarrativeTool.Data.Commands
{
    public sealed class SetEdgeLabelCmd : ICommand
    {
        public string Name => $"Set label on {edgeId}";

        private readonly GraphDocument graph;
        private readonly EventBus bus;
        private readonly string edgeId;
        private readonly string oldLabel;
        private readonly string newLabel;

        public SetEdgeLabelCmd(GraphDocument graph, EventBus bus, string edgeId,
                               string oldLabel, string newLabel)
        {
            this.graph = graph; this.bus = bus; this.edgeId = edgeId;
            this.oldLabel = oldLabel ?? ""; this.newLabel = newLabel ?? "";
        }

        public void Do()
        {
            var e = graph.FindEdge(edgeId);
            if (e == null) return;
            e.Label = newLabel;
            bus.Publish(new EdgeLabelChangedEvent(graph.Id, edgeId, oldLabel, newLabel));
        }

        public void Undo()
        {
            var e = graph.FindEdge(edgeId);
            if (e == null) return;
            e.Label = oldLabel;
            bus.Publish(new EdgeLabelChangedEvent(graph.Id, edgeId, newLabel, oldLabel));
        }

        public bool TryMerge(ICommand previous) => false;
    }
}