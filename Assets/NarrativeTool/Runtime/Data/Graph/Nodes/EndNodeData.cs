using UnityEngine;

namespace NarrativeTool.Data.Graph.Nodes
{
    [NodeType("nt.end", "End", Category = "Flow", Color = "#c83838")]
    public sealed class EndNodeData : NodeData
    {
        public const string InputPortId = "in";

        public EndNodeData(string id, Vector2 position)
            : base(id, "End", NodeCategory.Flow, position)
        {
            Inputs.Add(new PortData(InputPortId, "", PortDirection.Input,
                                PortCapacity.Multi, "flow"));
        }

        public EndNodeData(string id, string title, Vector2 position)
            : base(id, "End", NodeCategory.Flow, position) 
        {
            Inputs.Add(new PortData(InputPortId, "", PortDirection.Input,
                                   PortCapacity.Multi, "flow"));
        }
    }
}