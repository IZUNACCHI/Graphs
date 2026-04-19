using System.Collections.Generic;

namespace NarrativeTool.Data
{
    /// <summary>
    /// One graph within a project. Holds the nodes and edges. A project can have
    /// many graphs (subgraphs, separate dialogue trees, etc.).
    /// </summary>
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