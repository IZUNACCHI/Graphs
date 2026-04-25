using NarrativeTool.UI.Variables;
using System.Collections.Generic;

namespace NarrativeTool.Core.ContextMenu
{
    /// <summary>
    /// Right-click on a variable row → Rename / Delete.
    /// </summary>
    public sealed class VariableContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not VariableContextTarget ctx) return null;
            var panel = ctx.Panel;
            var v = ctx.Variable;

            return new List<ContextMenuItem>
            {
                ContextMenuItem.Of("Rename", () => panel.BeginRename(v.Id)),
                ContextMenuItem.Of("Delete", () => panel.RemoveVariable(v.Id)),
            };
        }
    }

    /// <summary>
    /// Right-click on a folder header or empty list area → Add variable here.
    /// </summary>
    public sealed class VariableFolderContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not VariableFolderContextTarget ctx) return null;
            var panel = ctx.Panel;
            string folder = ctx.FolderPath;

            string label = string.IsNullOrEmpty(folder)
                ? "Add variable"
                : $"Add variable in '{folder}'";

            return new List<ContextMenuItem>
            {
                ContextMenuItem.Of(label, () => panel.AddVariable(folder)),
            };
        }
    }
}
