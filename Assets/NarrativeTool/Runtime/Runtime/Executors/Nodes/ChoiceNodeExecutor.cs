using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NarrativeTool.Core.Runtime.Executors
{
    [NodeExecutorOf(typeof(ChoiceNodeData))]
    public class ChoiceNodeExecutor : INodeExecutor
    {
        public ExecutionResult Execute(NodeData node, RuntimeContext context)
        {
            var choice = (ChoiceNodeData)node;

            // ── Evaluate conditions and build visible/disabled options ──
            var runtimeOptions = new List<RuntimeChoiceOption>();
            foreach (var opt in choice.Options)
            {
                // Evaluate the Lua script only when the condition toggle is ON and the script is non‑empty
                bool conditionPassed = true;
                if (opt.ConditionEnabled && !string.IsNullOrEmpty(opt.ConditionScript))
                    conditionPassed = context.EvaluateCondition(opt.ConditionScript);

                if (!conditionPassed && opt.HideWhenConditionFalse)
                    continue;   // hidden completely

                runtimeOptions.Add(new RuntimeChoiceOption(opt, conditionPassed));
            }

            if (runtimeOptions.Count == 0)
            {
                var first = choice.Options.FirstOrDefault();
                if (first != null)
                {
                    Debug.Log($"[ChoiceExecutor] All options hidden for node {choice.Id}. Falling through to first option.");
                    return ExecutionResult.Continue(first.PortId);
                }
                return new ExecutionResult(); // end of graph
            }

            // Create interaction request
            var interaction = new ChoiceInteraction(runtimeOptions,
                choice.HasPreamble, choice.Speaker, choice.DialogueText, choice.StageDirections);
            return ExecutionResult.Pause(interaction);
        }
    }


    /// <summary>
    /// Runtime wrapper for a <see cref="ChoiceOption"/> that adds an Enabled flag.
    /// </summary>
    public sealed class RuntimeChoiceOption
    {
        public ChoiceOption Option { get; }
        public bool Enabled { get; }

        public string Label => Option.Label;

        public RuntimeChoiceOption(ChoiceOption option, bool enabled)
        {
            Option = option;
            Enabled = enabled;
        }
    }
}

