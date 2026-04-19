// ===== File: Assets/NarrativeTool/Runtime/Data/TextNode.cs =====
using UnityEngine;

namespace NarrativeTool.Data
{
    /// <summary>
    /// A flow node that carries a string payload. Stand-in for the eventual
    /// dialogue node. One flow input, one flow output, and a Text field.
    /// </summary>
    public sealed class TextNode : Node
    {
        public const string InputPortId = "in";
        public const string OutputPortId = "out";

        /// <summary>
        /// The editable text displayed on the node. Mutate via SetNodeTextCmd
        /// so undo/redo works correctly.
        /// </summary>
        public string Text { get; set; } = "";

        public TextNode(string id, string title, Vector2 position, string text = "")
            : base(id, title, NodeCategory.Data, position)
        {
            Text = text ?? "";
            Inputs.Add(new Port(InputPortId, "", PortDirection.Input,
                                 PortCapacity.Multi, "flow"));
            Outputs.Add(new Port(OutputPortId, "", PortDirection.Output,
                                 PortCapacity.Single, "flow"));
        }
    }
}