using NarrativeTool.Canvas.Views;
using NarrativeTool.Canvas.Manipulators;
using NarrativeTool.Core.Selection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas.Manipulators
{

    public sealed class MarqueeManipulator : Manipulator
    {
        private const float DragThresholdPixels = 4f;
        private readonly GraphView canvas;
        private VisualElement overlay;

        private enum Phase { Idle, Armed, Dragging }
        private Phase phase = Phase.Idle;

        private int capturedPointerId = -1;
        private Vector2 armedScreenPos;   
        private Vector2 currentScreenPos; 
        private bool additive;            

        public MarqueeManipulator(GraphView canvas)
        {
            this.canvas = canvas;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);

            EnsureOverlay();
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);

            overlay?.RemoveFromHierarchy();
            overlay = null;
        }

        private void OnPointerDown(PointerDownEvent e)
        {
            if (e.button != 0) return;
            if (!ReferenceEquals(e.target, canvas)) return;

            phase = Phase.Armed;
            capturedPointerId = e.pointerId;
            armedScreenPos = e.localPosition;
            currentScreenPos = e.localPosition;
            additive = e.shiftKey;

            target.CapturePointer(capturedPointerId);
        }

        private void OnPointerMove(PointerMoveEvent e)
        {
            if (phase == Phase.Idle) return;
            if (!target.HasPointerCapture(capturedPointerId)) return;

            currentScreenPos = e.localPosition;

            if (phase == Phase.Armed)
            {
                var delta = currentScreenPos - armedScreenPos;
                if (delta.sqrMagnitude < DragThresholdPixels * DragThresholdPixels) return;
                phase = Phase.Dragging;
            }

            UpdateOverlay(GetScreenRect());
            e.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (phase == Phase.Idle) return;
            if (e.button != 0) return;

            if (target.HasPointerCapture(capturedPointerId))
                target.ReleasePointer(capturedPointerId);

            bool wasDragging = phase == Phase.Dragging;
            phase = Phase.Idle;
            capturedPointerId = -1;

            HideOverlay();

            if (!wasDragging) return;

            CommitSelection(GetScreenRect());
            e.StopPropagation();
        }

        private void CommitSelection(Rect screenRect)
        {
            var hit = HitTest(screenRect);
            var svc = canvas.Selection;
            if (svc == null) return;

            if (additive)
            {
                var merged = new HashSet<ISelectable>(svc.CurrentSet());
                foreach (var n in hit) merged.Add(n);
                svc.SelectSet(merged);
            }
            else
            {
                svc.SelectSet(hit);
            }
        }

        private List<ISelectable> HitTest(Rect screenRect)
        {
            var result = new List<ISelectable>();

            var worldMin = canvas.ScreenToWorld(new Vector2(screenRect.xMin, screenRect.yMin));
            var worldMax = canvas.ScreenToWorld(new Vector2(screenRect.xMax, screenRect.yMax));
            var worldRect = Rect.MinMaxRect(worldMin.x, worldMin.y, worldMax.x, worldMax.y);

            foreach (var nv in canvas.NodeViews())
            {
                var nodeRect = new Rect(nv.GetVisualPosition(), nv.layout.size);
                if (worldRect.Overlaps(nodeRect))
                    result.Add(nv);
            }

            foreach (var kv in canvas.EdgeLayer.EdgeViews)
            {
                var ev = kv.Value;
                var anchors = ev.GetAnchors();
                if (anchors.Count < 2) continue;

                bool edgeHit = worldRect.Contains(anchors[0]) ||
                               worldRect.Contains(anchors[anchors.Count - 1]);

                for (int i = 0; i < ev.Edge.Waypoints.Count; i++)
                {
                    if (worldRect.Contains(ev.Edge.Waypoints[i].Position))
                    {
                        result.Add(WaypointSelectable.Get(ev, i));
                        edgeHit = true;
                    }
                }

                if (edgeHit)
                    result.Add(ev);
            }

            return result;
        }
        private Rect GetScreenRect()
        {
            return Rect.MinMaxRect(
                Mathf.Min(armedScreenPos.x, currentScreenPos.x),
                Mathf.Min(armedScreenPos.y, currentScreenPos.y),
                Mathf.Max(armedScreenPos.x, currentScreenPos.x),
                Mathf.Max(armedScreenPos.y, currentScreenPos.y));
        }


        //Overlay
        private void EnsureOverlay()
        {
            if (overlay != null) return;

            overlay = new VisualElement { name = "marquee-overlay" };
            overlay.pickingMode = PickingMode.Ignore;
            overlay.style.position = Position.Absolute;
            overlay.style.borderTopWidth = 1f;
            overlay.style.borderBottomWidth = 1f;
            overlay.style.borderLeftWidth = 1f;
            overlay.style.borderRightWidth = 1f;
            overlay.style.borderTopColor = new StyleColor(new Color(0.4f, 0.7f, 1f, 0.9f));
            overlay.style.borderBottomColor = new StyleColor(new Color(0.4f, 0.7f, 1f, 0.9f));
            overlay.style.borderLeftColor = new StyleColor(new Color(0.4f, 0.7f, 1f, 0.9f));
            overlay.style.borderRightColor = new StyleColor(new Color(0.4f, 0.7f, 1f, 0.9f));
            overlay.style.backgroundColor = new StyleColor(new Color(0.4f, 0.7f, 1f, 0.08f));
            overlay.style.display = DisplayStyle.None;
            canvas.Add(overlay);
        }

        private void UpdateOverlay(Rect r)
        {
            if (overlay == null) return;
            overlay.style.display = DisplayStyle.Flex;
            overlay.style.left = r.x;
            overlay.style.top = r.y;
            overlay.style.width = r.width;
            overlay.style.height = r.height;
        }

        private void HideOverlay()
        {
            if (overlay != null)
                overlay.style.display = DisplayStyle.None;
        }
    }
}