using System.Collections.Generic;
using NarrativeTool.Core;
using NarrativeTool.Data;
using NarrativeTool.Data.Commands;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas
{
    /// <summary>
    /// The canvas view. Does not own command history or selection — those
    /// are per-graph services looked up from the SessionState on Bind.
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
        private ContextMenuController contextMenu;
        private SelectionService selection;
        private SessionState session;

        public GraphDocument Graph => graph;
        public CommandSystem Commands => commands;
        public EventBus Bus => bus;
        public SelectionService Selection => selection;
        public ContextMenuController ContextMenu => contextMenu;
        public SessionState Session => session;

        public GraphCanvas()
        {
            AddToClassList("nt-canvas");
            style.flexGrow = 1;
            style.overflow = Overflow.Hidden;
            focusable = true;

            ContentLayer = new VisualElement { name = "content-layer" };
            ContentLayer.style.position = Position.Absolute;
            ContentLayer.style.left = 0;
            ContentLayer.style.top = 0;
            ContentLayer.usageHints = UsageHints.GroupTransform;
            ContentLayer.style.transformOrigin = new TransformOrigin(0, 0, 0);
            Add(ContentLayer);

            EdgeLayer = new EdgeLayer { name = "edge-layer" };
            EdgeLayer.style.position = Position.Absolute;
            EdgeLayer.style.left = 0;
            EdgeLayer.style.top = 0;
            EdgeLayer.pickingMode = PickingMode.Ignore;
            ContentLayer.Add(EdgeLayer);

            Camera = new PanZoomManipulator(this);
            this.AddManipulator(Camera);

            RegisterCallback<PointerDownEvent>(OnCanvasPointerDown);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        public Vector2 ScreenToWorld(Vector2 canvasLocal)
            => (canvasLocal - Camera.PanOffset) / Camera.Zoom;

        public Vector2 WorldToScreen(Vector2 world)
            => world * Camera.Zoom + Camera.PanOffset;

        public void Bind(GraphDocument graph, SessionState session, ContextMenuController contextMenu)
        {
            this.graph = graph;
            this.session = session;
            this.bus = session.Bus;
            this.commands = session.CommandsFor(graph);
            this.selection = session.SelectionFor(graph);
            this.contextMenu = contextMenu;

            foreach (var nv in nodeViews.Values) nv.RemoveFromHierarchy();
            nodeViews.Clear();

            foreach (var node in graph.Nodes) AddNodeView(node);

            ReconcileSelectionAfterRebind();

            EdgeLayer.Bind(graph, nodeViews);
            EdgeLayer.MarkDirtyRepaint();

            bus.Subscribe<NodeMovedEvent>(OnNodeMoved);
            bus.Subscribe<NodeAddedEvent>(OnNodeAdded);
            bus.Subscribe<NodeRemovedEvent>(OnNodeRemoved);
            bus.Subscribe<EdgeAddedEvent>(OnEdgeAdded);
            bus.Subscribe<EdgeRemovedEvent>(OnEdgeRemoved);
        }

        private void ReconcileSelectionAfterRebind()
        {
            // On first bind, selection is empty — no-op. On rebind (future
            // tab-switch), the selection set may hold stale view references.
            // Map them onto freshly-built views by Node.Id.
            if (selection.Count == 0) return;

            var stale = selection.Snapshot();
            selection.ApplyDirect(new HashSet<ISelectable>()); // silent clear
            var revived = new HashSet<ISelectable>();
            foreach (var s in stale)
            {
                if (s is NodeView nv && nodeViews.TryGetValue(nv.Node.Id, out var current))
                    revived.Add(current);
            }
            if (revived.Count > 0) selection.ApplyDirect(revived);
        }

        private void AddNodeView(Node node)
        {
            var view = new NodeView(node, this);
            nodeViews[node.Id] = view;
            ContentLayer.Add(view);
        }

        private void OnNodeMoved(NodeMovedEvent e)
        {
            if (graph == null || e.GraphId != graph.Id) return;
            if (nodeViews.TryGetValue(e.NodeId, out var view))
            {
                view.SyncPositionFromData();
                EdgeLayer.MarkDirtyRepaint();
            }
        }

        private void OnNodeAdded(NodeAddedEvent e)
        {
            if (graph == null || e.GraphId != graph.Id) return;
            var node = graph.FindNode(e.NodeId);
            if (node != null && !nodeViews.ContainsKey(node.Id))
            {
                AddNodeView(node);
                EdgeLayer.MarkDirtyRepaint();
            }
        }

        private void OnNodeRemoved(NodeRemovedEvent e)
        {
            if (graph == null || e.GraphId != graph.Id) return;
            if (nodeViews.TryGetValue(e.NodeId, out var view))
            {
                // DeleteSelected clears selection up-front in the same
                // transaction, so no command-routed Deselect is needed here.
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

        private void OnCanvasPointerDown(PointerDownEvent e)
        {
            Focus();

            if (!ReferenceEquals(e.target, this)) return;

            if (e.button == 0)
            {
                if (!e.shiftKey) selection?.Clear();
            }
            else if (e.button == 1)
            {
                if (contextMenu != null)
                {
                    var worldPos = ScreenToWorld(e.localPosition);
                    contextMenu.Open(new CanvasContextTarget(this, worldPos), e.position);
                    e.StopPropagation();
                }
            }
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            if (commands == null) return;

            bool ctrl = e.ctrlKey || e.commandKey;

            if (ctrl && e.keyCode == KeyCode.Z)
            {
                if (e.shiftKey) commands.Redo();
                else commands.Undo();
                e.StopPropagation();
                return;
            }

            if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
            {
                DeleteSelected();
                e.StopPropagation();
                return;
            }
        }


        /// <summary>
        /// Delete the current selection as a single undoable transaction.
        /// Clears selection first (inside the transaction) so undo restores
        /// nodes and the selection together.
        /// </summary>
        public void DeleteSelected()
        {
            if (selection == null || selection.Count == 0) return;
            var snapshot = selection.Snapshot();

            using (commands.BeginTransaction("Delete selection"))
            {
                selection.Clear();

                foreach (var s in snapshot)
                {
                    if (s is NodeView nv)
                        commands.Execute(new RemoveNodeCmd(graph, bus, nv.Node.Id));
                }
            }
        }
    }

    public sealed class CanvasContextTarget
    {
        public GraphCanvas Canvas { get; }
        public Vector2 WorldPosition { get; }
        public CanvasContextTarget(GraphCanvas canvas, Vector2 worldPos)
        {
            Canvas = canvas; WorldPosition = worldPos;
        }
    }
}