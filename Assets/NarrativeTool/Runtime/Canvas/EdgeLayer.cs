using System.Collections.Generic;
using NarrativeTool.Core;
using NarrativeTool.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas
{
    /// <summary>
    /// Parent layer holding per-edge EdgeView children, plus an overlay
    /// element for the in-progress edge drag preview.
    /// </summary>
    public sealed class EdgeLayer : VisualElement
    {
        private GraphDocument graph;
        private Dictionary<string, NodeView> nodeViews;
        private readonly Dictionary<string, EdgeView> edgeViews = new();

        private readonly EdgePreviewOverlay previewOverlay;

        public IReadOnlyDictionary<string, EdgeView> EdgeViews => edgeViews;

        public EdgeLayer()
        {
            style.position = Position.Absolute;
            // We don't pick ourselves — edges do. Preview overlay also ignores.
            pickingMode = PickingMode.Ignore;

            previewOverlay = new EdgePreviewOverlay();
            previewOverlay.style.position = Position.Absolute;
            previewOverlay.pickingMode = PickingMode.Ignore;
            Add(previewOverlay);
        }

        public void Bind(GraphDocument graph, Dictionary<string, NodeView> nodeViews)
        {
            this.graph = graph;
            this.nodeViews = nodeViews;
            RebuildEdges();
        }

        public void RebuildEdges()
        {
            foreach (var ev in edgeViews.Values) ev.RemoveFromHierarchy();
            edgeViews.Clear();
            if (graph == null) return;
            foreach (var edge in graph.Edges) AddEdgeView(edge);
            RefreshAll();
        }

        public void AddEdgeView(Edge edge)
        {
            if (edgeViews.ContainsKey(edge.Id)) return;
            var ev = new EdgeView(edge, FindCanvas());
            edgeViews[edge.Id] = ev;
            Insert(childCount - 1, ev); // keep preview overlay on top
            ev.RefreshBounds();
        }

        public void RemoveEdgeView(string edgeId)
        {
            if (!edgeViews.TryGetValue(edgeId, out var ev)) return;
            ev.RemoveFromHierarchy();
            edgeViews.Remove(edgeId);
        }

        public EdgeView Get(string edgeId)
        {
            edgeViews.TryGetValue(edgeId, out var v);
            return v;
        }

        public void RefreshEdge(string edgeId)
        {
            if (edgeViews.TryGetValue(edgeId, out var ev)) ev.RefreshBounds();
        }

        /// <summary>
        /// Refresh every edge (e.g. after a node moved, which reroutes every
        /// attached edge). Cheap — just bounds & repaint.
        /// </summary>
        public void RefreshAll()
        {
            foreach (var ev in edgeViews.Values) ev.RefreshBounds();
        }

        public EdgePreviewOverlay Preview => previewOverlay;

        private GraphCanvas FindCanvas()
        {
            VisualElement ve = this;
            while (ve != null) { if (ve is GraphCanvas gc) return gc; ve = ve.parent; }
            return null;
        }

        /// <summary>
        /// Set the opacity multiplier on an existing edge, used by the
        /// creation manipulator to dim an edge that will be replaced.
        /// </summary>
        public void SetEdgeGhost(string edgeId, bool ghost)
        {
            if (!edgeViews.TryGetValue(edgeId, out var ev)) return;
            ev.style.opacity = ghost ? 0.35f : 1f;
        }
    }

    /// <summary>
    /// Transparent overlay drawing the in-progress edge bezier while the user
    /// drags from a port. Set Active/Start/End/Enabled externally.
    /// </summary>
    public sealed class EdgePreviewOverlay : VisualElement
    {
        public bool Active { get; private set; }
        public Vector2 Start { get; private set; }
        public Vector2 End { get; private set; }
        public bool Forward { get; private set; } = true;

        public EdgePreviewOverlay()
        {
            generateVisualContent += OnDraw;
            // Size to parent by explicit 0/0 and full width/height
            style.left = 0; style.top = 0;
            style.width = new Length(100, LengthUnit.Percent);
            style.height = new Length(100, LengthUnit.Percent);
        }

        public void Show(Vector2 start, Vector2 end, bool forward)
        {
            Active = true;
            Start = start;
            End = end;
            Forward = forward;
            MarkDirtyRepaint();
        }

        public void Hide()
        {
            Active = false;
            MarkDirtyRepaint();
        }

        private void OnDraw(MeshGenerationContext ctx)
        {
            if (!Active) return;
            var p2d = ctx.painter2D;
            p2d.lineWidth = 2f;
            p2d.strokeColor = new Color(1f, 1f, 1f, 0.55f);
            p2d.lineCap = LineCap.Round;

            var a = Forward ? Start : End;
            var b = Forward ? End : Start;
            BezierMath.ControlPoints(a, b, out var c1, out var c2);

            p2d.BeginPath();
            p2d.MoveTo(a);
            p2d.BezierCurveTo(c1, c2, b);
            p2d.Stroke();
        }
    }
}