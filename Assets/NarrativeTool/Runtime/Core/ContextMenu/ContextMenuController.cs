using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Core.ContextMenu
{
    /// <summary>
    /// Central service that owns the currently-open context menu view and
    /// the set of registered IContextMenuProviders.
    /// </summary>
    public sealed class ContextMenuController
    {
        private readonly List<IContextMenuProvider> providers = new();
        private ContextMenuView activeView;
        private VisualElement rootHost;

        public void SetRootHost(VisualElement root) { rootHost = root; }

        public void RegisterProvider(IContextMenuProvider p)
        {
            if (p != null && !providers.Contains(p)) providers.Add(p);
        }

        public void UnregisterProvider(IContextMenuProvider p)
        {
            providers.Remove(p);
        }

        public void Open(object target, Vector2 panelPosition)
        {
            Close();

            if (rootHost == null)
            {
                Debug.LogWarning("[ContextMenu] No root host set. Call SetRootHost() at bootstrap.");
                return;
            }

            var items = CollectItems(target);
            if (items.Count == 0) return;

            activeView = new ContextMenuView();
            activeView.OnDismissRequested += Close;
            rootHost.Add(activeView);
            activeView.Show(items, panelPosition);
        }

        public void Close()
        {
            if (activeView == null) return;
            activeView.OnDismissRequested -= Close;
            activeView.RemoveFromHierarchy();
            activeView = null;
        }

        private List<ContextMenuItem> CollectItems(object target)
        {
            var result = new List<ContextMenuItem>();
            bool firstGroup = true;
            foreach (var provider in providers)
            {
                var items = provider.GetItemsFor(target);
                if (items == null || items.Count == 0) continue;
                if (!firstGroup) result.Add(ContextMenuItem.Separator());
                result.AddRange(items);
                firstGroup = false;
            }
            return result;
        }
    }
}