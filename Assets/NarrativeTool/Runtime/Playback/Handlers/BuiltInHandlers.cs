using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using System.Collections.Generic;

namespace NarrativeTool.Playback.Handlers
{
    /// <summary>
    /// Default runtime behaviour for built-in node types. Registered in
    /// bootstrap. New node types should add their own INodeRuntime impl
    /// rather than extending the engine.
    /// </summary>
    public sealed class StartNodeRuntime : INodeRuntime
    {
        public NodeStepResult Step(NodeData node, PlaybackContext ctx)
            => NodeStepResult.Continue(StartNodeData.OutputPortId);
    }

    public sealed class EndNodeRuntime : INodeRuntime
    {
        public NodeStepResult Step(NodeData node, PlaybackContext ctx)
            => NodeStepResult.End();
    }

    /// <summary>
    /// Text node — currently just flows through. The UI overlay reads the
    /// text from the node and renders it; the engine doesn't pause for
    /// "Continue" prompts (caller drives Step()).
    /// </summary>
    public sealed class TextNodeRuntime : INodeRuntime
    {
        public NodeStepResult Step(NodeData node, PlaybackContext ctx)
            => NodeStepResult.Continue(TextNodeData.OutputPortId);
    }

    public sealed class DialogNodeRuntime : INodeRuntime
    {
        public NodeStepResult Step(NodeData node, PlaybackContext ctx)
            => NodeStepResult.Continue(DialogNodeData.OutputPortId);
    }

    public sealed class TestNodeRuntime : INodeRuntime
    {
        public NodeStepResult Step(NodeData node, PlaybackContext ctx)
            => NodeStepResult.Continue(TestNodeData.OutputPortId);
    }

    /// <summary>
    /// Choice node — pauses the engine and surfaces the option labels +
    /// their port ids. The caller picks via <see cref="GraphPlayback.PickChoice"/>.
    ///
    /// TODO scripting: per-option ConditionEnabled/ConditionScript currently
    /// have no evaluator, so all options surface as enabled. When the
    /// expression host lands (PlaybackContext), gate or hide options whose
    /// condition evaluates to false (HideWhenConditionFalse drives which).
    /// </summary>
    public sealed class ChoiceNodeRuntime : INodeRuntime
    {
        public NodeStepResult Step(NodeData node, PlaybackContext ctx)
        {
            var choice = (ChoiceNodeData)node;
            var list = new List<PlaybackChoice>(choice.Options.Count);
            foreach (var opt in choice.Options)
                list.Add(new PlaybackChoice(opt.Label, opt.PortId, enabled: true));
            return NodeStepResult.AwaitChoice(list);
        }
    }

    /// <summary>
    /// Condition node — flow router based on a script expression. Without a
    /// scripting host, this defaults to True so the run keeps moving (and
    /// the user can at least exercise the True branch end-to-end).
    /// </summary>
    public sealed class ConditionNodeRuntime : INodeRuntime
    {
        public NodeStepResult Step(NodeData node, PlaybackContext ctx)
        {
            // TODO scripting: evaluate ((ConditionNodeData)node).ConditionScript
            // against ctx.State.Variables and route accordingly.
            return NodeStepResult.Continue(ConditionNodeData.TruePortId);
        }
    }
}
