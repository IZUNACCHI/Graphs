using NarrativeTool.Data.Graph;

namespace NarrativeTool.Core
{
    /// <summary>Minimal port descriptor for compatibility checks.</summary>
    public sealed class PortDefinition
    {
        public string PortId { get; set; }   // The internal ID (e.g. "in", "out", "true")
        public PortDirection Direction { get; set; }
        public string TypeTag { get; set; }
    }
}