using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Widgets
{
    public sealed class ModalConfirm : VisualElement
    {
        private readonly Action onConfirm;
        private readonly Action onCancel;

        /// <summary>
        /// Full‑panel modal confirmation dialog. Clicks on the background dismiss it (Cancel).
        /// </summary>
        /// <param name="message">e.g. "Delete variable 'reputation'?"</param>
        /// <param name="confirmLabel">e.g. "Delete"</param>
        /// <param name="onConfirm">Action when user clicks confirm</param>
        /// <param name="onCancel">Action when user cancels (can be null)</param>
        public ModalConfirm(string message, string confirmLabel, Action onConfirm, Action onCancel = null)
        {
            this.onConfirm = onConfirm;
            this.onCancel = onCancel;

            AddToClassList("nt-modal-overlay");
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            style.backgroundColor = new Color(0, 0, 0, 0.5f);
            style.justifyContent = Justify.Center;
            style.alignItems = Align.Center;

            // Clicking on the background (the overlay itself) cancels
            RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.target == this)   // only if the hit was directly on the background
                    Cancel();
            });

            // Escape key also cancels
            RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Escape)
                {
                    Cancel();
                    e.StopPropagation();
                }
            });

            // The inner box that contains the message and buttons
            var box = new VisualElement();
            box.AddToClassList("nt-modal-box");
            box.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            box.style.borderTopLeftRadius = 6;
            box.style.borderTopRightRadius = 6;
            box.style.borderBottomLeftRadius = 6;
            box.style.borderBottomRightRadius = 6;
            box.style.paddingTop = 16;
            box.style.paddingBottom = 16;
            box.style.paddingLeft = 20;
            box.style.paddingRight = 20;
            box.style.minWidth = 240;

            // Stop clicks inside the box from reaching the background
            box.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());

            var label = new Label(message);
            label.AddToClassList("nt-modal-message");
            label.style.color = Color.white;
            label.style.fontSize = 12;
            label.style.whiteSpace = WhiteSpace.Normal;
            box.Add(label);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 12;

            var cancelBtn = new Button(() => Cancel()) { text = "Cancel" };
            cancelBtn.AddToClassList("nt-btn");
            cancelBtn.AddToClassList("nt-btn--normal");
            buttons.Add(cancelBtn);

            var confirmBtn = new Button(() => Confirm()) { text = confirmLabel };
            confirmBtn.AddToClassList("nt-btn");
            confirmBtn.AddToClassList("nt-btn--danger");
            buttons.Add(confirmBtn);

            box.Add(buttons);
            Add(box);

            // Grab focus so the overlay can receive keyboard events
            focusable = true;
            schedule.Execute(() => Focus()).StartingIn(0);
        }

        private void Confirm()
        {
            onConfirm?.Invoke();
            RemoveFromHierarchy();
        }

        private void Cancel()
        {
            onCancel?.Invoke();
            RemoveFromHierarchy();
        }
    }
}