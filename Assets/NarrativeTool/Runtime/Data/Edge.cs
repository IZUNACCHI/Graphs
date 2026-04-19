namespace NarrativeTool.Data
{
    /// <summary>
    /// A connection from one port to another. Edges are directional: From is an
    /// output port, To is an input port.
    ///
    /// Anchors are (NodeId, PortId) pairs rather than direct Port references so
    /// the graph remains easy to serialise as JSON (just strings, no cycles).
    /// </summary>
    public sealed class Edge
    {
        public string Id { get; }
        public string FromNodeId { get; }
        public string FromPortId { get; }
        public string ToNodeId { get; }
        public string ToPortId { get; }

        public Edge(string id, string fromNodeId, string fromPortId,
                               string toNodeId, string toPortId)
        {
            Id = id;
            FromNodeId = fromNodeId; FromPortId = fromPortId;
            ToNodeId = toNodeId; ToPortId = toPortId;
        }
    }
}