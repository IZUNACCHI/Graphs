using System.Collections.Generic;
using NarrativeTool.Core;
using NarrativeTool.Data;
using NarrativeTool.Data.Commands;
using UnityEngine;

namespace NarrativeTool.Canvas
{
    public sealed class CanvasContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not CanvasContextTarget ctx) return null;
            var canvas = ctx.Canvas;
            var pos = ctx.WorldPosition;

            return new List<ContextMenuItem>
            {
                ContextMenuItem.Of("New Start Node", () => CreateStart(canvas, pos)),
                ContextMenuItem.Of("New Text Node",  () => CreateText (canvas, pos)),
                ContextMenuItem.Of("New End Node",   () => CreateEnd  (canvas, pos)),
            };
        }

        private static void CreateStart(GraphCanvas c, Vector2 pos)
        {
            var node = new StartNode(NewId("start"), pos);
            Create(c, node);
        }

        private static void CreateText(GraphCanvas c, Vector2 pos)
        {
            var node = new TextNode(NewId("text"), "Text Node", pos, "");
            Create(c, node);
        }

        private static void CreateEnd(GraphCanvas c, Vector2 pos)
        {
            var node = new EndNode(NewId("end"), pos);
            Create(c, node);
        }

        private static void Create(GraphCanvas c, Node node)
        {
            c.Commands.Execute(new AddNodeCmd(c.Graph, c.Bus, node));
            var view = c.GetNodeView(node.Id);
            if (view != null) c.Selection.SelectOnly(view);
        }

        private static string NewId(string prefix)
            => $"{prefix}_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";
    }

    public sealed class NodeContextMenuProvider : IContextMenuProvider
    {
        public IReadOnlyList<ContextMenuItem> GetItemsFor(object target)
        {
            if (target is not NodeContextTarget ctx) return null;
            var nv = ctx.NodeView;
            var canvas = nv.Canvas;
            int selectedCount = canvas.Selection.Count;

            string label = selectedCount > 1
                ? $"Delete {selectedCount} Items"
                : "Delete";

            return new List<ContextMenuItem>
            {
                ContextMenuItem.Of(label, canvas.DeleteSelected),
            };
        }
    }
}