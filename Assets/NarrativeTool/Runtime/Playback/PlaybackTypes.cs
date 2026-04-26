using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Project;
using System.Collections.Generic;

namespace NarrativeTool.Playback
{
    /// <summary>
    /// Per-run mutable state. Holds the node we're currently parked on plus
    /// the live variable values (initialised from project defaults at Start).
    /// </summary>
    public sealed class PlaybackState
    {
        // Node id of the current step. Null = not running / finished.
        public string CurrentNodeId { get; set; }

        // Live values, keyed by VariableDefinition.Id. Mutated by future
        // Set/Modify nodes; read by Condition expressions and the variable
        // inspector. Boxed since types vary.
        public Dictionary<string, object> Variables { get; } = new();

        public void ResetVariablesFromDefaults(ProjectModel project)
        {
            Variables.Clear();
            if (project == null) return;
            foreach (var v in project.Variables.Variables)
                Variables[v.Id] = v.DefaultValue;
        }
    }

    /// <summary>
    /// Per-step environment passed to node runtimes. Read-only references to
    /// the project + graph plus the mutable state. Future scripting needs a
    /// host to evaluate expressions against; that hook will live here.
    /// </summary>
    public sealed class PlaybackContext
    {
        public ProjectModel Project { get; }
        public GraphData Graph { get; }
        public PlaybackState State { get; }

        // TODO scripting: an expression evaluator (or interface to one) will
        // be added here so Condition/Set/Choice-condition nodes can read the
        // user's script bodies. Until then, callers should treat conditions
        // as a no-op (defaults documented per-handler).
        public PlaybackContext(ProjectModel project, GraphData graph, PlaybackState state)
        {
            Project = project; Graph = graph; State = state;
        }
    }

    /// <summary>
    /// One option offered to the player while paused at a Choice node.
    /// </summary>
    public sealed class PlaybackChoice
    {
        public string Label { get; }
        public string PortId { get; }
        public bool Enabled { get; }
        public PlaybackChoice(string label, string portId, bool enabled = true)
        { Label = label; PortId = portId; Enabled = enabled; }
    }

    public enum NodeStepKind
    {
        /// <summary>Engine should follow the named output port to the next node.</summary>
        Continue,
        /// <summary>Engine should pause; caller picks one of the surfaced choices.</summary>
        AwaitChoice,
        /// <summary>Run is complete; engine fires Finished and stops.</summary>
        End,
    }

    /// <summary>
    /// Result returned by a node runtime when stepped.
    /// </summary>
    public readonly struct NodeStepResult
    {
        public NodeStepKind Kind { get; }
        public string PortId { get; }
        public IReadOnlyList<PlaybackChoice> Choices { get; }

        private NodeStepResult(NodeStepKind kind, string portId, IReadOnlyList<PlaybackChoice> choices)
        { Kind = kind; PortId = portId; Choices = choices; }

        public static NodeStepResult Continue(string portId) => new(NodeStepKind.Continue, portId, null);
        public static NodeStepResult AwaitChoice(IReadOnlyList<PlaybackChoice> choices) => new(NodeStepKind.AwaitChoice, null, choices);
        public static NodeStepResult End() => new(NodeStepKind.End, null, null);
    }

    /// <summary>
    /// Per-node-type runtime behaviour. One impl per concrete NodeData
    /// subclass; registered with <see cref="PlaybackRegistry"/>.
    /// </summary>
    public interface INodeRuntime
    {
        NodeStepResult Step(NodeData node, PlaybackContext ctx);
    }
}
