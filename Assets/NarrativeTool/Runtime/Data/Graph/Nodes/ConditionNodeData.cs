using NarrativeTool.Core.Attributes;
using UnityEngine;

namespace NarrativeTool.Data.Graph.Nodes
{
    [NodeType("nt.condition", "Condition", Category = "Logic", Color = "#b0b0b0",
        Description = "Node that evaluates a condition and routes flow based on the result")]
    public sealed class ConditionNodeData : NodeData
    {
        public const string InputPortId = "in";
        public const string TruePortId = "true";
        public const string FalsePortId = "false";

        // No [EditableProperty] – the view builds the UI manually.
        public string ConditionScript { get; set; } = "";

        // No [EditableProperty] – the view builds a dropdown.
        public string ScriptingMode { get; set; } = "text";

        public ConditionNodeData(string id, string title, Vector2 position)
            : base(id, title, NodeCategory.Flow, position)
        {
            Inputs.Add(new PortData(InputPortId, "", PortDirection.Input, PortCapacity.Multi, "flow"));
            Outputs.Add(new PortData(TruePortId, "True", PortDirection.Output, PortCapacity.Single, "flow"));
            Outputs.Add(new PortData(FalsePortId, "False", PortDirection.Output, PortCapacity.Single, "flow"));
        }

        public ConditionNodeData() : base() { }
    }
}