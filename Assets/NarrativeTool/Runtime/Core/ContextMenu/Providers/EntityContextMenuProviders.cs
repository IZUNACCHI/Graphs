using NarrativeTool.UI.Entities;
using System.Collections.Generic;

namespace NarrativeTool.Core.ContextMenu
{
    public sealed class EntityContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not EntityContextTarget ctx) return null;
            var p = ctx.Panel; var e = ctx.Entity;
            return new List<ContextMenuItem>
            {
                ContextMenuItem.Of("Rename", () => p.BeginRenameEntity(e.Id)),
                ContextMenuItem.Of("Delete", () => p.RemoveEntity(e.Id)),
            };
        }
    }

    public sealed class EntityFolderContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not EntityFolderContextTarget ctx) return null;
            var p = ctx.Panel; string f = ctx.FolderPath;
            bool isRoot = string.IsNullOrEmpty(f);

            string addEntityLabel = isRoot ? "Add entity" : $"Add entity in '{f}'";
            string addFolderLabel = isRoot ? "Add folder" : $"Add folder in '{f}'";

            var items = new List<ContextMenuItem>
            {
                ContextMenuItem.Of(addEntityLabel, () => p.AddEntity(f)),
                ContextMenuItem.Of(addFolderLabel, () => p.AddEntityFolder(f)),
            };
            if (!isRoot)
            {
                items.Add(ContextMenuItem.Separator());
                items.Add(ContextMenuItem.Of("Rename folder", () => p.BeginRenameEntityFolder(f)));
                items.Add(ContextMenuItem.Of("Delete folder", () => p.RemoveEntityFolder(f)));
            }
            return items;
        }
    }

    public sealed class EnumDefContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not EnumContextTarget ctx) return null;
            var p = ctx.Panel; var e = ctx.Enum;
            return new List<ContextMenuItem>
            {
                ContextMenuItem.Of("Rename", () => p.BeginRenameEnum(e.Id)),
                ContextMenuItem.Of("Delete", () => p.RemoveEnum(e.Id)),
            };
        }
    }

    public sealed class EnumFolderContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not EnumFolderContextTarget ctx) return null;
            var p = ctx.Panel; string f = ctx.FolderPath;
            bool isRoot = string.IsNullOrEmpty(f);

            string addEnumLabel = isRoot ? "Add enum" : $"Add enum in '{f}'";
            string addFolderLabel = isRoot ? "Add folder" : $"Add folder in '{f}'";

            var items = new List<ContextMenuItem>
            {
                ContextMenuItem.Of(addEnumLabel, () => p.AddEnum(f)),
                ContextMenuItem.Of(addFolderLabel, () => p.AddEnumFolder(f)),
            };
            if (!isRoot)
            {
                items.Add(ContextMenuItem.Separator());
                items.Add(ContextMenuItem.Of("Rename folder", () => p.BeginRenameEnumFolder(f)));
                items.Add(ContextMenuItem.Of("Delete folder", () => p.RemoveEnumFolder(f)));
            }
            return items;
        }
    }
}
