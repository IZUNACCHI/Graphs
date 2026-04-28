using NarrativeTool.Core.Attributes;
using UnityEngine;

namespace NarrativeTool.Data.Graph.Nodes
{
    [NodeType("nt.jumperOut", "Jumper Out", Category = "Flow", Color = "#c07030",
        Description = "Exit teleporter. Receives execution from one or more In jumpers.")]
    public sealed class JumperOutNodeData : NodeData
    {
        public const string OutputPortId = "out";

        public JumperOutNodeData() : base()
        {

        }

        public JumperOutNodeData(string id, string title, Vector2 position)
            : base(id, title, NodeCategory.Flow, position)
        {
            Outputs.Add(new PortData(OutputPortId, "", PortDirection.Output, PortCapacity.Multi, "flow"));
        }
    }
}