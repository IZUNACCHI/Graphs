using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using UnityEngine;

namespace NarrativeTool.Core.Commands
{
    /// <summary>
    /// Set the Text of a TextNode. Mergeable — consecutive edits on the same
    /// node within a 500ms idle window collapse into one undo entry.
    ///
    /// TODO: revisit once Unity's runtime TextField gains native undo.
    /// </summary>
    public sealed class SetNodeTextCmd : ICommand
    {
        public const float MergeWindowSeconds = 0.5f;

        public string Name => $"Edit text {nodeId}";

        private readonly GraphData graph;
        private readonly EventBus bus;
        private readonly string nodeId;
        private string oldText;
        private string newText;
        private float timestamp;

        public SetNodeTextCmd(GraphData graph, EventBus bus, string nodeId,
                              string oldText, string newText)
        {
            this.graph = graph; this.bus = bus; this.nodeId = nodeId;
            this.oldText = oldText ?? ""; this.newText = newText ?? "";
            this.timestamp = Time.unscaledTime;
        }

        public void Do()
        {
            if (graph.FindNode(nodeId) is not TextNodeData tn) return;
            tn.Text = newText;
            bus.Publish(new NodeTextChangedEvent(graph.Id, nodeId, oldText, newText));
        }

        public void Undo()
        {
            if (graph.FindNode(nodeId) is not TextNodeData tn) return;
            tn.Text = oldText;
            bus.Publish(new NodeTextChangedEvent(graph.Id, nodeId, newText, oldText));
        }

        public bool TryMerge(ICommand previous)
        {
            if (previous is SetNodeTextCmd prev &&
                prev.nodeId == nodeId &&
                ReferenceEquals(prev.graph, graph) &&
                (timestamp - prev.timestamp) <= MergeWindowSeconds)
            {
                oldText = prev.oldText;
                return true;
            }
            return false;
        }
    }
}