// ===== File: Assets/NarrativeTool/Runtime/Canvas/PanZoomManipulator.cs =====
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas
{
    /// <summary>
    /// Middle-mouse-drag to pan, scroll wheel to zoom around the cursor.
    /// Writes the transform to <see cref="GraphCanvas.ContentLayer"/>.
    ///
    /// Math ported from the working CameraController: translate is applied
    /// pre-scale, so screen-space pan delta is divided by zoom; zoom-around-
    /// cursor keeps the world point under the cursor fixed.
    /// </summary>
    public sealed class PanZoomManipulator : Manipulator
    {
        private readonly GraphCanvas canvas;

        private Vector2 panOffset = Vector2.zero;
        private float zoom = 1f;

        private bool panning;
        private Vector2 panStartCanvas;   // pointer position in canvas-local space
        private Vector2 panStartOffset;   // panOffset when pan began

        private const float MIN_ZOOM = 0.2f;
        private const float MAX_ZOOM = 3f;
        private const float WHEEL_SENSITIVITY = 0.05f;

        public Vector2 PanOffset => panOffset;
        public float Zoom => zoom;

        public PanZoomManipulator(GraphCanvas canvas)
        {
            this.canvas = canvas;
        }

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
            if (e.button != 2) return; // middle mouse
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
            panOffset = panStartOffset + delta;     // <-- changed: no "/ zoom"
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

            // Keep the world point under the cursor fixed. Derivation:
            //   worldAtCursor = (cursor - panOffset) / oldZoom
            // After zoom we want (cursor - newPanOffset) / newZoom == worldAtCursor
            //   newPanOffset = cursor - worldAtCursor * newZoom
            //                = cursor - (cursor - panOffset) * (newZoom / oldZoom)
            var cursor = (Vector2)e.localMousePosition;
            zoom = newZoom;
            panOffset = cursor - (cursor - panOffset) * (zoom / oldZoom);
            ApplyTransform();
            e.StopPropagation();
        }

        private void ApplyTransform()
        {
            // Translate with explicit float args → implicit Length(pixels). Same
            // approach as the working reference. Avoid the 2-arg constructor —
            // it defaults to percent units and causes drift.
            canvas.ContentLayer.style.translate = new Translate(panOffset.x, panOffset.y, 0f);
            canvas.ContentLayer.style.scale = new Scale(new Vector3(zoom, zoom, 1f));
            canvas.EdgeLayer.MarkDirtyRepaint();
        }
    }
}