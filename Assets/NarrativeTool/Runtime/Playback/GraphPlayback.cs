using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Project;
using System;
using System.Collections.Generic;

namespace NarrativeTool.Playback
{
    /// <summary>
    /// Single-graph playback engine. Drives traversal one node at a time;
    /// the caller advances by Step() (auto-flowing nodes) or PickChoice()
    /// (when paused at a Choice). The engine holds no UI assumptions — a
    /// scene UI subscribes to the events to render state.
    ///
    /// Selecting a starting point: the entry point is whatever node id is
    /// passed to Start(). Any node is valid; if null, the engine looks for
    /// a StartNodeData in the graph.
    /// </summary>
    public sealed class GraphPlayback
    {
        private readonly GraphData graph;
        private readonly PlaybackRegistry registry;
        private readonly PlaybackContext ctx;

        public PlaybackState State => ctx.State;
        public NodeData CurrentNode => string.IsNullOrEmpty(State.CurrentNodeId)
            ? null : graph.FindNode(State.CurrentNodeId);
        public bool IsRunning => !string.IsNullOrEmpty(State.CurrentNodeId);
        public bool IsAwaitingChoice { get; private set; }
        public IReadOnlyList<PlaybackChoice> PendingChoices { get; private set; }

        public event Action<NodeData> OnEnter;
        public event Action<NodeData> OnExit;
        public event Action OnAwaitingChoice;
        public event Action OnFinished;

        public GraphPlayback(ProjectModel project, GraphData graph, PlaybackRegistry registry)
        {
            this.graph = graph; this.registry = registry;
            this.ctx = new PlaybackContext(project, graph, new PlaybackState());
        }

        /// <summary>
        /// Begin a run. <paramref name="startNodeId"/> may be any node in the
        /// graph; null falls back to the first <see cref="StartNodeData"/>.
        /// Variable values are reset to project defaults on each Start.
        /// </summary>
        public void Start(string startNodeId = null)
        {
            State.ResetVariablesFromDefaults(ctx.Project);
            IsAwaitingChoice = false;
            PendingChoices = null;

            if (string.IsNullOrEmpty(startNodeId))
                startNodeId = AutoFindStart()?.Id;

            if (string.IsNullOrEmpty(startNodeId))
            {
                Finish();
                return;
            }
            EnterNode(startNodeId);
        }

        /// <summary>
        /// Advance one step from the current node. No-op if not running or
        /// currently paused on a choice.
        /// </summary>
        public void Step()
        {
            if (!IsRunning || IsAwaitingChoice) return;
            var node = CurrentNode;
            if (node == null) { Finish(); return; }

            var handler = registry?.For(node);
            NodeStepResult result;
            if (handler != null)
            {
                result = handler.Step(node, ctx);
            }
            else
            {
                // Default behaviour for unknown / un-registered node types:
                // flow through the first output port if any, otherwise End.
                result = node.Outputs.Count > 0
                    ? NodeStepResult.Continue(node.Outputs[0].Id)
                    : NodeStepResult.End();
            }
            Apply(result, node);
        }

        public void PickChoice(string portId)
        {
            if (!IsAwaitingChoice) return;
            var node = CurrentNode; if (node == null) return;
            IsAwaitingChoice = false;
            PendingChoices = null;
            FollowOutput(node, portId);
        }

        public void Stop() => Finish();

        // ── internals ──

        private void Apply(NodeStepResult result, NodeData node)
        {
            switch (result.Kind)
            {
                case NodeStepKind.Continue:
                    FollowOutput(node, result.PortId);
                    break;
                case NodeStepKind.AwaitChoice:
                    IsAwaitingChoice = true;
                    PendingChoices = result.Choices ?? Array.Empty<PlaybackChoice>();
                    OnAwaitingChoice?.Invoke();
                    break;
                case NodeStepKind.End:
                    Finish();
                    break;
            }
        }

        private void FollowOutput(NodeData node, string portId)
        {
            OnExit?.Invoke(node);
            Edge edge = null;
            foreach (var e in graph.Edges)
                if (e.FromNodeId == node.Id && e.FromPortId == portId) { edge = e; break; }
            if (edge == null) { Finish(); return; }
            EnterNode(edge.ToNodeId);
        }

        private void EnterNode(string nodeId)
        {
            State.CurrentNodeId = nodeId;
            var node = CurrentNode;
            if (node == null) { Finish(); return; }
            OnEnter?.Invoke(node);
        }

        private void Finish()
        {
            var prev = CurrentNode;
            State.CurrentNodeId = null;
            IsAwaitingChoice = false;
            PendingChoices = null;
            if (prev != null) OnExit?.Invoke(prev);
            OnFinished?.Invoke();
        }

        private NodeData AutoFindStart()
        {
            // Cheap: first StartNodeData in node order. Future: look up via
            // a project-level "default start" config.
            foreach (var n in graph.Nodes)
                if (n.GetType().Name == "StartNodeData") return n;
            return null;
        }
    }
}
