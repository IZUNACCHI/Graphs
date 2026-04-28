using NarrativeTool.Data.Project;
using System.Collections.Generic;
using System.Linq;

namespace NarrativeTool.Core.Runtime
{
    /// <summary>
    /// Runtime variable store. Initialises itself from the project's variable
    /// definitions (from FolderTreeStore), then keeps a separate dictionary
    /// of current values for the duration of the run.
    /// </summary>
    public class RuntimeVariableStore : IVariableAccess
    {
        private readonly ProjectModel project;
        private readonly Dictionary<string, object> currentValues = new();
        private readonly Dictionary<string, VariableDefinition> definitions = new();

        public RuntimeVariableStore(ProjectModel project)
        {
            this.project = project;
            InitialiseFromDefinitions();
        }

        /// <summary>
        /// Reads all variable definitions from the project's FolderTreeStore and
        /// initialises the current values from their DefaultValue.
        /// </summary>
        private void InitialiseFromDefinitions()
        {
            currentValues.Clear();
            definitions.Clear();

            foreach (var def in project.Variables.Items)
            {
                definitions[def.Name] = def;
                currentValues[def.Name] = def.DefaultValue;  // copy the default
            }
        }

        /// <summary>
        /// Reset all current values back to the definition defaults.
        /// Call this when a run starts or restarts.
        /// </summary>
        public void ResetToDefaults()
        {
            InitialiseFromDefinitions();
        }

        public object GetValue(string name)
        {
            if (currentValues.TryGetValue(name, out var val))
                return val;

            UnityEngine.Debug.LogWarning($"[RuntimeVar] Variable '{name}' not found.");
            return null;
        }

        public void SetValue(string name, object value)
        {
            if (currentValues.ContainsKey(name))
            {
                currentValues[name] = value;
                // Optionally publish VariableChangedEvent here or let the caller do it
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[RuntimeVar] Variable '{name}' not found.");
            }
        }

        /// <summary>
        /// Returns the underlying definitions (for future use, e.g. type checks).
        /// </summary>
        public VariableDefinition GetDefinition(string name) =>
            definitions.TryGetValue(name, out var def) ? def : null;
    }
}