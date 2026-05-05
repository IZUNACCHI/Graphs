using NarrativeTool.Canvas.Views;
using NarrativeTool.Core.Commands;
using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Project;

namespace NarrativeTool.Canvas.Core
{
    public sealed class GraphTab
    {
        public GraphData Graph { get; private set; }
        public GraphView View { get; private set; }
        public LazyGraph Lazy { get; }
        public bool IsDirty { get; private set; }

        private readonly SessionState session;
        private readonly ContextMenuController contextMenu;

        public GraphTab(LazyGraph lazy, SessionState session, ContextMenuController contextMenu)
        {
            Lazy = lazy;
            this.session = session;
            this.contextMenu = contextMenu;

            Graph = lazy.GetGraph();
            // GraphData.Id may not survive JSON deserialisation in older
            // saves; LazyGraph.Id is the authoritative reference.
            if (Graph != null && string.IsNullOrEmpty(Graph.Id)) Graph.Id = lazy.Id;
            View = new GraphView();
            View.Bind(Graph, session, contextMenu);

            // Mark dirty on any command execution for this graph
            session.CommandsFor(Graph).OnHistoryChanged += MarkDirty;
        }

        public void Save()
        {
            Lazy.Update(Graph);
            IsDirty = false;
        }

        public void Dispose()
        {
            if (View != null)
            {
                View.RemoveFromHierarchy();
                session.CommandsFor(Graph).OnHistoryChanged -= MarkDirty;
                View = null;
            }
            Graph = null;
        }

        private void MarkDirty() => IsDirty = true;
    }
}