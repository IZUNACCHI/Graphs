using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Project;
using NarrativeTool.UI.Docking;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas.Core
{
    /// <summary>
    /// One open graph as a dockable panel pinned to the center zone. Wraps a
    /// <see cref="GraphView"/> and tracks per-graph dirty state. Replaces the
    /// old <c>GraphTab</c> class, which was a thin wrapper used only by the
    /// also-deleted <c>GraphTabManager</c>.
    /// </summary>
    public sealed class GraphPanel : IDockablePanel
    {
        public string Id { get; }
        public string Title => Lazy?.Name ?? "(graph)";
        public VisualElement Content => view;
        public bool IsCloseable => true;
        public bool IsPinnedCenter => true;

        public LazyGraph Lazy { get; }
        public GraphData Graph { get; private set; }
        public GraphView View => view;
        public bool IsDirty { get; private set; }

        private readonly GraphView view;
        private readonly SessionState session;

        public GraphPanel(LazyGraph lazy, SessionState session, ContextMenuController contextMenu)
        {
            Lazy = lazy;
            this.session = session;
            Id = "graph:" + lazy.Id;

            Graph = lazy.GetGraph();
            // Older saves may not have GraphData.Id populated — Lazy.Id is the
            // authoritative reference.
            if (Graph != null && string.IsNullOrEmpty(Graph.Id)) Graph.Id = lazy.Id;

            view = new GraphView();
            view.style.flexGrow = 1;
            view.Bind(Graph, session, contextMenu);

            session.CommandsFor(Graph).OnHistoryChanged += MarkDirty;
        }

        public void Save()
        {
            Lazy.Update(Graph);
            IsDirty = false;
        }

        public void Dispose()
        {
            if (view != null)
            {
                view.RemoveFromHierarchy();
                if (Graph != null) session.CommandsFor(Graph).OnHistoryChanged -= MarkDirty;
            }
            Graph = null;
        }

        private void MarkDirty() => IsDirty = true;
    }
}
