// ===== File: Assets/NarrativeTool/Runtime/Canvas/GraphCanvas.cs =====
using System.Collections.Generic;
using NarrativeTool.Core;
using NarrativeTool.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas
{
    /// <summary>
    /// The canvas view. Holds a pannable/zoomable content layer that contains
    /// the EdgeLayer (behind) and the NodeViews (on top).
    ///
    /// Pan/zoom state lives here (owned by the PanZoomManipulator) so other
    /// manipulators can call ScreenToWorld / WorldToScreen without knowing the
    /// manipulator's internals.
    /// </summary>
    [UxmlElement]
    public sealed partial class GraphCanvas : VisualElement
    {
        public VisualElement ContentLayer { get; }
        public EdgeLayer EdgeLayer { get; }
        public PanZoomManipulator Camera { get; }

        private readonly Dictionary<string, NodeView> nodeViews = new();
        private GraphDocument graph;
        private EventBus bus;
        private CommandSystem commands;

        public GraphDocument Graph => graph;
        public CommandSystem Commands => commands;
        public EventBus Bus => bus;

        public GraphCanvas()
        {
            AddToClassList("nt-canvas");
            style.flexGrow = 1;
            style.overflow = Overflow.Hidden;
            focusable = true;

            // Content layer: this is what pans and zooms. Nodes and edges live inside.
            ContentLayer = new VisualElement { name = "content-layer" };
            ContentLayer.style.position = Position.Absolute;
            ContentLayer.style.left = 0;
            ContentLayer.style.top = 0;
            ContentLayer.usageHints = UsageHints.GroupTransform;
            // Transform origin at top-left so translate/scale math is straightforward
            ContentLayer.style.transformOrigin = new TransformOrigin(0, 0, 0);
            Add(ContentLayer);

            // Edge layer sits behind the nodes inside the content layer
            EdgeLayer = new EdgeLayer { name = "edge-layer" };
            EdgeLayer.style.position = Position.Absolute;
            EdgeLayer.style.left = 0;
            EdgeLayer.style.top = 0;
            EdgeLayer.pickingMode = PickingMode.Ignore;
            ContentLayer.Add(EdgeLayer);

            // Pan/zoom owns the content-layer transform state
            Camera = new PanZoomManipulator(this);
            this.AddManipulator(Camera);
        }

        /// <summary>
        /// Convert a position in this canvas's local coordinate system (i.e. a
        /// pointer event's .localPosition, or .position when the canvas fills
        /// the panel) into content-layer / "world" coordinates — which is the
        /// space in which Node.Position lives.
        /// </summary>
        public Vector2 ScreenToWorld(Vector2 canvasLocal)
            => (canvasLocal - Camera.PanOffset) / Camera.Zoom;

        /// <summary>Inverse of <see cref="ScreenToWorld"/>.</summary>
        public Vector2 WorldToScreen(Vector2 world)
            => world * Camera.Zoom + Camera.PanOffset;

        /// <summary>
        /// Bind this canvas to a graph. Clears any prior views and builds fresh
        /// ones. Subscribes to bus events for live view updates.
        /// </summary>
        public void Bind(GraphDocument graph, EventBus bus, CommandSystem commands)
        {
            this.graph = graph; this.bus = bus; this.commands = commands;

            foreach (var nv in nodeViews.Values) nv.RemoveFromHierarchy();
            nodeViews.Clear();

            foreach (var node in graph.Nodes)
                AddNodeView(node);

            EdgeLayer.Bind(graph, nodeViews);
            EdgeLayer.MarkDirtyRepaint();

            bus.Subscribe<NodeMovedEvent>(OnNodeMoved);
            bus.Subscribe<NodeAddedEvent>(OnNodeAdded);
            bus.Subscribe<NodeRemovedEvent>(OnNodeRemoved);
            bus.Subscribe<EdgeAddedEvent>(OnEdgeAdded);
            bus.Subscribe<EdgeRemovedEvent>(OnEdgeRemoved);
        }

        private void AddNodeView(Node node)
        {
            var view = new NodeView(node, this);
            nodeViews[node.Id] = view;
            ContentLayer.Add(view);
        }

        private void OnNodeMoved(NodeMovedEvent e)
        {
            if (e.GraphId != graph.Id) return;
            if (nodeViews.TryGetValue(e.NodeId, out var view))
            {
                view.SyncPositionFromData();
                EdgeLayer.MarkDirtyRepaint();
            }
        }

        private void OnNodeAdded(NodeAddedEvent e)
        {
            if (e.GraphId != graph.Id) return;
            var node = graph.FindNode(e.NodeId);
            if (node != null && !nodeViews.ContainsKey(node.Id))
            {
                AddNodeView(node);
                EdgeLayer.MarkDirtyRepaint();
            }
        }

        private void OnNodeRemoved(NodeRemovedEvent e)
        {
            if (e.GraphId != graph.Id) return;
            if (nodeViews.TryGetValue(e.NodeId, out var view))
            {
                view.RemoveFromHierarchy();
                nodeViews.Remove(e.NodeId);
                EdgeLayer.MarkDirtyRepaint();
            }
        }

        private void OnEdgeAdded(EdgeAddedEvent _) => EdgeLayer.MarkDirtyRepaint();
        private void OnEdgeRemoved(EdgeRemovedEvent _) => EdgeLayer.MarkDirtyRepaint();

        public NodeView GetNodeView(string nodeId)
        {
            nodeViews.TryGetValue(nodeId, out var v);
            return v;
        }
    }
}