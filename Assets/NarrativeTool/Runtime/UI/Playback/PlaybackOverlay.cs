using NarrativeTool.Canvas;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using NarrativeTool.Data.Project;
using NarrativeTool.Playback;
using System;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Playback
{
    /// <summary>
    /// Floating bar at the bottom of the editor view that drives playback:
    /// shows the current node's preview, offers Step / Stop, and replaces
    /// itself with a choice list while paused at a Choice node.
    /// </summary>
    public sealed class PlaybackOverlay : VisualElement
    {
        private readonly ProjectModel project;
        private readonly GraphData graph;
        private readonly PlaybackRegistry registry;
        private readonly GraphView canvas;

        private GraphPlayback playback;

        // UI
        private readonly Label statusLabel;
        private readonly Label previewLabel;
        private readonly VisualElement actionsRow;

        public PlaybackOverlay(ProjectModel project, GraphData graph, PlaybackRegistry registry, GraphView canvas)
        {
            this.project = project; this.graph = graph;
            this.registry = registry; this.canvas = canvas;

            AddToClassList("nt-playback");
            style.display = DisplayStyle.None;   // hidden until Start

            statusLabel = new Label("● Playing");
            statusLabel.AddToClassList("nt-playback-status");
            Add(statusLabel);

            previewLabel = new Label();
            previewLabel.AddToClassList("nt-playback-preview");
            Add(previewLabel);

            actionsRow = new VisualElement();
            actionsRow.AddToClassList("nt-playback-actions");
            Add(actionsRow);
        }

        public void StartAt(string nodeId)
        {
            DetachPlayback();
            playback = new GraphPlayback(project, graph, registry);
            playback.OnEnter += HandleEnter;
            playback.OnExit += HandleExit;
            playback.OnAwaitingChoice += HandleAwaitingChoice;
            playback.OnFinished += HandleFinished;

            style.display = DisplayStyle.Flex;
            playback.Start(nodeId);
        }

        public void Stop()
        {
            playback?.Stop();
            // HandleFinished does the cleanup.
        }

        // ── engine event handlers ──

        private void HandleEnter(NodeData node)
        {
            previewLabel.text = PreviewFor(node);
            canvas?.SetHighlightedNode(node.Id);
            BuildContinueActions();
        }

        private void HandleExit(NodeData node) { /* handled by next Enter or Finished */ }

        private void HandleAwaitingChoice() => BuildChoiceActions();

        private void HandleFinished()
        {
            statusLabel.text = "● Finished";
            previewLabel.text = "";
            actionsRow.Clear();
            var close = new Button(() => { style.display = DisplayStyle.None; DetachPlayback(); })
            { text = "Close" };
            close.AddToClassList("nt-playback-btn");
            actionsRow.Add(close);
            canvas?.SetHighlightedNode(null);
        }

        private void DetachPlayback()
        {
            if (playback == null) return;
            playback.OnEnter -= HandleEnter;
            playback.OnExit -= HandleExit;
            playback.OnAwaitingChoice -= HandleAwaitingChoice;
            playback.OnFinished -= HandleFinished;
            playback = null;
        }

        // ── action builders ──

        private void BuildContinueActions()
        {
            actionsRow.Clear();
            statusLabel.text = "● Playing";

            var step = new Button(() => playback?.Step()) { text = "Continue →" };
            step.AddToClassList("nt-playback-btn");
            step.AddToClassList("nt-playback-btn--primary");
            actionsRow.Add(step);

            var stop = new Button(Stop) { text = "Stop" };
            stop.AddToClassList("nt-playback-btn");
            actionsRow.Add(stop);
        }

        private void BuildChoiceActions()
        {
            actionsRow.Clear();
            statusLabel.text = "● Choose";

            if (playback?.PendingChoices != null)
            {
                foreach (var c in playback.PendingChoices)
                {
                    var portId = c.PortId;   // capture for closure
                    var btn = new Button(() => playback?.PickChoice(portId)) { text = c.Label };
                    btn.AddToClassList("nt-playback-btn");
                    btn.AddToClassList("nt-playback-choice");
                    if (!c.Enabled) btn.SetEnabled(false);
                    actionsRow.Add(btn);
                }
            }

            var stop = new Button(Stop) { text = "Stop" };
            stop.AddToClassList("nt-playback-btn");
            actionsRow.Add(stop);
        }

        // ── display helpers ──

        private static string PreviewFor(NodeData node)
        {
            switch (node)
            {
                case TextNodeData t: return $"\"{t.Text}\"";
                case DialogNodeData d:
                    var who = string.IsNullOrEmpty(d.Speaker) ? "" : d.Speaker + ": ";
                    return who + "\"" + d.Dialogue + "\"";
                case ChoiceNodeData c:
                    return c.HasPreamble && !string.IsNullOrEmpty(c.DialogueText)
                        ? "\"" + c.DialogueText + "\""
                        : "(choice)";
                case StartNodeData _: return "(start)";
                case EndNodeData _: return "(end)";
                default: return node.Title ?? node.GetType().Name;
            }
        }
    }
}
