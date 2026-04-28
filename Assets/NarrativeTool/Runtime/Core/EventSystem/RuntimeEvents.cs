using NarrativeTool.Core.Runtime;
using NarrativeTool.Core.Runtime.Executors;
using System.Collections.Generic;

namespace NarrativeTool.Core.EventSystem
{

    public readonly struct NodeEnteredEvent
    {
        public readonly string GraphId;
        public readonly string NodeId;
        public NodeEnteredEvent(string graphId, string nodeId)
        {
            GraphId = graphId;
            NodeId = nodeId;
        }
        public override string ToString() => $"{{ NodeEntered: Graph={GraphId}, Node={NodeId} }}";
    }

    public readonly struct RuntimeStateChanged
    {
        public readonly RuntimeState NewState;
        public RuntimeStateChanged(RuntimeState newState) => NewState = newState;
    }

    public readonly struct ContinueRequestedEvent
    {
        public readonly string NodeId;
        public readonly string Message;
        public ContinueRequestedEvent(string nodeId, string message = null)
        {
            NodeId = nodeId;
            Message = message;
        }
    }

    /// <summary>
    /// Published when a dialogue line (or choice preamble) is displayed.
    /// </summary>
    public readonly struct DialogueLineEvent
    {
        public readonly string NodeId;
        public readonly string Speaker;
        public readonly string Line;
        /// <summary>Optional stage directions shown above the dialogue text.</summary>
        public readonly string StageDirections;

        public DialogueLineEvent(string nodeId, string speaker, string line, string stageDirections = null)
        {
            NodeId = nodeId;
            Speaker = speaker;
            Line = line;
            StageDirections = stageDirections;
        }
    }

    /// <summary>
    /// Published when a choice is presented. The options list contains <see cref="RuntimeChoiceOption"/>,
    /// which may have disabled items due to failed conditions.
    /// </summary>
    public readonly struct ChoicePresentedEvent
    {
        public readonly string NodeId;
        public readonly IReadOnlyList<RuntimeChoiceOption> Options;
        public readonly bool HasPreamble;
        public readonly string Speaker;
        public readonly string DialogueText;
        public readonly string StageDirections;

        public ChoicePresentedEvent(string nodeId, IReadOnlyList<RuntimeChoiceOption> options,
            bool hasPreamble = false, string speaker = "", string dialogueText = "", string stageDirections = "")
        {
            NodeId = nodeId;
            Options = options;
            HasPreamble = hasPreamble;
            Speaker = speaker;
            DialogueText = dialogueText;
            StageDirections = stageDirections;
        }
    }
}