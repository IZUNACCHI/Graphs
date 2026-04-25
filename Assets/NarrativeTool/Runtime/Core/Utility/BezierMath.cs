using System.Collections.Generic;
using UnityEngine;

namespace NarrativeTool.Core.Utilities.Math
{
    /// <summary>
    /// Shared bezier math for edge rendering and hit-testing.
    /// A path is described by a list of anchors. Between adjacent anchors A
    /// and B, we draw a cubic bezier with horizontal tangents (control points
    /// offset from A and B on the x-axis). This matches Unreal's look.
    /// </summary>
    public static class BezierMath
    {
        /// <summary>
        /// Compute control points for a single segment between two anchors.
        /// Horizontal tangent offset = max(40, |dx|/2).
        /// </summary>
        public static void ControlPoints(Vector2 a, Vector2 b, out Vector2 c1, out Vector2 c2)
        {
            float dx = Mathf.Max(40f, Mathf.Abs(b.x - a.x) * 0.5f);
            c1 = new Vector2(a.x + dx, a.y);
            c2 = new Vector2(b.x - dx, b.y);
        }

        public static Vector2 Evaluate(Vector2 a, Vector2 c1, Vector2 c2, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * u * a
                 + 3f * u * u * t * c1
                 + 3f * u * t * t * c2
                 + t * t * t * b;
        }

        /// <summary>
        /// Approximate distance from a point to a cubic bezier segment by
        /// flat-sampling. 20 samples is plenty for pick accuracy.
        /// </summary>
        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            ControlPoints(a, b, out var c1, out var c2);
            const int samples = 20;
            float best = float.MaxValue;
            Vector2 prev = a;
            for (int i = 1; i <= samples; i++)
            {
                float t = i / (float)samples;
                var pt = Evaluate(a, c1, c2, b, t);
                float d = DistanceToSegmentLine(p, prev, pt);
                if (d < best) best = d;
                prev = pt;
            }
            return best;
        }

        /// <summary>
        /// Total distance from a point to the multi-anchor path (a_0 ? a_1 ? ...).
        /// </summary>
        public static float DistanceToPath(Vector2 p, IList<Vector2> anchors)
        {
            if (anchors == null || anchors.Count < 2) return float.MaxValue;
            float best = float.MaxValue;
            for (int i = 0; i < anchors.Count - 1; i++)
            {
                float d = DistanceToSegment(p, anchors[i], anchors[i + 1]);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>
        /// Standard point-to-line-segment distance.
        /// </summary>
        public static float DistanceToSegmentLine(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-6f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            Vector2 proj = a + ab * t;
            return Vector2.Distance(p, proj);
        }

        /// <summary>
        /// Returns (segmentIndex, t) for the closest point on the multi-anchor
        /// path. Useful for "add waypoint here" positioning and for choosing
        /// where to insert a new waypoint in the list.
        /// </summary>
        public static (int segmentIndex, float t, Vector2 closest) ClosestPointOnPath(
            Vector2 p, IList<Vector2> anchors)
        {
            int bestSeg = 0;
            float bestT = 0f;
            Vector2 bestPoint = anchors.Count > 0 ? anchors[0] : Vector2.zero;
            float bestDist = float.MaxValue;

            for (int seg = 0; seg < anchors.Count - 1; seg++)
            {
                Vector2 a = anchors[seg], b = anchors[seg + 1];
                ControlPoints(a, b, out var c1, out var c2);
                const int samples = 30;
                Vector2 prev = a;
                for (int i = 1; i <= samples; i++)
                {
                    float t = i / (float)samples;
                    var pt = Evaluate(a, c1, c2, b, t);

                    // distance from p to the mini-line prev->pt
                    float d = DistanceToSegmentLine(p, prev, pt);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestSeg = seg;
                        bestT = t;
                        bestPoint = pt;
                    }
                    prev = pt;
                }
            }
            return (bestSeg, bestT, bestPoint);
        }

        /// <summary>
        /// Midpoint of the longest segment in an anchor path. Used for label
        /// placement.
        /// </summary>
        public static Vector2 MidpointOfLongestSegment(IList<Vector2> anchors)
        {
            if (anchors == null || anchors.Count < 2) return Vector2.zero;
            int bestSeg = 0;
            float bestLen = -1f;
            for (int i = 0; i < anchors.Count - 1; i++)
            {
                float d = Vector2.Distance(anchors[i], anchors[i + 1]);
                if (d > bestLen) { bestLen = d; bestSeg = i; }
            }
            Vector2 a = anchors[bestSeg], b = anchors[bestSeg + 1];
            ControlPoints(a, b, out var c1, out var c2);
            return Evaluate(a, c1, c2, b, 0.5f);
        }
    }
}