using System.Collections.Generic;

namespace NarrativeTool.Data.Graph
{
    public sealed class GraphData
    {
        public string Id { get; private set; }
        public string Name { get; set; }

        public List<NodeData> Nodes { get; } = new();
        public List<Edge> Edges { get; } = new();

        public GraphData(string id, string name)
        {
            Id = id; Name = name;
        }

        public GraphData() { }

        public NodeData FindNode(string id)
        {
            foreach (var n in Nodes) if (n.Id == id) return n;
            return null;
        }

        public Edge FindEdge(string id)
        {
            foreach (var e in Edges) if (e.Id == id) return e;
            return null;
        }
    }
}