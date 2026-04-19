namespace NarrativeTool.Data
{
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