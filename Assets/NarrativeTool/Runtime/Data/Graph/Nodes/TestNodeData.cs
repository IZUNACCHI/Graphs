using NarrativeTool.Core.Attributes;
using UnityEngine;

namespace NarrativeTool.Data.Graph.Nodes
{
    public sealed class TestNodeData : NodeData
    {

        public const string InputPortId = "in";
        public const string OutputPortId = "out";
        // Simple string with placeholder
        [EditableProperty(Label = "Character Name", Order = 1, Placeholder = "Enter name...")]
        public string CharacterName { get; set; } = "";

        // Multiline string
        [EditableProperty(Label = "Description", Multiline = true, Order = 2)]
        public string Description { get; set; } = "";

        // Integer field
        [EditableProperty(Label = "Age", Order = 3, Min = 0, Max = 999)]
        public int Age { get; set; } = 25;

        // Float field
        [EditableProperty(Label = "Confidence", Order = 4, Min = 0f, Max = 1f)]
        public float Confidence { get; set; } = 0.5f;

        // Boolean field
        [EditableProperty(Label = "Is Alive", Order = 5)]
        public bool IsAlive { get; set; } = true;

        // Enum field (you need a simple enum somewhere)
        [EditableProperty(Label = "Mood", Order = 6)]
        public Mood Mood { get; set; } = Mood.Neutral;

        // Read-only field (greyed out)
        [EditableProperty(Label = "Lucky Number", Order = 7, Editable = false)]
        public int LuckyNumber { get; set; } = 42;

        // Simple string without placeholder
        [EditableProperty(Label = "Note", Order = 8)]
        public string Note { get; set; } = "Some text";

        public TestNodeData(string id, string title, Vector2 position)
            : base(id, title, NodeCategory.Data, position)
        {
            // No input or output ports for simplicity, but you can add optional ones
            Inputs.Add(new PortData("in", "", PortDirection.Input, PortCapacity.Multi, "flow"));
            Outputs.Add(new PortData("out", "", PortDirection.Output, PortCapacity.Single, "flow"));
        }
    }

    public enum Mood
    {
        Happy,
        Sad,
        Angry,
        Neutral,
        Thoughtful
    }
}