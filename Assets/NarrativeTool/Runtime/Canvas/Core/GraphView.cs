using NarrativeTool.Canvas.Manipulators;
using NarrativeTool.Canvas.Views;
using NarrativeTool.Core;
using NarrativeTool.Core.Commands;
using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Core.Selection;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Project;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas
{
    [UxmlElement]
    public sealed partial class GraphView : VisualElement
    {
        public VisualElement ContentLayer { get; }
        public EdgeLayer EdgeLayer { get; }
        public PanZoomManipulator Camera { get; }

        private readonly Dictionary<string, NodeView> nodeViews = new();
        private GraphData graph;
        private EventBus bus;
        private CommandSystem commands;
        private ContextMenuController contextMenu;
        private SelectionService selection;
        private SessionState session;

        public GraphData Graph => graph;
        public CommandSystem Commands => commands;
        public EventBus Bus => bus;
        public SelectionService Selection => selection;
        public ContextMenuController ContextMenu => contextMenu;
        public SessionState Session => session;

        public GraphView()
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
            // EdgeLayer itself does not pick — per-edge EdgeViews do.
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

        public void Bind(GraphData graph, SessionState session, ContextMenuController contextMenu)
        {
            this.graph = graph;
            this.session = session;
            this.bus = session.Bus;
            this.commands = session.CommandsFor(graph);
            this.selection = session.SelectionFor(graph);
            this.contextMenu = contextMenu;

            // Clear any stale waypoint selectables from a previous bind.
            WaypointSelectable.ClearAll();

            foreach (var nv in nodeViews.Values) nv.RemoveFromHierarchy();
            nodeViews.Clear();

            foreach (var node in graph.Nodes) AddNodeView(node);

            ReconcileSelectionAfterRebind();

            EdgeLayer.Bind(graph, nodeViews);

            // After edge views are built, attach waypoint manipulators.
            InstallEdgeInteractors();

            bus.Subscribe<NodeMovedEvent>(OnNodeMoved);
            bus.Subscribe<NodeAddedEvent>(OnNodeAdded);
            bus.Subscribe<NodeRemovedEvent>(OnNodeRemoved);
            bus.Subscribe<EdgeAddedEvent>(OnEdgeAdded);
            bus.Subscribe<EdgeRemovedEvent>(OnEdgeRemoved);
            bus.Subscribe<EdgeLabelChangedEvent>(OnEdgeLabelChanged);
            bus.Subscribe<WaypointAddedEvent>(OnWaypointAdded);
            bus.Subscribe<WaypointRemovedEvent>(OnWaypointRemoved);
            bus.Subscribe<WaypointMovedEvent>(OnWaypointMoved);
        }

        private void InstallEdgeInteractors()
        {
            foreach (var kv in EdgeLayer.EdgeViews)
            {
                var ev = kv.Value;
                // Avoid double-install if we're re-entering this path.
                if (ev.GetManipulatorInstalled()) continue;
                ev.AddManipulator(new WaypointDragManipulator(ev));
                ev.SetManipulatorInstalled();
            }
        }

        private void ReconcileSelectionAfterRebind()
        {
            if (selection.Count == 0) return;

            var stale = selection.Snapshot();
            selection.ApplyDirect(new HashSet<ISelectable>());
            var revived = new HashSet<ISelectable>();
            foreach (var s in stale)
            {
                if (s is NodeView nv && nodeViews.TryGetValue(nv.Node.Id, out var current))
                    revived.Add(current);
                // EdgeView / WaypointSelectable from prior binds are not
                // carried over — edge views are rebuilt from scratch.
            }
            if (revived.Count > 0) selection.ApplyDirect(revived);
        }

        private void AddNodeView(NodeData node)
        {
            // Use the registry to get the correct view type
            var registry = Services.Get<NodeRegistry>();
            NodeView view = registry.CreateView(node, this) ?? new NodeView(node, this);
            nodeViews[node.Id] = view;
            ContentLayer.Add(view);
        }

        // ---------- Node event handlers ----------

        private void OnNodeMoved(NodeMovedEvent e)
        {
            if (graph == null || e.GraphId != graph.Id) return;
            if (nodeViews.TryGetValue(e.NodeId, out var view))
            {
                view.SyncPositionFromData();
                // Reroute every edge — cheap, at most all edges.
                EdgeLayer.RefreshAll();
            }
        }

        private void OnNodeAdded(NodeAddedEvent e)
        {
            if (graph == null || e.GraphId != graph.Id) return;
            var node = graph.FindNode(e.NodeId);
            if (node != null && !nodeViews.ContainsKey(node.Id))
            {
                AddNodeView(node);
                EdgeLayer.RefreshAll();
            }
        }

        private void OnNodeRemoved(NodeRemovedEvent e)
        {
            if (graph == null || e.GraphId != graph.Id) return;
            if (nodeViews.TryGetValue(e.NodeId, out var view))
            {
                view.RemoveFromHierarchy();
                nodeViews.Remove(e.NodeId);
                EdgeLayer.RefreshAll();
            }
        }

        // ---------- Edge event handlers ----------

        private void OnEdgeAdded(EdgeAddedEvent e)
        {
            if (graph == null || e.GraphId != graph.Id) return;
            var edge = graph.FindEdge(e.EdgeId);
            if (edge == null) return;
            if (EdgeLayer.Get(e.EdgeId) == null)
            {
                EdgeLayer.AddEdgeView(edge);
                var ev = EdgeLayer.Get(e.EdgeId);
                if (ev != null && !ev.GetManipulatorInstalled())
                {
                    ev.AddManipulator(new WaypointDragManipulator(ev));
                    ev.SetManipulatorInstalled();
                }
            }
        }

        private void OnEdgeRemoved(EdgeRemovedEvent e)
        {
            if (graph == null || e.GraphId != graph.Id) return;
            var ev = EdgeLayer.Get(e.EdgeId);
            if (ev != null)
            {
                // Pull edge + its waypoint selectables out of the selection set.
                if (selection != null)
                {
                    var toDeselect = new List<ISelectable>();
                    if (selection.IsSelected(ev)) toDeselect.Add(ev);
                    foreach (var sel in selection.Snapshot())
                        if (sel is WaypointSelectable wp && ReferenceEquals(wp.EdgeView, ev))
                            toDeselect.Add(wp);
                    foreach (var s in toDeselect) selection.ApplyDirect(ExcludeOne(selection.CurrentSet(), s));
                }
                WaypointSelectable.InvalidateEdge(ev);
            }
            EdgeLayer.RemoveEdgeView(e.EdgeId);
        }

        private void OnEdgeLabelChanged(EdgeLabelChangedEvent e)
        {
            if (graph == null || e.GraphId != graph.Id) return;
            var ev = EdgeLayer.Get(e.EdgeId);
            ev?.RefreshBounds();
        }

        // ---------- Waypoint event handlers ----------

        private void OnWaypointAdded(WaypointAddedEvent e)
        {
            if (graph == null || e.GraphId != graph.Id) return;
            var ev = EdgeLayer.Get(e.EdgeId);
            if (ev == null) return;
            // Waypoint list shifted — invalidate any cached selectables for
            // this edge since indices may have moved.
            WaypointSelectable.InvalidateEdge(ev);
            ev.RefreshBounds();
        }

        private void OnWaypointRemoved(WaypointRemovedEvent e)
        {
            if (graph == null || e.GraphId != graph.Id) return;
            var ev = EdgeLayer.Get(e.EdgeId);
            if (ev == null) return;
            // Deselect any waypoint selectables on this edge before wiping
            // the cache (they no longer map to anything sensible).
            if (selection != null)
            {
                var toDrop = new HashSet<ISelectable>();
                foreach (var sel in selection.Snapshot())
                    if (sel is WaypointSelectable wp && ReferenceEquals(wp.EdgeView, ev))
                        toDrop.Add(wp);
                if (toDrop.Count > 0)
                {
                    var next = new HashSet<ISelectable>(selection.CurrentSet());
                    foreach (var s in toDrop) next.Remove(s);
                    selection.ApplyDirect(next);
                }
            }
            WaypointSelectable.InvalidateEdge(ev);
            ev.RefreshBounds();
        }

        private void OnWaypointMoved(WaypointMovedEvent e)
        {
            if (graph == null || e.GraphId != graph.Id) return;
            var ev = EdgeLayer.Get(e.EdgeId);
            ev?.RefreshBounds();
        }


        public NodeView GetNodeView(string nodeId)
        {
            nodeViews.TryGetValue(nodeId, out var v);
            return v;
        }

        // ---------- Canvas-level input ----------

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
            if (IsFocusInsidePropertyField())
                return;

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

        private bool IsFocusInsidePropertyField()
        {
            var focusedElement = focusController?.focusedElement as VisualElement;
            if (focusedElement == null)
                return false;

            // Walk up the hierarchy looking for any property field type
            while (focusedElement != null)
            {
                if (focusedElement is TextField ||
                    focusedElement is IntegerField ||
                    focusedElement is FloatField ||
                    focusedElement is Toggle ||
                    focusedElement is DropdownField)
                    return true;
                focusedElement = focusedElement.parent;
            }
            return false;
        }



        /// <summary>
        /// Delete everything selected — nodes, edges, and waypoints — as a
        /// single transaction.
        /// </summary>
        public void DeleteSelected()
        {
            if (selection == null || selection.Count == 0) return;

            // Snapshot then group by kind so we can order deletions sensibly
            // (remove waypoints first so their indices are still valid when
            // we hit them, then edges, then nodes — nodes cascade-remove
            // their remaining edges).
            var snapshot = selection.Snapshot();
            var waypoints = new List<WaypointSelectable>();
            var edges = new List<EdgeView>();
            var nodes = new List<NodeView>();
            foreach (var s in snapshot)
            {
                if (s is WaypointSelectable wp) waypoints.Add(wp);
                else if (s is EdgeView ev) edges.Add(ev);
                else if (s is NodeView nv) nodes.Add(nv);
            }

            using (commands.BeginTransaction("Delete selection"))
            {
                selection.Clear();

                // Waypoints first. Sort descending by index within each edge
                // so list indices stay valid as we remove from the end.
                waypoints.Sort((a, b) =>
                {
                    int c = a.EdgeView.Edge.Id.CompareTo(b.EdgeView.Edge.Id);
                    if (c != 0) return c;
                    return b.Index.CompareTo(a.Index);
                });
                foreach (var wp in waypoints)
                    commands.Execute(new RemoveWaypointCmd(graph, bus, wp.EdgeView.Edge.Id, wp.Index));

                foreach (var ev in edges)
                    commands.Execute(new RemoveEdgeCmd(graph, bus, ev.Edge.Id));

                foreach (var nv in nodes)
                    commands.Execute(new RemoveNodeCmd(graph, bus, nv.Node.Id));
            }
        }

        // ---------- tiny helper ----------

        private static HashSet<ISelectable> ExcludeOne(HashSet<ISelectable> set, ISelectable excluded)
        {
            var next = new HashSet<ISelectable>(set);
            next.Remove(excluded);
            return next;
        }
    }

    public sealed class CanvasContextTarget
    {
        public GraphView Canvas { get; }
        public Vector2 WorldPosition { get; }
        public CanvasContextTarget(GraphView canvas, Vector2 worldPos)
        {
            Canvas = canvas; WorldPosition = worldPos;
        }
    }

    /// <summary>
    /// Small marker extension so GraphCanvas can idempotently install the
    /// waypoint manipulator on an EdgeView.
    /// </summary>
    internal static class EdgeViewManipulatorInstall
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EdgeView, object> marks = new();

        public static bool GetManipulatorInstalled(this EdgeView ev)
            => marks.TryGetValue(ev, out _);

        public static void SetManipulatorInstalled(this EdgeView ev)
        {
            if (!marks.TryGetValue(ev, out _)) marks.Add(ev, new object());
        }
    }
}