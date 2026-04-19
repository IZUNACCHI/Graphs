// ===== File: Assets/NarrativeTool/Runtime/Canvas/DragNodeManipulator.cs =====
using NarrativeTool.Core;
using NarrativeTool.Data;
using NarrativeTool.Data.Commands;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas
{
    /// <summary>
    /// Drags a NodeView by its header. While dragging, only the view's visual
    /// position changes — no model mutation, no commands, no event bus
    /// traffic. On pointer-up, a single MoveNodeCmd records the full delta
    /// as one undo entry.
    ///
    /// Attach to the node's header element (not the whole node), so clicks on
    /// ports, body, or a text field don't start a drag.
    /// </summary>
    public sealed class DragNodeManipulator : Manipulator
    {
        private readonly NodeView nodeView;

        private bool dragging;
        private Vector2 dragStartMouseWorld;   // world-space pointer pos at drag start
        private Vector2 dragStartNodePosition; // node world position at drag start

        public DragNodeManipulator(NodeView nv) { nodeView = nv; }

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

        private void OnDown(PointerDownEvent e)
        {
            if (e.button != 0) return; // left mouse only

            dragging = true;
            dragStartMouseWorld = CanvasLocalToWorld(e);
            dragStartNodePosition = nodeView.Node.Position;
            target.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        private void OnMove(PointerMoveEvent e)
        {
            if (!dragging) return;
            if (!target.HasPointerCapture(e.pointerId)) return;

            var currentWorld = CanvasLocalToWorld(e);
            var delta = currentWorld - dragStartMouseWorld;
            var newVisualPos = dragStartNodePosition + delta;

            // Visual-only update — model untouched until pointer up.
            nodeView.SetVisualPosition(newVisualPos);
            nodeView.Canvas.EdgeLayer.MarkDirtyRepaint();
            e.StopPropagation();
        }

        private void OnUp(PointerUpEvent e)
        {
            if (!dragging) return;
            if (e.button != 0) return;
            if (target.HasPointerCapture(e.pointerId)) target.ReleasePointer(e.pointerId);
            dragging = false;

            var finalPos = nodeView.GetVisualPosition();
            if (finalPos == dragStartNodePosition)
            {
                // No movement — nothing to record. Sync view just in case of
                // float jitter.
                nodeView.SyncPositionFromData();
                nodeView.Canvas.EdgeLayer.MarkDirtyRepaint();
                e.StopPropagation();
                return;
            }

            var canvas = nodeView.Canvas;
            canvas.Commands.Execute(new MoveNodeCmd(
                canvas.Graph, canvas.Bus, nodeView.Node.Id,
                dragStartNodePosition, finalPos));
            e.StopPropagation();
        }

        /// <summary>
        /// Convert a pointer event's position into world (content-layer) coords.
        /// e.position is panel-local; when the canvas fills the panel that also
        /// equals canvas-local. If the canvas were offset inside the panel we'd
        /// need WorldToLocal here, but the current layout makes that unnecessary.
        /// </summary>
        private Vector2 CanvasLocalToWorld(IPointerEvent e)
        {
            var canvasLocal = (Vector2)e.position;
            // If the canvas is not at the panel origin, convert:
            var canvas = nodeView.Canvas;
            var panelOrigin = canvas.parent != null
                ? canvas.parent.LocalToWorld(canvas.layout.position)
                : Vector2.zero;
            canvasLocal -= panelOrigin;
            return canvas.ScreenToWorld(canvasLocal);
        }
    }
}