// ===== File: Assets/NarrativeTool/Runtime/Data/EndNode.cs =====
using UnityEngine;

namespace NarrativeTool.Data
{
    /// <summary>
    /// Graph exit point. Single flow input, no outputs. Carries no data of its
    /// own. Paired with StartNode for subgraph boundaries later.
    /// </summary>
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