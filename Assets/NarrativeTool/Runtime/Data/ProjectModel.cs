using System.Collections.Generic;

namespace NarrativeTool.Data
{
    /// <summary>
    /// Root of a project. Holds all graphs and project-level metadata. Later
    /// phases add VariableStore, EntityLibrary, SharedLibraries, etc.
    /// </summary>
    public sealed class ProjectModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int SchemaVersion { get; set; } = 1;

        public List<GraphDocument> Graphs { get; } = new();

        public GraphDocument FindGraph(string id)
        {
            foreach (var g in Graphs) if (g.Id == id) return g;
            return null;
        }
    }
}