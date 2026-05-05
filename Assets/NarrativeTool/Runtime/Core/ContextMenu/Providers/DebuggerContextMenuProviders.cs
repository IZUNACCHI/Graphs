using NarrativeTool.UI.Debugger;
using System.Collections.Generic;

namespace NarrativeTool.Core.ContextMenu.Providers
{
    /// <summary>Right-click on a Watch row → Set runtime value / Remove from watch.</summary>
    public sealed class WatchContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not WatchContextTarget ctx) return null;
            var tab = ctx.Tab;
            var t = ctx.Target;
            return new List<ContextMenuItem>
            {
                ContextMenuItem.Of("Set runtime value…", () => tab.BeginEditValue(t)),
                ContextMenuItem.Separator(),
                ContextMenuItem.Of("Remove from watch", () => tab.RemoveWatch(t)),
            };
        }
    }

    /// <summary>Routes the "+ watch" picker target to the actual menu items it carries.</summary>
    public sealed class WatchPickerContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not WatchPickerTarget ctx) return null;
            return ctx.Items;
        }
    }

    /// <summary>Right-click on a Breakpoints row → Disable / Enable / Remove.</summary>
    public sealed class BreakpointContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not BreakpointContextTarget ctx) return null;
            var tab = ctx.Tab;
            var key = ctx.Key;
            return new List<ContextMenuItem>
            {
                ContextMenuItem.Of(
                    ctx.Enabled ? "Disable breakpoint" : "Enable breakpoint",
                    () => tab.Toggle(key)),
                ContextMenuItem.Separator(),
                ContextMenuItem.Of("Remove breakpoint", () => tab.Remove(key)),
            };
        }
    }
}
