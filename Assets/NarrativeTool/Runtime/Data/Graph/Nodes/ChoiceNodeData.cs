// NarrativeTool.Data.Graph.Nodes/ChoiceNodeData.cs
using System.Collections.Generic;
using UnityEngine;
using NarrativeTool.Core.Attributes;
using NarrativeTool.Data.Graph;

namespace NarrativeTool.Data.Graph.Nodes
{
    [NodeType("nt.choice", "Choice", Category = "Narrative", Color = "#c0a040",
    Description = "Player choice with one or more options.")]
    public sealed class ChoiceNodeData : NodeData
    {
        public const string InputPortId = "in";

        // Preamble fields (optional dialogue before choices) 
        [EditableProperty(Label = "Speaker", Order = 1)]
        public string Speaker { get; set; } = "";

        [EditableProperty(Label = "Stage Directions", Multiline = true, Order = 2)]
        public string StageDirections { get; set; } = "";

        [EditableProperty(Label = "Dialogue", Multiline = true, Order = 3)]
        public string DialogueText { get; set; } = "";

        public bool HasPreamble { get; set; } = true;   // toggled by context menu

        //  Options 
        public List<ChoiceOption> Options { get; } = new();

        public ChoiceNodeData(string id, string title, Vector2 position)
            : base(id, title, NodeCategory.Flow, position)
        {
            Inputs.Add(new PortData(InputPortId, "", PortDirection.Input, PortCapacity.Multi, "flow"));
            // Outputs are added dynamically with each option
        }


        public ChoiceOption AddOption(string text = "")
        {
            var option = new ChoiceOption
            {
                Id = $"opt_{System.Guid.NewGuid():N}".Substring(0, 8),
                Label = text,
                PortId = $"opt_{Options.Count}"
            };
            Options.Add(option);
            Outputs.Add(new PortData(option.PortId, text, PortDirection.Output, PortCapacity.Single, "flow"));
            return option;
        }

        public void RemoveOption(int index)
        {
            if (index < 0 || index >= Options.Count) return;
            var portId = Options[index].PortId;
            Options.RemoveAt(index);
            Outputs.RemoveAll(p => p.Id == portId);
        }
    }


    public sealed class ChoiceOption
    {
        public string Id { get; set; }
        public string Label { get; set; } = "";
        public string ConditionScript { get; set; } = "";
        public bool HideWhenConditionFalse { get; set; } = true;
        public bool HasCondition => !string.IsNullOrWhiteSpace(ConditionScript);
        public string PortId { get; set; }
    }
}