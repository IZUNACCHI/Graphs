using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;
using NarrativeTool.UI.Graphs;
using System.Collections.Generic;

namespace NarrativeTool.Core.ContextMenu.Providers
{
    public sealed class GraphContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not GraphContextTarget ctx) return null;
            var panel = ctx.Panel;
            var graph = ctx.Graph;

            var items = new List<ContextMenuItem>
            {
                ContextMenuItem.Of("Rename", () => panel.BeginRenameGraph(graph.Id)),
            };

            if (panel.GraphCount > 1)
                items.Add(ContextMenuItem.Of("Delete", () => panel.RemoveGraph(graph.Id)));

            return items;
        }
    }

    public sealed class GraphFolderContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not GraphFolderContextTarget ctx) return null;
            var panel = ctx.Panel;
            var folder = ctx.FolderPath;

            return new List<ContextMenuItem>
            {
                ContextMenuItem.Of("New Graph", () => panel.AddGraph(folder)),
                ContextMenuItem.Of("New Folder", () => panel.AddFolder(folder)),
                ContextMenuItem.Separator(),
                ContextMenuItem.Of("Rename", () => panel.BeginRenameFolder(folder)),
                ContextMenuItem.Of("Delete", () => panel.RemoveFolder(folder)),
            };
        }
    }
}