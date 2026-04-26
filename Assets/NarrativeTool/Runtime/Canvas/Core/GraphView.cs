using NarrativeTool.Canvas.Manipulators;
using NarrativeTool.Canvas.Views;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas
{
    /// <summary>
    /// Passive presentation layer for a graph: hosts the content/edge layers,
    /// owns the camera and node-view dictionary, and handles canvas-level
    /// input. All graph state and bus coordination lives on
    /// <see cref="GraphController"/>.
    /// </summary>
    [UxmlElement]
    public sealed partial class GraphView : VisualElement
    {
        public VisualElement ContentLayer { get; }
        public EdgeLayer EdgeLayer { get; }
        public PanZoomManipulator Camera { get; }

        private readonly Dictionary<string, NodeView> nodeViews = new();

        public GraphController Controller { get; private set; }

        // Exposed for EdgeLayer.Bind(), which needs the live dictionary.
        public IReadOnlyDictionary<string, NodeView> NodeViews => nodeViews;
        internal Dictionary<string, NodeView> NodeViewsDictionary => nodeViews;

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
            EdgeLayer.pickingMode = PickingMode.Ignore;
            ContentLayer.Add(EdgeLayer);

            Camera = new PanZoomManipulator(this);
            this.AddManipulator(Camera);

            RegisterCallback<PointerDownEvent>(OnCanvasPointerDown);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        internal void AttachController(GraphController controller)
        {
            Controller = controller;
        }

        // ---------- Coord transforms ----------

        public Vector2 ScreenToWorld(Vector2 canvasLocal)
            => (canvasLocal - Camera.PanOffset) / Camera.Zoom;

        public Vector2 WorldToScreen(Vector2 world)
            => world * Camera.Zoom + Camera.PanOffset;

        // ---------- Node-view lifecycle (driven by controller) ----------

        public void ResetNodeViews()
        {
            foreach (var nv in nodeViews.Values) nv.RemoveFromHierarchy();
            nodeViews.Clear();
        }

        public void RegisterNodeView(string nodeId, NodeView view)
        {
            nodeViews[nodeId] = view;
            ContentLayer.Add(view);
        }

        public bool RemoveNodeView(string nodeId)
        {
            if (!nodeViews.TryGetValue(nodeId, out var view)) return false;
            view.RemoveFromHierarchy();
            nodeViews.Remove(nodeId);
            return true;
        }

        public NodeView GetNodeView(string nodeId)
        {
            nodeViews.TryGetValue(nodeId, out var v);
            return v;
        }

        // ---------- Playback hooks ----------

        // Set by the editor host (bootstrap) so the "Start playback here"
        // context-menu item can kick off a run on the playback overlay.
        public System.Action<string> OnStartPlayback;

        /// <summary>
        /// Toggle the .nt-node--playing class on the named node (clearing
        /// it on every other). Called by PlaybackOverlay as it walks the
        /// graph. Pass null to clear all highlights.
        /// </summary>
        public void SetHighlightedNode(string nodeId)
        {
            foreach (var kv in nodeViews)
                kv.Value.EnableInClassList("nt-node--playing", kv.Key == nodeId);
        }

        // ---------- Canvas-level input ----------

        private void OnCanvasPointerDown(PointerDownEvent e)
        {
            Focus();

            if (!ReferenceEquals(e.target, this)) return;

            if (e.button == 0)
            {
                if (!e.shiftKey) Controller?.Selection?.Clear();
            }
            else if (e.button == 1)
            {
                if (Controller?.ContextMenu != null)
                {
                    var worldPos = ScreenToWorld(e.localPosition);
                    Controller.ContextMenu.Open(new CanvasContextTarget(Controller, worldPos), e.position);
                    e.StopPropagation();
                }
            }
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            if (Controller?.Commands == null) return;
            if (IsFocusInsidePropertyField())
                return;

            bool ctrl = e.ctrlKey || e.commandKey;

            if (ctrl && e.keyCode == KeyCode.Z)
            {
                if (e.shiftKey) Controller.Commands.Redo();
                else Controller.Commands.Undo();
                e.StopPropagation();
                return;
            }

            if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
            {
                Controller.DeleteSelected();
                e.StopPropagation();
                return;
            }
        }

        private bool IsFocusInsidePropertyField()
        {
            var focusedElement = focusController?.focusedElement as VisualElement;
            if (focusedElement == null)
                return false;

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
    }

    public sealed class CanvasContextTarget
    {
        public GraphController Controller { get; }
        public Vector2 WorldPosition { get; }
        public CanvasContextTarget(GraphController controller, Vector2 worldPos)
        {
            Controller = controller; WorldPosition = worldPos;
        }
    }

    /// <summary>
    /// Small marker extension so the controller can idempotently install the
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
