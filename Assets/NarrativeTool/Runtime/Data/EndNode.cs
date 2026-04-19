using UnityEngine;

namespace NarrativeTool.Data
{
    public sealed class EndNode : Node
    {
        public const string InputPortId = "in";

        public EndNode(string id, Vector2 position)
            : base(id, "End", NodeCategory.Flow, position)
        {
            Inputs.Add(new Port(InputPortId, "", PortDirection.Input,
                                PortCapacity.Multi, "flow"));
        }
    }
}