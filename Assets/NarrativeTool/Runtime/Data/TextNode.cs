using UnityEngine;

namespace NarrativeTool.Data
{
    /// <summary>
    /// A flow node that carries a string payload. One flow input, one flow
    /// output. Mutate Text via SetNodeTextCmd so undo works.
    /// </summary>
    public sealed class TextNode : Node
    {
        public const string InputPortId = "in";
        public const string OutputPortId = "out";

        public string Text { get; set; } = "";

        public TextNode(string id, string title, Vector2 position, string text = "")
            : base(id, title, NodeCategory.Data, position)
        {
            Text = text ?? "";
            Inputs.Add(new Port(InputPortId, "►", PortDirection.Input,
                                 PortCapacity.Multi, "flow"));
            Outputs.Add(new Port(OutputPortId, "►", PortDirection.Output,
                                 PortCapacity.Single, "flow"));
        }
    }
}