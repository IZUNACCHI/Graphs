using UnityEngine;

namespace NarrativeTool.Data.Graph.Nodes
{
    [NodeType("nt.start", "Start", Category = "Flow", Color = "#c83838", Description = "Graph entry point")]
    public sealed class StartNodeData : NodeData
    {
        public const string OutputPortId = "out";

        public StartNodeData(string id, Vector2 position)
            : base(id, "Start", NodeCategory.Event, position)
        {
            Outputs.Add(new PortData(OutputPortId, "", PortDirection.Output,
                                 PortCapacity.Single, "flow"));
        }

        public StartNodeData(string id, string title, Vector2 position)
            : base(id, "Start", NodeCategory.Event, position)
        {
            Outputs.Add(new PortData(OutputPortId, "", PortDirection.Output,
                                 PortCapacity.Single, "flow"));
        }

        public StartNodeData() : base() { }
    }
}