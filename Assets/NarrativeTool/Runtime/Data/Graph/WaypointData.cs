using UnityEngine;

namespace NarrativeTool.Data.Graph
{
    /// <summary>
    /// One anchor point on an edge. Owned by Edge.Waypoints. No ID — addressed
    /// by (edge, index) in commands so waypoint data is fully contained in
    /// the edge.
    /// </summary>
    public sealed class WaypointData
    {
        public Vector2 Position;

        public WaypointData(Vector2 position) { Position = position; }
    }
}