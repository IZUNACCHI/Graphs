using System.Collections.Generic;
using NarrativeTool.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas
{
    /// <summary>
    /// A single VisualElement that draws every edge of the graph as a bezier
    /// curve using Painter2D (generateVisualContent). Sits behind the NodeViews.
    /// Call MarkDirtyRepaint() whenever any anchor position changes.
    /// </summary>
    public sealed class EdgeLayer : VisualElement
    {
        private GraphDocument graph;
        private Dictionary<string, NodeView> nodeViews;

        public EdgeLayer()
        {
            generateVisualContent += OnGenerateVisualContent;
            // Will be sized to match its parent content layer; no fixed size.
            style.position = Position.Absolute;
            pickingMode = PickingMode.Ignore;
        }

        public void Bind(GraphDocument graph, Dictionary<string, NodeView> nodeViews)
        {
            this.graph = graph;
            this.nodeViews = nodeViews;
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (graph == null || nodeViews == null) return;

            var p2d = ctx.painter2D;
            p2d.lineWidth = 2f;
            p2d.strokeColor = new Color(0.76f, 0.76f, 0.76f, 1f);
            p2d.lineCap = LineCap.Round;

            foreach (var edge in graph.Edges)
            {
                if (!nodeViews.TryGetValue(edge.FromNodeId, out var fromNv)) continue;
                if (!nodeViews.TryGetValue(edge.ToNodeId, out var toNv)) continue;

                var fromPv = fromNv.GetPortView(edge.FromPortId);
                var toPv = toNv.GetPortView(edge.ToPortId);
                if (fromPv == null || toPv == null) continue;

                // Wait one frame if layout hasn't happened yet
                if (float.IsNaN(fromPv.Glyph.worldBound.width) ||
                    fromPv.Glyph.worldBound.width == 0f) continue;

                var a = fromPv.GetAnchorIn(this);
                var b = toPv.GetAnchorIn(this);

                DrawBezier(p2d, a, b);
            }
        }

        /// <summary>
        /// Horizontal-tangent cubic bezier, like Unreal. Control points are
        /// offset horizontally by ~half the x-distance so the curve eases out
        /// of the source to the right and into the target from the left.
        /// </summary>
        private static void DrawBezier(Painter2D p, Vector2 a, Vector2 b)
        {
            float dx = Mathf.Max(40f, Mathf.Abs(b.x - a.x) * 0.5f);
            var c1 = new Vector2(a.x + dx, a.y);
            var c2 = new Vector2(b.x - dx, b.y);
            p.BeginPath();
            p.MoveTo(a);
            p.BezierCurveTo(c1, c2, b);
            p.Stroke();
        }
    }
}