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

        /// <summary>
        /// Replaces {{variableName}} or {{Entity.Field}} patterns with the current
        /// runtime value. Single‑brace {text} is left unchanged.
        /// </summary>
        public string Interpolate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return System.Text.RegularExpressions.Regex.Replace(
                text,
                @"\{\{(.+?)\}\}",
                match =>
                {
                    string expression = match.Groups[1].Value.Trim();
                    // Try entity.field first (dot notation)
                    int dotIndex = expression.IndexOf('.');
                    if (dotIndex > 0)
                    {
                        string entityName = expression.Substring(0, dotIndex);
                        string fieldName = expression.Substring(dotIndex + 1);
                        object val = Entities?.GetValue(entityName, fieldName);
                        return val != null ? val.ToString() : $"???{expression}???";
                    }
                    // Otherwise treat as a variable name
                    object varVal = Variables.GetValue(expression);
                    return varVal != null ? varVal.ToString() : $"???{expression}???";
                });
        }
    }
}