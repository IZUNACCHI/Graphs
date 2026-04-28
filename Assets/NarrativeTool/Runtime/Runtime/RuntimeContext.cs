using NarrativeTool.Core.EventSystem;
using NarrativeTool.Core.Scripting;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Project;
using NarrativeTool.Runtime;
using System.Collections.Generic;

namespace NarrativeTool.Core.Runtime
{
    public class RuntimeContext
    {
        public ProjectModel Project { get; } // Provides access to project-wide data (e.g., metadata, variables, entities).
        public EventBus EventBus { get; } // Used to publish events that the UI and other systems can react to (e.g., node entered, choice presented).
        public IGraphLoader GraphLoader { get; } // Responsible for loading graph data.
        public IScriptingBackend Scripting { get; } // Provides scripting capabilities for evaluating conditions and executing scripts.
        public IVariableAccess Variables { get; } // Provides access to project variables.
        public IEntityAccess Entities { get; } // Provides access to project entities.

        public GraphData CurrentGraph { get; set; } // The graph currently being executed.
        public NodeData CurrentNode { get; set; } // The node currently being executed.
        public Stack<CallFrame> CallStack { get; } = new(); // The call stack for function calls within the graph.

        // Interaction context for the current pause (e.g., choice or continue). The engine sets this when it needs to wait for user input.
        public InteractionContext Interaction { get; } = new();

        public RuntimeContext(ProjectModel project, EventBus bus, IGraphLoader graphLoader,
                              IScriptingBackend scripting, IVariableAccess variableAccess,
                              IEntityAccess entityAccess)
        {
            Project = project;
            EventBus = bus;
            GraphLoader = graphLoader;
            Scripting = scripting;
            Variables = variableAccess;
            Entities = entityAccess;
        }

        public bool EvaluateCondition(string script)
        {
            if (string.IsNullOrEmpty(script)) return true;   // empty = always pass
            bool success = Scripting.Evaluate(script, out object result);
            return success && result is true;
        }
    }
}