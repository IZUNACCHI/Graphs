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

        public GraphData FindGraph(string id)
        {
            foreach (var g in Graphs) if (g.Id == id) return g;
            return null;
        }
    }
}