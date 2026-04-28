using NarrativeTool.Core.Attributes;
using UnityEngine;

namespace NarrativeTool.Data.Graph.Nodes
{
    [NodeType("nt.subgraph", "Subgraph", Category = "Flow", Color = "#a050c0",
        Description = "Calls another graph. When the subgraph finishes, execution continues from this node.")]
    public sealed class SubgraphNodeData : NodeData
    {
        public const string InputPortId = "in";
        public const string OutputPortId = "out";

        public string ReferencedGraphId { get; set; } = "";

        public SubgraphNodeData() : base() { }   


        public SubgraphNodeData(string id, string title, Vector2 position)
            : base(id, title, NodeCategory.Flow, position)
        {
            Inputs.Add(new PortData(InputPortId, "", PortDirection.Input, PortCapacity.Multi, "flow"));
            Outputs.Add(new PortData(OutputPortId, "", PortDirection.Output, PortCapacity.Single, "flow"));
        }
    }
}