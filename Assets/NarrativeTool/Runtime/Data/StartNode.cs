// ===== File: Assets/NarrativeTool/Runtime/Data/StartNode.cs =====
using UnityEngine;

namespace NarrativeTool.Data
{
    /// <summary>
    /// Graph entry point. Single flow output, no inputs. Carries no data of its
    /// own — it exists to mark "execution begins here" and to be the anchor for
    /// subgraph entry later.
    /// </summary>
    public sealed class StartNode : Node
    {
        public const string OutputPortId = "out";

        public StartNode(string id, Vector2 position)
            : base(id, "Start", NodeCategory.Event, position)
        {
            Outputs.Add(new Port(OutputPortId, "", PortDirection.Output,
                                 PortCapacity.Single, "flow"));
        }
    }
}