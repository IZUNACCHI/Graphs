using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Core
{
    /// <summary>
    /// Popup context menu. Click-outside and Escape dismiss.
    /// </summary>
    public sealed class ContextMenuView : VisualElement
    {
        public event Action OnDismissRequested;

        private EventCallback<PointerDownEvent> globalClickHandler;
        private EventCallback<KeyDownEvent> globalKeyHandler;

        public ContextMenuView()
        {
            AddToClassList("nt-ctxmenu");
            style.position = Position.Absolute;
            focusable = true;
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        public void Show(IReadOnlyList<ContextMenuItem> items, Vector2 panelPosition)
        {
            Clear();
            foreach (var it in items)
                Add(BuildRow(it));

            style.left = panelPosition.x;
            style.top = panelPosition.y;
            schedule.Execute(() => ClampToPanel(panelPosition));

            var root = FindRoot();
            if (root != null)
            {
                globalClickHandler = OnGlobalPointerDown;
                root.RegisterCallback(globalClickHandler, TrickleDown.TrickleDown);

                globalKeyHandler = OnGlobalKeyDown;
                root.RegisterCallback(globalKeyHandler, TrickleDown.TrickleDown);
            }

            Focus();
        }

        public void Hide() => OnDismissRequested?.Invoke();

        private VisualElement BuildRow(ContextMenuItem item)
        {
            if (item.IsSeparator)
            {
                var sep = new VisualElement();
                sep.AddToClassList("nt-ctxmenu-separator");
                return sep;
            }

            var row = new Label(item.Label ?? "");
            row.AddToClassList("nt-ctxmenu-item");
            if (!item.Enabled) row.AddToClassList("nt-ctxmenu-item--disabled");

            if (item.Enabled && item.Action != null)
            {
                row.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 0) return;
                    e.StopPropagation();
                    item.Action.Invoke();
                    Hide();
                });
            }
            return row;
        }

        private VisualElement FindRoot()
        {
            VisualElement p = this;
            while (p.parent != null) p = p.parent;
            return p;
        }

        private void OnGlobalPointerDown(PointerDownEvent e)
        {
            if (e.target is VisualElement ve && IsDescendant(ve)) return;
            Hide();
        }

        private void OnGlobalKeyDown(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.Escape) Hide();
        }

        private bool IsDescendant(VisualElement ve)
        {
            while (ve != null) { if (ve == this) return true; ve = ve.parent; }
            return false;
        }

        private void ClampToPanel(Vector2 requested)
        {
            var root = FindRoot();
            if (root == null) return;
            var rootRect = root.worldBound;
            var myRect = worldBound;

            float x = requested.x;
            float y = requested.y;

            if (myRect.xMax > rootRect.xMax) x -= myRect.width;
            if (myRect.yMax > rootRect.yMax) y -= myRect.height;

            x = Mathf.Max(0, x);
            y = Mathf.Max(0, y);

            style.left = x;
            style.top = y;
        }

        private void OnDetach(DetachFromPanelEvent _)
        {
            var root = FindRoot();
            if (root != null)
            {
                if (globalClickHandler != null)
                    root.UnregisterCallback(globalClickHandler, TrickleDown.TrickleDown);
                if (globalKeyHandler != null)
                    root.UnregisterCallback(globalKeyHandler, TrickleDown.TrickleDown);
            }
            globalClickHandler = null;
            globalKeyHandler = null;
        }
    }
}