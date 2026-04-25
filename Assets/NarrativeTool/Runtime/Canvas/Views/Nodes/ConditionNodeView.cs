using NarrativeTool.Core.Commands;
using NarrativeTool.Core.Widgets;
using NarrativeTool.Data.Graph.Nodes;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas.Views
{

    public sealed class ConditionNodeView : NodeView
    {
        public ConditionNodeView(ConditionNodeData data, GraphView canvas)
            : base(data, canvas) { }

        protected override void BuildCustomBody()
        {
            var node = (ConditionNodeData)Node;

            // ── Script editor field ──────────────────────────────
            var scriptField = new FlexTextField("Condition", multiline: false)
            {
                value = node.ConditionScript
            };

            // Live update the data while typing (visual feedback only)
            scriptField.RegisterValueChangedCallback(e =>
                node.ConditionScript = e.newValue);

            // On commit  push an undoable command
            scriptField.OnCommit += (oldVal, newVal) =>
            {
                Canvas.Commands.Execute(new SetPropertyCommand(
                    "ConditionScript",
                    v => node.ConditionScript = (string)v,
                    oldVal, newVal, Canvas.Bus));
            };

            extrasContainer.Add(scriptField);

            // ── Scripting mode dropdown ─────────────────────────
            var modeDropdown = new DropdownField(
                "Mode",
                new List<string> { "text", "visual", "block" },
                0);
            modeDropdown.value = node.ScriptingMode;
            modeDropdown.RegisterValueChangedCallback(e =>
                node.ScriptingMode = e.newValue);

            extrasContainer.Add(modeDropdown);
        }
    }
}