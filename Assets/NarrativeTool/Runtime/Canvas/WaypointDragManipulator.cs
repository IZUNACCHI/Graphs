using NarrativeTool.Core;
using NarrativeTool.Data;
using NarrativeTool.Data.Commands;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas
{
    /// <summary>
    /// Attached to an EdgeView. Tracks left-drag on a waypoint. 4-pixel
    /// distance threshold before entering drag mode (so a click is still a
    /// click). On pointer-up, commits one MoveWaypointCmd — the command's
    /// TryMerge collapses bursts of sub-pixel moves into a single undo step.
    /// </summary>
    public sealed class WaypointDragManipulator : Manipulator
    {
        private const float DragThresholdPixels = 4f;

        private readonly EdgeView edgeView;

        private enum Phase { Idle, Armed, Dragging }
        private Phase phase = Phase.Idle;

        private int waypointIndex = -1;
        private Vector2 armedPointerScreenStart;
        private Vector2 dragStartMouseWorld;
        private Vector2 dragStartWaypointPos;

        public WaypointDragManipulator(EdgeView ev) { edgeView = ev; }

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

            var worldPoint = ScreenToWorld(e);
            int hitIndex = edgeView.WaypointIndexUnderPoint(worldPoint);
            if (hitIndex < 0) return;

            waypointIndex = hitIndex;
            phase = Phase.Armed;
            armedPointerScreenStart = e.position;
            target.CapturePointer(e.pointerId);
            // Don't stop propagation — EdgeView.OnPointerDown still needs to
            // run for selection. (It will see the same event, select the
            // waypoint, and we proceed independently.)
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
                dragStartMouseWorld = ScreenToWorld(e);
                if (waypointIndex < 0 || waypointIndex >= edgeView.Edge.Waypoints.Count)
                {
                    phase = Phase.Idle;
                    return;
                }
                dragStartWaypointPos = edgeView.Edge.Waypoints[waypointIndex].Position;
            }

            var currentWorld = ScreenToWorld(e);
            var delta = currentWorld - dragStartMouseWorld;
            var newPos = dragStartWaypointPos + delta;

            // Update data directly during drag (no command yet).
            if (waypointIndex >= 0 && waypointIndex < edgeView.Edge.Waypoints.Count)
            {
                edgeView.Edge.Waypoints[waypointIndex].Position = newPos;
                edgeView.RefreshBounds();
            }
            e.StopPropagation();
        }

        private void OnUp(PointerUpEvent e)
        {
            if (phase == Phase.Idle) return;
            if (e.button != 0) return;
            if (target.HasPointerCapture(e.pointerId)) target.ReleasePointer(e.pointerId);

            bool wasDragging = phase == Phase.Dragging;
            int index = waypointIndex;

            phase = Phase.Idle;
            waypointIndex = -1;

            if (!wasDragging) return;
            if (index < 0 || index >= edgeView.Edge.Waypoints.Count) return;

            var finalPos = edgeView.Edge.Waypoints[index].Position;
            if (finalPos == dragStartWaypointPos) return;

            // Commit as a single command, going from original to final.
            // We bypass the live updates: revert the data to its pre-drag
            // state, then the command's Do() applies the final value and
            // fires the event cleanly.
            edgeView.Edge.Waypoints[index].Position = dragStartWaypointPos;
            edgeView.Canvas.Commands.Execute(new MoveWaypointCmd(
                edgeView.Canvas.Graph, edgeView.Canvas.Bus, edgeView.Edge.Id,
                index, dragStartWaypointPos, finalPos));
            e.StopPropagation();
        }

        private Vector2 ScreenToWorld(IPointerEvent e)
        {
            var canvas = edgeView.Canvas;
            var canvasLocal = (Vector2)e.position;
            var panelOrigin = canvas.parent != null
                ? canvas.parent.LocalToWorld(canvas.layout.position)
                : Vector2.zero;
            canvasLocal -= panelOrigin;
            return canvas.ScreenToWorld(canvasLocal);
        }
    }
}