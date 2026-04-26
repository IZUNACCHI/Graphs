using NarrativeTool.Core.Commands;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Core.Selection;
using NarrativeTool.Data.Graph;
using System.Collections.Generic;

namespace NarrativeTool.Data.Project
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
        private readonly Dictionary<GraphData, CommandSystem> commandsByGraph = new();
        private readonly Dictionary<GraphData, SelectionService> selectionByGraph = new();
        private CommandSystem projectCommands;

        public EventBus Bus => bus;
        public ProjectModel Project { get; set; }
        // Absolute path the open project lives at on disk. Set on
        // open/create/save; null while no project is bound. Ctrl+S writes
        // back to this path.
        public string ProjectPath { get; set; }

        /// <summary>
        /// Undo stack for project-scoped operations (variables, entities,
        /// future global rename refactors). Independent from per-graph
        /// stacks so editing a variable doesn't pollute a graph's history.
        /// </summary>
        public CommandSystem ProjectCommands
            => projectCommands ??= new CommandSystem();

        public SessionState(EventBus bus)
        {
            this.bus = bus;
        }

        public CommandSystem CommandsFor(GraphData graph)
        {
            if (!commandsByGraph.TryGetValue(graph, out var cmds))
            {
                cmds = new CommandSystem();
                commandsByGraph[graph] = cmds;
            }
            return cmds;
        }

        public SelectionService SelectionFor(GraphData graph)
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
            projectCommands = null;
            Project = null;
            ProjectPath = null;
        }
    }
}