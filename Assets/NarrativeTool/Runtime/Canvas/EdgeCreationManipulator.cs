using NarrativeTool.Core;
using NarrativeTool.Data;
using NarrativeTool.Data.Commands;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas
{
    /// <summary>
    /// Attached to a PortView's glyph. Left-drag from the glyph starts an
    /// edge-creation session:
    ///  - Preview bezier drawn from the source port to the cursor
    ///  - On hover over a compatible port: highlight
    ///  - Incompatible port: small tooltip explaining why
    ///  - On release over a compatible port: create (or replace) edge
    ///  - On release over empty canvas: open context menu for node creation
    ///    with auto-connect
    ///  - During drag: if source is a Single-capacity port with an existing
    ///    edge, that edge renders at reduced opacity (will be replaced)
    ///
    /// Compatibility: same TypeTag, and the drag results in one output +
    /// one input combination (both-output or both-input is rejected).
    /// </summary>
    public sealed class EdgeCreationManipulator : Manipulator
    {
        private readonly PortView sourcePort;

        private bool dragging;
        private Vector2 sourceWorld;     // cached start anchor in content-layer coords
        private string ghostedEdgeId;    // an existing edge being previewed as "will be replaced"
        private PortView hoveredPort;    // port glyph currently under cursor (if any)
        private VisualElement tooltipElement;

        public EdgeCreationManipulator(PortView port) { sourcePort = port; }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnDown);
            target.RegisterCallback<PointerMoveEvent>(OnMove);
            target.RegisterCallback<PointerUpEvent>(OnUp);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnDown);
            target.UnregisterCallback<PointerMoveEvent>(OnMove);
            target.UnregisterCallback<PointerUpEvent>(OnUp);
        }

        private GraphCanvas Canvas => sourcePort.OwnerNode?.Canvas;

        private void OnDown(PointerDownEvent e)
        {
            if (e.button != 0) return;
            if (Canvas == null) return;

            dragging = true;
            sourceWorld = sourcePort.GetAnchorIn(Canvas.ContentLayer);

            // If the source is Single-capacity and already has an edge,
            // ghost that edge (0.35 opacity) — it'll be replaced on drop.
            ghostedEdgeId = FindExistingEdgeForSingleCapacitySource();
            if (ghostedEdgeId != null)
                Canvas.EdgeLayer.SetEdgeGhost(ghostedEdgeId, true);

            var cursorWorld = CursorWorld(e);
            Canvas.EdgeLayer.Preview.Show(sourceWorld, cursorWorld, sourcePort.Port.Direction == PortDirection.Output);

            target.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        private void OnMove(PointerMoveEvent e)
        {
            if (!dragging) return;
            if (Canvas == null) return;

            var cursorWorld = CursorWorld(e);
            Canvas.EdgeLayer.Preview.Show(sourceWorld, cursorWorld, sourcePort.Port.Direction == PortDirection.Output);

            // Find port under cursor (in world coords)
            var pv = FindPortUnderCursor(e);

            if (!ReferenceEquals(pv, hoveredPort))
            {
                if (hoveredPort != null) hoveredPort.SetCompatibleHighlight(false);
                HideTooltip();

                hoveredPort = pv;
                if (hoveredPort != null)
                {
                    var compat = EvaluateCompat(hoveredPort);
                    if (compat.ok)
                        hoveredPort.SetCompatibleHighlight(true);
                    else
                        ShowTooltip(hoveredPort, compat.reason);
                }
            }

            e.StopPropagation();
        }

        private void OnUp(PointerUpEvent e)
        {
            if (!dragging) return;
            if (target.HasPointerCapture(e.pointerId)) target.ReleasePointer(e.pointerId);
            dragging = false;

            if (Canvas == null) return;

            // Cleanup visuals
            Canvas.EdgeLayer.Preview.Hide();
            if (hoveredPort != null) hoveredPort.SetCompatibleHighlight(false);
            HideTooltip();
            if (ghostedEdgeId != null)
            {
                Canvas.EdgeLayer.SetEdgeGhost(ghostedEdgeId, false);
            }

            // Completion logic
            var targetPort = hoveredPort;
            hoveredPort = null;

            if (targetPort != null)
            {
                var compat = EvaluateCompat(targetPort);
                if (compat.ok)
                {
                    CommitEdge(targetPort);
                }
                // incompatible ? drop is a no-op
            }
            else
            {
                // Dropped over empty canvas — open node-creation context menu
                OpenDropMenu(e);
            }

            ghostedEdgeId = null;
            e.StopPropagation();
        }

        // ---------- Compatibility ----------

        private (bool ok, string reason) EvaluateCompat(PortView other)
        {
            if (ReferenceEquals(other, sourcePort)) return (false, "Same port");
            if (other.OwnerNode == sourcePort.OwnerNode) return (false, "Same node");

            // Exactly one input and one output
            if (other.Port.Direction == sourcePort.Port.Direction)
                return (false, sourcePort.Port.Direction == PortDirection.Output
                    ? "Both are outputs" : "Both are inputs");

            // TypeTag match
            if (other.Port.TypeTag != sourcePort.Port.TypeTag)
                return (false, $"Type mismatch: '{sourcePort.Port.TypeTag}' ? '{other.Port.TypeTag}'");

            return (true, null);
        }

        // ---------- Completion ----------

        private void CommitEdge(PortView targetPort)
        {
            // Normalize direction: From = output, To = input
            PortView outPort, inPort;
            if (sourcePort.Port.Direction == PortDirection.Output)
            { outPort = sourcePort; inPort = targetPort; }
            else
            { outPort = targetPort; inPort = sourcePort; }

            var graph = Canvas.Graph;
            var bus = Canvas.Bus;

            // Replace-on-single-capacity: if output port is single and has an edge, remove it first
            string replaceEdgeId = null;
            if (outPort.Port.Capacity == PortCapacity.Single)
            {
                foreach (var ed in graph.Edges)
                {
                    if (ed.FromNodeId == outPort.OwnerNode.Node.Id && ed.FromPortId == outPort.Port.Id)
                    {
                        replaceEdgeId = ed.Id; break;
                    }
                }
            }

            // Also enforce single-capacity on input side (rare — our current
            // model uses Multi for inputs, but honor the flag if someone
            // changes it later).
            string replaceInputEdgeId = null;
            if (inPort.Port.Capacity == PortCapacity.Single)
            {
                foreach (var ed in graph.Edges)
                {
                    if (ed.ToNodeId == inPort.OwnerNode.Node.Id && ed.ToPortId == inPort.Port.Id)
                    {
                        replaceInputEdgeId = ed.Id; break;
                    }
                }
            }

            var newEdge = new Edge(
                NewEdgeId(),
                outPort.OwnerNode.Node.Id, outPort.Port.Id,
                inPort.OwnerNode.Node.Id, inPort.Port.Id);

            using (Canvas.Commands.BeginTransaction("Create edge"))
            {
                if (replaceEdgeId != null)
                    Canvas.Commands.Execute(new RemoveEdgeCmd(graph, bus, replaceEdgeId));
                if (replaceInputEdgeId != null && replaceInputEdgeId != replaceEdgeId)
                    Canvas.Commands.Execute(new RemoveEdgeCmd(graph, bus, replaceInputEdgeId));
                Canvas.Commands.Execute(new AddEdgeCmd(graph, bus, newEdge));
            }
        }

        private void OpenDropMenu(PointerUpEvent e)
        {
            if (Canvas.ContextMenu == null) return;
            var worldPos = CursorWorld(e);
            var payload = new EdgeDropContextTarget(Canvas, sourcePort, worldPos);
            Canvas.ContextMenu.Open(payload, e.position);
        }

        // ---------- Helpers ----------

        private string FindExistingEdgeForSingleCapacitySource()
        {
            if (sourcePort.Port.Capacity != PortCapacity.Single) return null;
            var graph = Canvas?.Graph;
            if (graph == null) return null;

            if (sourcePort.Port.Direction == PortDirection.Output)
            {
                foreach (var ed in graph.Edges)
                    if (ed.FromNodeId == sourcePort.OwnerNode.Node.Id && ed.FromPortId == sourcePort.Port.Id)
                        return ed.Id;
            }
            else
            {
                foreach (var ed in graph.Edges)
                    if (ed.ToNodeId == sourcePort.OwnerNode.Node.Id && ed.ToPortId == sourcePort.Port.Id)
                        return ed.Id;
            }
            return null;
        }

        private PortView FindPortUnderCursor(IPointerEvent e)
        {
            // Panel-space point
            var panelRoot = Canvas.panel.visualTree;
            var panelPoint = (Vector2)e.position;

            var picked = panelRoot.panel.Pick(panelPoint);
            while (picked != null)
            {
                // Port glyph carries the EdgeCreationManipulator — look for
                // a PortView ancestor.
                if (picked.parent is PortView pv) return pv;
                if (picked is PortView direct) return direct;
                picked = picked.parent;
            }
            return null;
        }

        private Vector2 CursorWorld(IPointerEvent e)
        {
            var canvas = Canvas;
            var canvasLocal = (Vector2)e.position;
            var panelOrigin = canvas.parent != null
                ? canvas.parent.LocalToWorld(canvas.layout.position)
                : Vector2.zero;
            canvasLocal -= panelOrigin;
            return canvas.ScreenToWorld(canvasLocal);
        }

        private static string NewEdgeId()
            => $"e_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";

        // ---------- Incompatibility tooltip ----------

        private void ShowTooltip(PortView nearPort, string message)
        {
            if (Canvas == null) return;
            HideTooltip();
            tooltipElement = new Label(message);
            tooltipElement.AddToClassList("nt-edge-compat-tooltip");
            tooltipElement.style.position = Position.Absolute;

            // Place tooltip near the port in panel-local coords.
            var portRect = nearPort.Glyph.worldBound;
            var panelRoot = Canvas.panel.visualTree;
            tooltipElement.style.left = portRect.xMax + 8f;
            tooltipElement.style.top = portRect.yMin - 4f;

            panelRoot.Add(tooltipElement);
        }

        private void HideTooltip()
        {
            if (tooltipElement == null) return;
            tooltipElement.RemoveFromHierarchy();
            tooltipElement = null;
        }
    }

    /// <summary>
    /// Passed to context-menu providers when an edge-creation drag is
    /// released over empty canvas.
    /// </summary>
    public sealed class EdgeDropContextTarget
    {
        public GraphCanvas Canvas { get; }
        public PortView SourcePort { get; }
        public Vector2 WorldPosition { get; }
        public EdgeDropContextTarget(GraphCanvas canvas, PortView sourcePort, Vector2 pos)
        {
            Canvas = canvas; SourcePort = sourcePort; WorldPosition = pos;
        }
    }
}