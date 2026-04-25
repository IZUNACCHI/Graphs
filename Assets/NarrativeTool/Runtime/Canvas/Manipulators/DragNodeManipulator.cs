using NarrativeTool.Canvas.Views;
using NarrativeTool.Core.Commands;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas.Manipulators
{
    /// <summary>
    /// Drag a NodeView by its header. 4-pixel distance threshold gates
    /// drag start so bare clicks don't become drags. Shift held at
    /// pointer-down suppresses drag entirely (selection-only gesture).
    /// </summary>
    public sealed class DragNodeManipulator : Manipulator
    {
        private const float DragThresholdPixels = 4f;

        private readonly NodeView nodeView;

        private enum Phase { Idle, Armed, Dragging }
        private Phase phase = Phase.Idle;

        private Vector2 armedPointerScreenStart;
        private Vector2 dragStartMouseWorld;
        private Vector2 dragStartNodePosition;

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
            if (e.button != 0) return;
            if (e.shiftKey) return;

            phase = Phase.Armed;
            armedPointerScreenStart = e.position;
            target.CapturePointer(e.pointerId);
        }

        private void OnMove(PointerMoveEvent e)
        {
            if (phase == Phase.Idle) return;
            if (!target.HasPointerCapture(e.pointerId)) return;

            if (phase == Phase.Armed)
            {
                var screenDelta = (Vector2)e.position - armedPointerScreenStart;
                if (screenDelta.sqrMagnitude < DragThresholdPixels * DragThresholdPixels) return;

                phase = Phase.Dragging;
                dragStartMouseWorld = CanvasLocalToWorld(e);
                dragStartNodePosition = nodeView.Node.Position;
            }

            var currentWorld = CanvasLocalToWorld(e);
            var delta = currentWorld - dragStartMouseWorld;
            var newVisualPos = dragStartNodePosition + delta;

            nodeView.SetVisualPosition(newVisualPos);
            nodeView.Canvas.EdgeLayer.RefreshAll();
            e.StopPropagation();
        }

        private void OnUp(PointerUpEvent e)
        {
            if (phase == Phase.Idle) return;
            if (e.button != 0) return;
            if (target.HasPointerCapture(e.pointerId)) target.ReleasePointer(e.pointerId);

            bool wasDragging = phase == Phase.Dragging;
            phase = Phase.Idle;

            if (!wasDragging) return;

            var finalPos = nodeView.GetVisualPosition();
            if (finalPos == dragStartNodePosition)
            {
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

        private Vector2 CanvasLocalToWorld(IPointerEvent e)
        {
            var canvasLocal = (Vector2)e.position;
            var canvas = nodeView.Canvas;
            var panelOrigin = canvas.parent != null
                ? canvas.parent.LocalToWorld(canvas.layout.position)
                : Vector2.zero;
            canvasLocal -= panelOrigin;
            return canvas.ScreenToWorld(canvasLocal);
        }
    }
}