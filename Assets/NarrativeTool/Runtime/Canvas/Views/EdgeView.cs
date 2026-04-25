using System.Collections.Generic;
using NarrativeTool.Canvas.Manipulators;
using NarrativeTool.Core.Commands;
using NarrativeTool.Core.Selection;
using NarrativeTool.Core.Utilities.Math;
using NarrativeTool.Core.Widgets;
using NarrativeTool.Data.Graph;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas.Views
{
    /// <summary>
    /// One VisualElement per edge. Handles its own rendering (bezier path
    /// through ports and waypoints), label display, hit-testing, selection,
    /// waypoint drawing, and the context menu target.
    ///
    /// Rect covers the full bounding box of the path (with slack) so UI
    /// Toolkit can pick us; ContainsPoint is overridden to match only the
    /// stroke (or waypoints / label).
    /// </summary>
    public sealed class EdgeView : VisualElement, ISelectable
    {
        public const float HitTestSlackPx = 6f;
        public const float WaypointHitRadiusPx = 12f;
        public const float EdgeStrokePx = 2f;
        public const float EdgeStrokeSelectedPx = 3f;

        public Edge Edge { get; }
        public GraphView Canvas { get; }

        private Label labelView;
        private FlexTextField labelEditor;
        private bool isSelected;

        /// <summary>
        /// Selected waypoint indices (view-side). We don't track full
        /// selection state here — the canvas's SelectionService holds the
        /// authoritative set. This field just mirrors it for rendering.
        /// </summary>
        private readonly HashSet<int> selectedWaypointIndices = new();

        public EdgeView(Edge edge, GraphView canvas)
        {
            Edge = edge;
            Canvas = canvas;
            AddToClassList("nt-edge");
            style.position = Position.Absolute;
            pickingMode = PickingMode.Position;

            generateVisualContent += OnGenerateVisualContent;

            RegisterCallback<PointerDownEvent>(OnPointerDown);
        }

        public void RefreshBounds()
        {
            // Compute a big enough rect to contain all anchors. ContainsPoint
            // filters to the actual path / hit-slop.
            var anchors = GetAnchors();
            if (anchors.Count == 0) return;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var a in anchors)
            {
                if (a.x < minX) minX = a.x; if (a.x > maxX) maxX = a.x;
                if (a.y < minY) minY = a.y; if (a.y > maxY) maxY = a.y;
            }
            // Expand for bezier-sag and label space.
            const float pad = 80f;
            style.left = minX - pad;
            style.top = minY - pad;
            style.width = (maxX - minX) + pad * 2f;
            style.height = (maxY - minY) + pad * 2f;

            UpdateLabelDisplay();
            MarkDirtyRepaint();
        }

        /// <summary>
        /// Compute anchor list in content-layer coordinates:
        /// [sourcePort, waypoint0, waypoint1, ..., targetPort].
        /// Returns empty list if either port view is missing.
        /// </summary>
        public List<Vector2> GetAnchors()
        {
            var list = new List<Vector2>();
            var fromNv = Canvas.GetNodeView(Edge.FromNodeId);
            var toNv = Canvas.GetNodeView(Edge.ToNodeId);
            if (fromNv == null || toNv == null) return list;
            var fromPv = fromNv.GetPortView(Edge.FromPortId);
            var toPv = toNv.GetPortView(Edge.ToPortId);
            if (fromPv == null || toPv == null) return list;

            // Anchors are in content-layer coords (where waypoints live too).
            var contentLayer = Canvas.ContentLayer;
            var fromWorld = fromPv.GetAnchorIn(contentLayer);
            var toWorld = toPv.GetAnchorIn(contentLayer);

            list.Add(fromWorld);
            foreach (var w in Edge.Waypoints) list.Add(w.Position);
            list.Add(toWorld);
            return list;
        }

        public override bool ContainsPoint(Vector2 localPoint)
        {
            // localPoint is in this element's local coords. Translate into
            // content-layer coords for distance math.
            var anchors = GetAnchors();
            if (anchors.Count < 2) return false;

            // Offset: style.left/top places this element in content-layer
            // coords, so a localPoint (lx, ly) corresponds to content
            // (style.left + lx, style.top + ly).
            var worldPoint = new Vector2(
                resolvedStyle.left + localPoint.x,
                resolvedStyle.top + localPoint.y);

            // Hit on waypoints
            for (int i = 0; i < Edge.Waypoints.Count; i++)
            {
                if (Vector2.Distance(worldPoint, Edge.Waypoints[i].Position) <= WaypointHitRadiusPx)
                    return true;
            }

            // Hit on bezier path
            float d = BezierMath.DistanceToPath(worldPoint, anchors);
            if (d <= HitTestSlackPx) return true;

            // Hit on label (if any) — ContainsPoint of the label child
            if (labelView != null && !string.IsNullOrEmpty(Edge.Label))
            {
                var lb = labelView.worldBound;
                var worldClick = this.LocalToWorld(localPoint);
                if (lb.Contains(worldClick)) return true;
            }

            return false;
        }

        /// <summary>Return world-space position of the clicked waypoint, or -1.</summary>
        public int WaypointIndexUnderPoint(Vector2 worldPoint)
        {
            for (int i = 0; i < Edge.Waypoints.Count; i++)
            {
                if (Vector2.Distance(worldPoint, Edge.Waypoints[i].Position) <= WaypointHitRadiusPx)
                    return i;
            }
            return -1;
        }

        // ---------- Selection ----------

        void ISelectable.OnSelected()
        {
            isSelected = true;
            AddToClassList("nt-edge--selected");
            MarkDirtyRepaint();
        }

        void ISelectable.OnDeselected()
        {
            isSelected = false;
            RemoveFromClassList("nt-edge--selected");
            MarkDirtyRepaint();
        }

        // ---------- Click / right-click / double-click ----------

        private float lastClickTime = -1f;
        private Vector2 lastClickWorld;

        private void OnPointerDown(PointerDownEvent e)
        {
            // Skip if inside the label editor
            if (e.target is VisualElement ve && IsInsideTextField(ve)) return;

            var worldPoint = ToWorld(e.localPosition);

            // Check waypoint hit first
            int wpIndex = WaypointIndexUnderPoint(worldPoint);

            if (e.button == 0)
            {
                if (wpIndex >= 0)
                {
                    // Delegate waypoint interaction to the waypoint manipulator
                    // by exposing state the manipulator reads. For now, select
                    // on click — drag is handled by WaypointDragManipulator.
                    var wpSel = WaypointSelectable.Get(this, wpIndex);
                    if (e.shiftKey) Canvas.Selection.Toggle(wpSel);
                    else if (!Canvas.Selection.IsSelected(wpSel))
                        Canvas.Selection.SelectOnly(wpSel);
                    e.StopPropagation();
                    return;
                }

                // Double-click on body → add waypoint
                float now = Time.unscaledTime;
                if (lastClickTime > 0f && now - lastClickTime < 0.35f &&
                    Vector2.Distance(worldPoint, lastClickWorld) < 6f)
                {
                    AddWaypointAt(worldPoint);
                    lastClickTime = -1f;
                    e.StopPropagation();
                    return;
                }
                lastClickTime = now;
                lastClickWorld = worldPoint;

                // Single-click selects the edge
                if (e.shiftKey) Canvas.Selection.Toggle(this);
                else if (!Canvas.Selection.IsSelected(this))
                    Canvas.Selection.SelectOnly(this);
                e.StopPropagation();
            }
            else if (e.button == 1)
            {
                if (wpIndex >= 0)
                {
                    var wpSel = WaypointSelectable.Get(this, wpIndex);
                    if (!Canvas.Selection.IsSelected(wpSel)) Canvas.Selection.SelectOnly(wpSel);
                    Canvas.ContextMenu?.Open(new WaypointContextTarget(this, wpIndex), e.position);
                }
                else
                {
                    if (!Canvas.Selection.IsSelected(this)) Canvas.Selection.SelectOnly(this);
                    Canvas.ContextMenu?.Open(new EdgeContextTarget(this, worldPoint), e.position);
                }
                e.StopPropagation();
            }
        }

        private void AddWaypointAt(Vector2 worldPoint)
        {
            var anchors = GetAnchors();
            if (anchors.Count < 2) return;
            var (segIndex, _, closest) = BezierMath.ClosestPointOnPath(worldPoint, anchors);

            // Insert at waypoint-list-index corresponding to segIndex.
            // anchors: [from, w0, w1, ..., to]. Segment `segIndex` is between
            // anchors[segIndex] and anchors[segIndex+1]. So the new waypoint
            // goes at waypoint-list index `segIndex` (inserting "between
            // anchors[segIndex] and anchors[segIndex+1]").
            int waypointIndex = Mathf.Clamp(segIndex, 0, Edge.Waypoints.Count);
            Canvas.Commands.Execute(
                new AddWaypointCmd(Canvas.Graph, Canvas.Bus, Edge.Id, waypointIndex, closest));
        }

        private static bool IsInsideTextField(VisualElement ve)
        {
            while (ve != null) { if (ve is TextField) return true; ve = ve.parent; }
            return false;
        }

        private Vector2 ToWorld(Vector2 localInThis)
        {
            return new Vector2(resolvedStyle.left + localInThis.x,
                               resolvedStyle.top + localInThis.y);
        }

        // ---------- Label ----------

        public void BeginEditLabel()
        {
            if (labelEditor != null) return; // already editing
            var anchors = GetAnchors();
            if (anchors.Count < 2) return;
            var mid = BezierMath.MidpointOfLongestSegment(anchors);

            labelEditor = new FlexTextField(multiline: false) { value = Edge.Label ?? "" };
            labelEditor.AddToClassList("nt-edge-label-editor");
            labelEditor.style.position = Position.Absolute;

            // Local coords within this EdgeView
            float lx = mid.x - resolvedStyle.left;
            float ly = mid.y - resolvedStyle.top;
            labelEditor.style.left = lx - 60f;
            labelEditor.style.top = ly - 9f;
            labelEditor.style.width = 120f;
            Add(labelEditor);

            labelEditor.OnCommit += (oldLabel, newLabel) =>
            {
                Canvas.Commands.Execute(
                    new SetEdgeLabelCmd(Canvas.Graph, Canvas.Bus, Edge.Id, oldLabel, newLabel));
                EndEditLabel();
            };

            // Commit-on-blur means if the field loses focus without OnCommit
            // (net-zero change), we still want to close the editor.
            labelEditor.RegisterCallback<FocusOutEvent>(_ =>
            {
                // Schedule close after commit event has fired (it fires first
                // in the same frame if there was a change).
                schedule.Execute(EndEditLabel);
            });

            labelEditor.Focus();
            // Hide the static label while editing
            if (labelView != null) labelView.style.display = DisplayStyle.None;
        }

        public void EndEditLabel()
        {
            if (labelEditor != null)
            {
                labelEditor.RemoveFromHierarchy();
                labelEditor = null;
            }
            UpdateLabelDisplay();
        }

        private void UpdateLabelDisplay()
        {
            bool hasLabel = !string.IsNullOrEmpty(Edge.Label);

            if (!hasLabel)
            {
                if (labelView != null)
                {
                    labelView.RemoveFromHierarchy();
                    labelView = null;
                }
                return;
            }

            if (labelView == null)
            {
                labelView = new Label();
                labelView.AddToClassList("nt-edge-label");
                labelView.style.position = Position.Absolute;
                labelView.RegisterCallback<PointerDownEvent>(OnLabelPointerDown);
                Add(labelView);
            }

            labelView.text = Edge.Label;
            labelView.style.display = DisplayStyle.Flex;

            var anchors = GetAnchors();
            if (anchors.Count < 2) return;
            var mid = BezierMath.MidpointOfLongestSegment(anchors);
            labelView.style.left = (mid.x - resolvedStyle.left) - 40f;
            labelView.style.top = (mid.y - resolvedStyle.top) - 9f;
            labelView.style.width = 80f;
        }

        private float lastLabelClickTime = -1f;

        private void OnLabelPointerDown(PointerDownEvent e)
        {
            if (e.button != 0) return;
            float now = Time.unscaledTime;
            if (lastLabelClickTime > 0f && now - lastLabelClickTime < 0.35f)
            {
                BeginEditLabel();
                e.StopPropagation();
                lastLabelClickTime = -1f;
                return;
            }
            lastLabelClickTime = now;
            // Let normal edge click logic run too (single click selects edge).
        }

        // ---------- Rendering ----------

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var anchors = GetAnchors();
            if (anchors.Count < 2) return;

            var p2d = ctx.painter2D;
            float stroke = isSelected ? EdgeStrokeSelectedPx : EdgeStrokePx;
            p2d.lineWidth = stroke;
            p2d.strokeColor = isSelected
                ? new Color(1f, 1f, 1f, 1f)
                : new Color(0.76f, 0.76f, 0.76f, 1f);
            p2d.lineCap = LineCap.Round;

            // Draw the path as successive cubic beziers between anchors.
            p2d.BeginPath();
            var firstLocal = ToLocal(anchors[0]);
            p2d.MoveTo(firstLocal);
            for (int i = 0; i < anchors.Count - 1; i++)
            {
                var a = anchors[i];
                var b = anchors[i + 1];
                BezierMath.ControlPoints(a, b, out var c1, out var c2);
                p2d.BezierCurveTo(ToLocal(c1), ToLocal(c2), ToLocal(b));
            }
            p2d.Stroke();

            // Waypoints (filled circles, inherit stroke color, diameter
            // max(8, 2 * stroke)). Selection highlight = small square outline.
            float wpDiameter = Mathf.Max(8f, stroke * 2f);
            float wpRadius = wpDiameter * 0.5f;

            var fillColor = isSelected
                ? new Color(1f, 1f, 1f, 1f)
                : new Color(0.76f, 0.76f, 0.76f, 1f);

            for (int i = 0; i < Edge.Waypoints.Count; i++)
            {
                var w = Edge.Waypoints[i];
                var pLocal = ToLocal(w.Position);

                // Filled circle
                p2d.fillColor = fillColor;
                p2d.BeginPath();
                p2d.Arc(pLocal, wpRadius, 0f, 360f);
                p2d.Fill();

                // Selected waypoint → square outline around it
                if (selectedWaypointIndices.Contains(i))
                {
                    float squareSide = wpDiameter + 6f;
                    var topLeft = pLocal - new Vector2(squareSide * 0.5f, squareSide * 0.5f);
                    p2d.strokeColor = new Color(1f, 1f, 1f, 1f);
                    p2d.lineWidth = 1.5f;
                    p2d.BeginPath();
                    p2d.MoveTo(topLeft);
                    p2d.LineTo(topLeft + new Vector2(squareSide, 0f));
                    p2d.LineTo(topLeft + new Vector2(squareSide, squareSide));
                    p2d.LineTo(topLeft + new Vector2(0f, squareSide));
                    p2d.ClosePath();
                    p2d.Stroke();
                    // restore for next
                    p2d.lineWidth = stroke;
                    p2d.strokeColor = isSelected
                        ? new Color(1f, 1f, 1f, 1f)
                        : new Color(0.76f, 0.76f, 0.76f, 1f);
                }
            }
        }

        private Vector2 ToLocal(Vector2 worldPoint)
        {
            return new Vector2(worldPoint.x - resolvedStyle.left,
                               worldPoint.y - resolvedStyle.top);
        }

        public void SetWaypointSelected(int index, bool selected)
        {
            if (selected) selectedWaypointIndices.Add(index);
            else selectedWaypointIndices.Remove(index);
            MarkDirtyRepaint();
        }

        public bool IsWaypointSelected(int index) => selectedWaypointIndices.Contains(index);
    }

    /// <summary>
    /// Passed to context-menu providers on right-click of an edge body.
    /// </summary>
    public sealed class EdgeContextTarget
    {
        public EdgeView EdgeView { get; }
        public Vector2 WorldPoint { get; }
        public EdgeContextTarget(EdgeView edgeView, Vector2 worldPoint)
        {
            EdgeView = edgeView; WorldPoint = worldPoint;
        }
    }

    /// <summary>
    /// Passed to context-menu providers on right-click of a waypoint.
    /// </summary>
    public sealed class WaypointContextTarget
    {
        public EdgeView EdgeView { get; }
        public int WaypointIndex { get; }
        public WaypointContextTarget(EdgeView edgeView, int index)
        {
            EdgeView = edgeView; WaypointIndex = index;
        }
    }
}