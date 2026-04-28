using NarrativeTool.Core.Attributes;
using UnityEngine;

namespace NarrativeTool.Data.Graph.Nodes
{
    [NodeType("nt.jumperIn", "Jumper In", Category = "Flow", Color = "#c07030",
        Description = "Entry teleporter. Pairs with an Out jumper.")]
    public sealed class JumperInNodeData : NodeData
    {
        public const string InputPortId = "in";

        /// <summary>The ID of the <see cref="JumperOutNodeData"/> this jumper leads to.</summary>

        public string TargetOutNodeId { get; set; } = "";

        public JumperInNodeData() : base()
        {
        }

        public JumperInNodeData(string id, string title, Vector2 position)
            : base(id, title, NodeCategory.Flow, position)
        {
            Inputs.Add(new PortData(InputPortId, "", PortDirection.Input, PortCapacity.Multi, "flow"));
        }
    }
}