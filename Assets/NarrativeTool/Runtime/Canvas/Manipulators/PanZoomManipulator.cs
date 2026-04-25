using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas.Manipulators
{
    /// <summary>
    /// Middle-mouse pan, wheel zoom. Writes transform to
    /// canvas.ContentLayer. Pan delta is in screen space (no /zoom);
    /// transformOrigin (0,0) keeps scale-then-translate math simple.
    /// </summary>
    public sealed class PanZoomManipulator : Manipulator
    {
        private readonly GraphView canvas;

        private Vector2 panOffset = Vector2.zero;
        private float zoom = 1f;

        private bool panning;
        private Vector2 panStartCanvas;
        private Vector2 panStartOffset;

        private const float MIN_ZOOM = 0.2f;
        private const float MAX_ZOOM = 3f;
        private const float WHEEL_SENSITIVITY = 0.05f;

        public Vector2 PanOffset => panOffset;
        public float Zoom => zoom;

        public PanZoomManipulator(GraphView canvas) { this.canvas = canvas; }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<WheelEvent>(OnWheel);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<WheelEvent>(OnWheel);
        }

        private void OnPointerDown(PointerDownEvent e)
        {
            if (e.button != 2) return;
            panning = true;
            panStartCanvas = e.localPosition;
            panStartOffset = panOffset;
            target.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent e)
        {
            if (!panning) return;
            if (!target.HasPointerCapture(e.pointerId)) return;

            var delta = (Vector2)e.localPosition - panStartCanvas;
            panOffset = panStartOffset + delta;
            ApplyTransform();
            e.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (!panning) return;
            if (e.button != 2) return;
            if (target.HasPointerCapture(e.pointerId)) target.ReleasePointer(e.pointerId);
            panning = false;
            e.StopPropagation();
        }

        private void OnWheel(WheelEvent e)
        {
            var oldZoom = zoom;
            var newZoom = Mathf.Clamp(zoom - e.delta.y * WHEEL_SENSITIVITY, MIN_ZOOM, MAX_ZOOM);
            if (Mathf.Approximately(newZoom, oldZoom)) { e.StopPropagation(); return; }

            var cursor = (Vector2)e.localMousePosition;
            zoom = newZoom;
            panOffset = cursor - (cursor - panOffset) * (zoom / oldZoom);
            ApplyTransform();
            e.StopPropagation();
        }

        private void ApplyTransform()
        {
            canvas.ContentLayer.style.translate = new Translate(panOffset.x, panOffset.y, 0f);
            canvas.ContentLayer.style.scale = new Scale(new Vector3(zoom, zoom, 1f));
            canvas.EdgeLayer.MarkDirtyRepaint();
        }
    }
}