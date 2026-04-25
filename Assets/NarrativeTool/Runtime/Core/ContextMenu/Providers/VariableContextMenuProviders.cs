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
                ContextMenuItem.Of("Rename", () => panel.BeginRenameVariable(v.Id)),
                ContextMenuItem.Of("Delete", () => panel.RemoveVariable(v.Id)),
            };
        }
    }

    /// <summary>
    /// Right-click on a folder header (or empty list area when FolderPath is "")
    /// → add variable here, add a folder, and on a real folder rename / delete.
    /// </summary>
    public sealed class VariableFolderContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not VariableFolderContextTarget ctx) return null;
            var panel = ctx.Panel;
            string folder = ctx.FolderPath;
            bool isRoot = string.IsNullOrEmpty(folder);

            string addVarLabel = isRoot
                ? "Add variable"
                : $"Add variable in '{folder}'";

            string addFolderLabel = isRoot
                ? "Add folder"
                : $"Add folder in '{folder}'";

            var items = new List<ContextMenuItem>
            {
                ContextMenuItem.Of(addVarLabel, () => panel.AddVariable(folder)),
                ContextMenuItem.Of(addFolderLabel, () => panel.AddFolder(folder)),
            };

            if (!isRoot)
            {
                items.Add(ContextMenuItem.Separator());
                items.Add(ContextMenuItem.Of("Rename folder", () => panel.BeginRenameFolder(folder)));
                items.Add(ContextMenuItem.Of("Delete folder", () => panel.RemoveFolder(folder)));
            }

            return items;
        }
    }
}
