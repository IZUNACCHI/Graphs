using UnityEngine;

namespace NarrativeTool.Data
{
    /// <summary>
    /// One anchor point on an edge. Owned by Edge.Waypoints. No ID — addressed
    /// by (edge, index) in commands so waypoint data is fully contained in
    /// the edge.
    /// </summary>
    public sealed class Waypoint
    {
        public Vector2 Position;

        public Waypoint(Vector2 position) { Position = position; }
    }
}