// NarrativeTool.Data.Graph/PropertyValue.cs
namespace NarrativeTool.Data.Graph
{
    /// <summary>
    /// Stores the serialized value of one dynamic property on a node.
    /// DefinitionId references a PropertyDefinition. SerializedValue is
    /// a JSON-encoded string; the actual type is determined by the definition.
    /// </summary>
    public sealed class PropertyInstance
    {
        /// <summary>Links to PropertyDefinition.Id.</summary>
        public string DefinitionId { get; set; }

        /// <summary>JSON-encoded value. Empty string = use definition's default.</summary>
        public string SerializedValue { get; set; } = "";
    }
}