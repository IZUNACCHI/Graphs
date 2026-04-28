using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Serialization;

namespace NarrativeTool.Data.Project
{
    ///<summary>
    /// A class that holds a graph in a lazy manner. The graph is only deserialized when GetGraph() is called.
    /// This is useful for reducing memory usage and improving performance when dealing with large graphs that may not be needed immediately.
    /// The graph is stored as a raw string (e.g., JSON) and only deserialized when needed. When the graph is updated, it is re-serialized to keep the raw data in sync.
    /// Note: This class is not thread-safe. If multiple threads access the same instance, synchronization is needed.
    ///</summary>
    public sealed class LazyGraph : IFolderableItem, INamedItem
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public string FolderPath { get; set; } = ""; // "" = root; "/"-delimited for nested folders
        public string RawData { get; set; }   // serialised graph (JSON, binary, etc.)

        public int CachedNodeCount { get; set; } // number of nodes in the cached graph; 0 if not cached
        public int CachedEdgeCount { get; set; } // number of edges in the cached graph; 0 if not cached

        private GraphData cachedGraph;

        public GraphData GetGraph()
        {
            if (cachedGraph == null && !string.IsNullOrEmpty(RawData))
            {
                cachedGraph = SerializerRegistry.Current.DeserializeGraph(RawData);
                // Initialise cache from the loaded graph
                CachedNodeCount = cachedGraph.Nodes.Count;
                CachedEdgeCount = cachedGraph.Edges.Count;
            }
            return cachedGraph;
        }

        public void Update(GraphData graph)
        {
            cachedGraph = graph;
            RawData = SerializerRegistry.Current.SerializeGraph(graph);
            // Update the cache immediately
            CachedNodeCount = graph.Nodes.Count;
            CachedEdgeCount = graph.Edges.Count;
        }

    }
}