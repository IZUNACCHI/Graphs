using NarrativeTool.Canvas.Views;
using NarrativeTool.Core.Commands;
using NarrativeTool.Core.Selection;
using System.Collections.Generic;
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
        private readonly Dictionary<NodeView, Vector2> dragGroup = new();
        private readonly Dictionary<WaypointSelectable, Vector2> waypointGroup = new();


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
                BuildDragGroups();
            }

            var delta = CanvasLocalToWorld(e) - dragStartMouseWorld;

            foreach (var kv in dragGroup)
                kv.Key.SetVisualPosition(kv.Value + delta);

            foreach (var kv in waypointGroup)
                kv.Key.EdgeView.Edge.Waypoints[kv.Key.Index].Position = kv.Value + delta;
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

            var canvas = nodeView.Canvas;
            bool anyMoved = false;

            foreach (var kv in dragGroup)
                if (kv.Key.GetVisualPosition() != kv.Value) { anyMoved = true; break; }

            if (!anyMoved)
                foreach (var kv in waypointGroup)
                    if (kv.Key.EdgeView.Edge.Waypoints[kv.Key.Index].Position != kv.Value) { anyMoved = true; break; }

            if (!anyMoved)
            {
                foreach (var kv in dragGroup) kv.Key.SyncPositionFromData();

                foreach (var kv in waypointGroup)
                    kv.Key.EdgeView.Edge.Waypoints[kv.Key.Index].Position = kv.Value;

                canvas.EdgeLayer.MarkDirtyRepaint();
                dragGroup.Clear();
                waypointGroup.Clear();
                e.StopPropagation();
                return;
            }

            using (canvas.Commands.BeginTransaction("Move selection"))
            {
                foreach (var kv in dragGroup)
                {
                    var finalPos = kv.Key.GetVisualPosition();
                    if (finalPos == kv.Value) continue;
                    canvas.Commands.Execute(new MoveNodeCmd(
                        canvas.Graph, canvas.Bus, kv.Key.Node.Id,
                        kv.Value, finalPos));
                }
            }


            foreach (var kv in waypointGroup)
            {
                var wp = kv.Key;
                var finalPos = wp.EdgeView.Edge.Waypoints[wp.Index].Position;
                if (finalPos == kv.Value) continue;
                wp.EdgeView.Edge.Waypoints[wp.Index].Position = kv.Value;
                canvas.Commands.Execute(new MoveWaypointCmd(
                    canvas.Graph, canvas.Bus, wp.EdgeView.Edge.Id,
                    wp.Index, kv.Value, finalPos));
            }



            dragGroup.Clear();
            waypointGroup.Clear();
            e.StopPropagation();
        }
        private void BuildDragGroups()
        {
            dragGroup.Clear();
            waypointGroup.Clear();

            var canvas = nodeView.Canvas;
            var selection = canvas.Selection;

            if (selection != null && selection.IsSelected(nodeView) && selection.Count > 1)
            {
                foreach (var selectable in selection.Selected)
                {
                    if (selectable is NodeView nv)
                        dragGroup[nv] = nv.Node.Position;
                    else if (selectable is WaypointSelectable wp)
                        waypointGroup[wp] = wp.EdgeView.Edge.Waypoints[wp.Index].Position;
                }

            }

            // Always include the dragged node (covers unselected drag too).
            if (!dragGroup.ContainsKey(nodeView))
                dragGroup[nodeView] = nodeView.Node.Position;
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