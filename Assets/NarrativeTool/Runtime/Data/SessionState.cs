using System.Collections.Generic;
using NarrativeTool.Core;

namespace NarrativeTool.Data
{
    /// <summary>
    /// Runtime state for an open project. Owns per-graph services (command
    /// stacks, selection) that exist only while the project is open and are
    /// NOT serialised to disk.
    ///
    /// One SessionState per open project. When the project is closed, the
    /// entire SessionState is dropped along with all its histories and
    /// selections.
    ///
    /// Looking up a graph's services auto-creates them on first access.
    /// Switching tabs in a docked canvas later will simply re-look-up the
    /// services for the newly-active graph, preserving history across tab
    /// switches.
    /// </summary>
    public sealed class SessionState
    {
        private readonly EventBus bus;
        private readonly Dictionary<GraphDocument, CommandSystem> commandsByGraph = new();
        private readonly Dictionary<GraphDocument, SelectionService> selectionByGraph = new();

        public EventBus Bus => bus;

        public SessionState(EventBus bus)
        {
            this.bus = bus;
        }

        public CommandSystem CommandsFor(GraphDocument graph)
        {
            if (!commandsByGraph.TryGetValue(graph, out var cmds))
            {
                cmds = new CommandSystem();
                commandsByGraph[graph] = cmds;
            }
            return cmds;
        }

        public SelectionService SelectionFor(GraphDocument graph)
        {
            if (!selectionByGraph.TryGetValue(graph, out var sel))
            {
                sel = new SelectionService(graph, bus, CommandsFor(graph));
                selectionByGraph[graph] = sel;
            }
            return sel;
        }

        /// <summary>
        /// Drop all per-graph state. Called when the project closes or when a
        /// different project is being loaded. Canvases should be unbound
        /// before this runs.
        /// </summary>
        public void Clear()
        {
            commandsByGraph.Clear();
            selectionByGraph.Clear();
        }
    }
}