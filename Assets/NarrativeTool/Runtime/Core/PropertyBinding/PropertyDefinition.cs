// NarrativeTool.Core/Binding/PropertyMetadata.cs
namespace NarrativeTool.Core.Binding
{
    /// <summary>
    /// Describes a property for UI generation. Works for both:
    ///   - Built-in C# properties (populated by PropertyCollector.CollectFromNode)
    ///   - Dynamic PropertyValue entries (populated by PropertyCollector.CollectFromPropertyValues)
    /// </summary>
    public sealed class PropertyDefinition
    {
        /// <summary>Unique ID — C# property name for built-ins, DefinitionId for dynamic.</summary>
        public string Id { get; set; }

        public string Label { get; set; }
        public string Placeholder { get; set; }
        public string Tooltip { get; set; }
        public PropertyType Type { get; set; }
        public bool Editable { get; set; } = true;
        public bool Multiline { get; set; }
        public float Min { get; set; } = float.MinValue;
        public float Max { get; set; } = float.MaxValue;
        public string[] EnumOptions { get; set; }

        /// <summary>
        /// Serialized default value used by PropertyAccessor.FromPropertyValue
        /// when PropertyValue.SerializedValue is empty.
        /// Leave null/empty for built-in C# properties — not used on that path.
        /// </summary>
        public string Default { get; set; } = "";
    }

    public enum PropertyType
    {
        String,
        Int,
        Float,
        Bool,
        Enum,
    }
}