using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Toolbar
{
    /// <summary>
    /// Three-zone (left / center / right) toolbar that mirrors
    /// <see cref="ToolbarRegistry"/>. Always sits above the dock area.
    /// </summary>
    public sealed class Toolbar : VisualElement
    {
        public Toolbar()
        {
            AddToClassList("nt-toolbar");
            style.flexDirection = FlexDirection.Row;
            style.flexShrink = 0;

            ToolbarRegistry.Changed += Refresh;
            RegisterCallback<DetachFromPanelEvent>(_ => ToolbarRegistry.Changed -= Refresh);

            Refresh();
        }

        public void Refresh()
        {
            Clear();

            var left   = MakeSide("left");
            var spacerL = new VisualElement(); spacerL.style.flexGrow = 1;
            var center = MakeSide("center");
            var spacerR = new VisualElement(); spacerR.style.flexGrow = 1;
            var right  = MakeSide("right");

            Populate(left,   ToolbarSide.Left);
            Populate(center, ToolbarSide.Center);
            Populate(right,  ToolbarSide.Right);

            Add(left); Add(spacerL); Add(center); Add(spacerR); Add(right);
        }

        private static VisualElement MakeSide(string suffix)
        {
            var v = new VisualElement();
            v.AddToClassList("nt-toolbar__side");
            v.AddToClassList("nt-toolbar__side--" + suffix);
            v.style.flexDirection = FlexDirection.Row;
            return v;
        }

        private static void Populate(VisualElement host, ToolbarSide side)
        {
            var ordered = ToolbarRegistry.All
                .Where(x => x.Side == side)
                .OrderBy(x => x.Group ?? string.Empty)
                .ThenBy(x => x.Order)
                .ToList();

            string lastGroup = null;
            foreach (var d in ordered)
            {
                if (d.IsVisible != null && !d.IsVisible()) continue;

                if (lastGroup != null && d.Group != lastGroup)
                {
                    var sep = new VisualElement();
                    sep.AddToClassList("nt-toolbar__separator");
                    host.Add(sep);
                }

                var v = d.Build?.Invoke();
                if (v != null)
                {
                    v.AddToClassList("nt-toolbar__item");
                    host.Add(v);
                }
                lastGroup = d.Group;
            }
        }
    }
}
