// ===== File: Assets/NarrativeTool/Runtime/Data/Commands/SetNodeTextCmd.cs =====
using NarrativeTool.Core;
using UnityEngine;

namespace NarrativeTool.Data.Commands
{
    /// <summary>
    /// Set the Text of a TextNode. Mergeable: consecutive edits on the same
    /// node within a short idle window collapse into one undo entry, so a
    /// burst of typing becomes a single undo step rather than one per keystroke.
    /// Pause longer than MergeWindowSeconds and the next edit starts a fresh entry.
    /// </summary>
    public sealed class SetNodeTextCmd : ICommand
    {
        public const float MergeWindowSeconds = 0.1f;

        public string Name => $"Edit text {nodeId}";

        private readonly GraphDocument graph;
        private readonly EventBus bus;
        private readonly string nodeId;
        private string oldText;   // mutable — merge extends the origin
        private string newText;
        private float timestamp;  // Time.unscaledTime when this cmd was created

        public SetNodeTextCmd(GraphDocument graph, EventBus bus, string nodeId,
                              string oldText, string newText)
        {
            this.graph = graph; this.bus = bus; this.nodeId = nodeId;
            this.oldText = oldText ?? ""; this.newText = newText ?? "";
            this.timestamp = Time.unscaledTime;
        }

        public void Do()
        {
            if (graph.FindNode(nodeId) is not TextNode tn) return;
            tn.Text = newText;
            bus.Publish(new NodeTextChangedEvent(graph.Id, nodeId, oldText, newText));
        }

        public void Undo()
        {
            if (graph.FindNode(nodeId) is not TextNode tn) return;
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
                // Keep the oldest origin so undo reverts all the way back to
                // the state before the typing burst began.
                oldText = prev.oldText;
                return true;
            }
            return false;
        }
    }
}