using System.Collections.Generic;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Core.Runtime.Executors;

namespace NarrativeTool.Core.Runtime
{
    /// <summary>
    /// Base class for all interaction types. Subclasses implement <see cref="Configure"/>
    /// to set up the <see cref="InteractionContext"/> and publish appropriate events.
    /// </summary>
    public abstract class InteractionRequest
    {
        /// <summary>
        /// Called by the engine to prepare the context for pausing and to inform the UI.
        /// </summary>
        public abstract void Configure(InteractionContext ctx, EventBus bus, string nodeId);
    }

    /// <summary>
    /// Standard choice interaction (one or more options, some possibly disabled).
    /// </summary>
    public class ChoiceInteraction : InteractionRequest
    {
        public IReadOnlyList<RuntimeChoiceOption> Options { get; }
        public bool HasPreamble { get; }
        public string Speaker { get; }
        public string DialogueText { get; }
        public string StageDirections { get; }

        public ChoiceInteraction(IReadOnlyList<RuntimeChoiceOption> options,
            bool hasPreamble = false, string speaker = "", string dialogueText = "", string stageDirections = "")
        {
            Options = options;
            HasPreamble = hasPreamble;
            Speaker = speaker;
            DialogueText = dialogueText;
            StageDirections = stageDirections;
        }

        public override void Configure(InteractionContext ctx, EventBus bus, string nodeId)
        {
            // Set up the interaction context so it can be resolved later
            ctx.RequestChoice(Options, selectedIndex =>
            {
                var chosen = Options[selectedIndex];
                ctx.UserData = chosen.Option.PortId;
            });
            // Publish the event for the UI
            bus.Publish(new ChoicePresentedEvent(nodeId, Options, HasPreamble, Speaker, DialogueText, StageDirections));
        }
    }

    /// <summary>
    /// Simple "click to continue" interaction (dialogues, etc.).
    /// </summary>
    public class ContinueInteraction : InteractionRequest
    {
        public string Message { get; }
        public string NextPortId { get; }

        public ContinueInteraction(string message, string nextPortId)
        {
            Message = message;
            NextPortId = nextPortId;
        }

        public override void Configure(InteractionContext ctx, EventBus bus, string nodeId)
        {
            ctx.RequestContinue(Message, () => ctx.UserData = NextPortId);
            bus.Publish(new ContinueRequestedEvent(nodeId, Message));
        }
    }
}