using NarrativeTool.Core.Attributes;
using UnityEngine;

namespace NarrativeTool.Data.Graph.Nodes
{
    [NodeType("nt.dialog", "Dialogue", Category = "Narrative", Color = "#4a9a6a", Description = "Node representing a piece of dialogue, with optional stage directions")]
    public sealed class DialogNodeData : NodeData
    {
        public const string InputPortId = "in";
        public const string OutputPortId = "out";

        [EditableProperty(Label = "Speaker", Order = 1)]
        public string Speaker { get; set; } = "";

        [EditableProperty(Label = "Stage Directions", Multiline = true, Order = 2, Placeholder = "Enter stage directions...")]
        public string StageDirections { get; set; } = "";

        [EditableProperty(Label = "Dialogue", Multiline = true, Order = 3)]
        public string Dialogue { get; set; } = "";

        public DialogNodeData(string id, string title, Vector2 position)
            : base(id, title, NodeCategory.Data, position)
        {
            Inputs.Add(new PortData(InputPortId, "", PortDirection.Input, PortCapacity.Multi, "flow"));
            Outputs.Add(new PortData(OutputPortId, "", PortDirection.Output, PortCapacity.Single, "flow"));
        }

        public DialogNodeData() : base() { }
    }
}