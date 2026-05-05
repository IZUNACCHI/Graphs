using NarrativeTool.Core.Attributes;
using UnityEngine;

namespace NarrativeTool.Data.Graph.Nodes
{
    [NodeType("nt.script", "Script", Category = "Logic", Color = "#8b80c8",
        Description = "Runs a Lua script (e.g., setVar, side effects) and continues.")]
    public sealed class ScriptNodeData : NodeData
    {
        public const string InputPortId = "in";
        public const string OutputPortId = "out";

        [EditableProperty(Label = "Script", Multiline = true, Order = 1)]
        public string Script { get; set; } = "";

        [EditableProperty(Label = "Mode", Order = 2)]
        public string ScriptingMode { get; set; } = "text";

        public ScriptNodeData(string id, string title, Vector2 position)
            : base(id, title, NodeCategory.Flow, position)
        {
            Inputs.Add(new PortData(InputPortId, "", PortDirection.Input, PortCapacity.Multi, "flow"));
            Outputs.Add(new PortData(OutputPortId, "", PortDirection.Output, PortCapacity.Single, "flow"));
        }

        public ScriptNodeData() : base() { }
    }
}