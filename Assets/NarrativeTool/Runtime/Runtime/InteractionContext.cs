using System;
using System.Collections.Generic;
using NarrativeTool.Core.Runtime.Executors; // for RuntimeChoiceOption
using NarrativeTool.Data.Graph.Nodes;

namespace NarrativeTool.Core.Runtime
{
    public enum InteractionType
    {
        None,
        Choice,
        Continue,
        Custom
    }

    /// <summary>
    /// Describes a pause that requires external input before the engine can resume.
    /// The engine stops when <see cref="IsPending"/> is true, publishes the
    /// appropriate event, and waits for a <see cref="Resolve"/> call.
    /// </summary>
    public sealed class InteractionContext
    {
        public InteractionType Type { get; private set; }

        /// <summary>If Type is Choice, the list of options (some may be disabled).</summary>
        public IReadOnlyList<RuntimeChoiceOption> Options { get; private set; }

        public string Message { get; private set; }

        /// <summary>
        /// Arbitrary payload set by the executor (e.g., the next port ID to follow).
        /// Not cleared on <see cref="Clear"/> because the engine reads it after resolution.
        /// </summary>
        public object UserData { get; set; }

        public bool IsPending => Type != InteractionType.None;

        private Action<int> onResolve;

        /// <summary>
        /// Called by the UI (or test) to resolve the interaction.
        /// <paramref name="resultIndex"/> is the chosen option index for Choice, or 0 for Continue / Custom.
        /// </summary>
        public void Resolve(int resultIndex = 0)
        {
            if (!IsPending) return;
            onResolve?.Invoke(resultIndex);
            Clear();
        }

        /// <summary>Requests a choice pause. Called by the choice executor.</summary>
        public void RequestChoice(IReadOnlyList<RuntimeChoiceOption> options, Action<int> onChoiceMade)
        {
            Type = InteractionType.Choice;
            Options = options;
            onResolve = onChoiceMade;
        }

        /// <summary>Requests a simple “continue” pause. Called by a dialogue executor.</summary>
        public void RequestContinue(string message, Action onContinue)
        {
            Type = InteractionType.Continue;
            Message = message;
            Options = null;
            onResolve = _ => onContinue?.Invoke();
        }

        public void Clear()
        {
            Type = InteractionType.None;
            Options = null;
            Message = null;
            onResolve = null;
            // UserData is intentionally left intact – the engine reads it after resolution.
        }
    }
}