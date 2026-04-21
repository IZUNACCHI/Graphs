using UnityEngine;

namespace NarrativeTool.Data
{
    public sealed class StartNode : Node
    {
        public const string OutputPortId = "out";

        public StartNode(string id, Vector2 position)
            : base(id, "Start", NodeCategory.Event, position)
        {
            Outputs.Add(new Port(OutputPortId, "►", PortDirection.Output,
                                 PortCapacity.Single, "flow"));
        }
    }
}