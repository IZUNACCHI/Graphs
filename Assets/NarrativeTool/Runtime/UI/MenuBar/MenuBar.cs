using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.MenuBar
{
    /// <summary>
    /// Compact menu bar (File / Edit / Tools / Settings) sitting above the toolbar.
    /// Each top-level button opens a runtime-friendly <see cref="GenericDropdownMenu"/>.
    /// Menus are populated from <see cref="MenuBarRegistry"/>.
    /// </summary>
    public sealed class MenuBar : VisualElement
    {
        // Canonical order: anything else is appended in registration order.
        private static readonly string[] CanonicalOrder = { "File", "Edit", "Tools", "Settings", "Help" };

        public MenuBar()
        {
            AddToClassList("nt-menubar");
            style.flexDirection = FlexDirection.Row;
            style.flexShrink = 0;

            MenuBarRegistry.Changed += Refresh;
            RegisterCallback<DetachFromPanelEvent>(_ => MenuBarRegistry.Changed -= Refresh);

            Refresh();
        }

        public void Refresh()
        {
            Clear();

            var present = MenuBarRegistry.Menus.ToList();
            var ordered = CanonicalOrder.Where(present.Contains)
                                        .Concat(present.Except(CanonicalOrder))
                                        .ToList();

            foreach (var menu in ordered)
            {
                var btn = new Button { text = menu };
                btn.AddToClassList("nt-menubar__item");
                var captured = menu;
                var anchor = btn;
                btn.clicked += () => OpenMenu(captured, anchor);
                Add(btn);
            }
        }

        private static void OpenMenu(string menu, VisualElement anchor)
        {
            var dd = new GenericDropdownMenu();
            var items = MenuBarRegistry.All
                .Where(i => i.Menu == menu)
                .OrderBy(i => i.Order)
                .ToList();

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                bool enabled = it.IsEnabled?.Invoke() ?? true;
                bool isChecked = it.IsChecked?.Invoke() ?? false;
                string label = string.IsNullOrEmpty(it.Shortcut)
                    ? it.Path
                    : it.Path + "    " + it.Shortcut;

                if (enabled)
                    dd.AddItem(label, isChecked, () => it.Action?.Invoke());
                else
                    dd.AddDisabledItem(label, isChecked);

                if (it.IsSeparatorAfter && i < items.Count - 1)
                    dd.AddSeparator(string.Empty);
            }

            // The third argument disambiguates between the two DropDown overloads
            // (Unity 6 added a 4-arg variant). false = use the anchor's bottom-left
            // as the menu's top-left, matching native menu-bar behaviour.
            dd.DropDown(anchor.worldBound, anchor, DropdownMenuSizeMode.Auto);
        }
    }
}
