using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Docking
{
    /// <summary>
    /// Translucent overlay drawn over the candidate <see cref="DockArea"/> while a
    /// tab is being dragged. Highlights the half/quadrant the drop will land on.
    /// Lives as an absolutely-positioned child of <see cref="DockRoot"/>.
    /// </summary>
    public sealed class DockDropOverlay : VisualElement
    {
        public DockDropOverlay()
        {
            AddToClassList("nt-dock-overlay");
            style.position = Position.Absolute;
            pickingMode = PickingMode.Ignore;
            style.display = DisplayStyle.None;
            style.borderLeftWidth = 1;
            style.borderRightWidth = 1;
            style.borderTopWidth = 1;
            style.borderBottomWidth = 1;
        }

        public void ShowOver(VisualElement host, Rect targetWorldBounds, DropSide side)
        {
            // Convert target's world rect into host's local coordinates.
            Vector2 topLeft = host.WorldToLocal(new Vector2(targetWorldBounds.xMin, targetWorldBounds.yMin));
            Vector2 size = new Vector2(targetWorldBounds.width, targetWorldBounds.height);

            Rect r = ComputeDropRect(new Rect(topLeft, size), side);
            style.left = r.x;
            style.top = r.y;
            style.width = r.width;
            style.height = r.height;
            style.display = DisplayStyle.Flex;
        }

        public void Hide() => style.display = DisplayStyle.None;

        private static Rect ComputeDropRect(Rect a, DropSide side) => side switch
        {
            DropSide.Center => a,
            DropSide.Left   => new Rect(a.x, a.y, a.width / 2f, a.height),
            DropSide.Right  => new Rect(a.x + a.width / 2f, a.y, a.width / 2f, a.height),
            DropSide.Top    => new Rect(a.x, a.y, a.width, a.height / 2f),
            DropSide.Bottom => new Rect(a.x, a.y + a.height / 2f, a.width, a.height / 2f),
            _               => a,
        };
    }
}
