using System.Collections.Generic;

namespace NarrativeTool.Data.Graph
{
    /// <summary>
    /// A connection from one port to another. Directional: From is an output,
    /// To is an input. Now carries optional label, routing mode, and waypoints.
    /// </summary>
    public sealed class Edge
    {
        public string Id { get; }
        public string FromNodeId { get; }
        public string FromPortId { get; }
        public string ToNodeId { get; }
        public string ToPortId { get; }

        public string Label { get; set; } = "";
        public EdgeRoutingMode RoutingMode { get; set; } = EdgeRoutingMode.Bezier;
        public List<WaypointData> Waypoints { get; } = new();

        public Edge(string id, string fromNodeId, string fromPortId,
                               string toNodeId, string toPortId)
        {
            Id = id;
            FromNodeId = fromNodeId; FromPortId = fromPortId;
            ToNodeId = toNodeId; ToPortId = toPortId;
        }
    }
}