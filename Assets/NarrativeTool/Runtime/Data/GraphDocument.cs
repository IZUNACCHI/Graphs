using System.Collections.Generic;

namespace NarrativeTool.Data
{
    public sealed class GraphDocument
    {
        public string Id { get; }
        public string Name { get; set; }

        public List<Node> Nodes { get; } = new();
        public List<Edge> Edges { get; } = new();

        public GraphDocument(string id, string name)
        {
            Id = id; Name = name;
        }

        public Node FindNode(string id)
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