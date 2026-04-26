using NarrativeTool.Data.Graph;
using System.Collections.Generic;

namespace NarrativeTool.Data.Project
{
    public sealed class ProjectModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int SchemaVersion { get; set; } = 1;

        public List<GraphData> Graphs { get; } = new();

        public VariableStore Variables { get; } = new();
        public EnumStore Enums { get; } = new();
        public EntityStore Entities { get; } = new();

        // Locales the project supports. en-US is always present and cannot
        // be removed (enforced at the wizard level for now). Settable so
        // JSON deserialization replaces the default-init list cleanly.
        public List<string> Locales { get; set; } = new() { "en-US" };

        public GraphData FindGraph(string id)
        {
            foreach (var g in Graphs) if (g.Id == id) return g;
            return null;
        }
    }
}