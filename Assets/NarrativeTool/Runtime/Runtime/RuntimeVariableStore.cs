using NarrativeTool.Core.EventSystem;
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
        private readonly EventBus bus;
        private readonly Dictionary<string, object> currentValues = new();
        private readonly Dictionary<string, VariableDefinition> definitions = new();

        public RuntimeVariableStore(ProjectModel project, EventBus bus = null)
        {
            this.project = project;
            this.bus = bus;
            InitialiseFromDefinitions();
        }

        public IReadOnlyDictionary<string, VariableDefinition> Definitions => definitions;

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
            if (currentValues.TryGetValue(name, out var oldValue))
            {
                currentValues[name] = value;
                if (!Equals(oldValue, value))
                    bus?.Publish(new VariableRuntimeValueChangedEvent(name, oldValue, value));
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

        /// <summary>Snapshot the entire current-values dictionary (shallow copy of boxed values).</summary>
        public Dictionary<string, object> SnapshotValues() => new(currentValues);

        /// <summary>
        /// Replace current values from a snapshot. Publishes one
        /// <see cref="VariableRuntimeValueChangedEvent"/> per variable that
        /// actually changed (used by Undo Step so the Watch panel updates).
        /// </summary>
        public void RestoreValues(IReadOnlyDictionary<string, object> snapshot)
        {
            if (snapshot == null) return;
            foreach (var kv in snapshot)
            {
                if (!currentValues.TryGetValue(kv.Key, out var oldValue)) continue;
                if (Equals(oldValue, kv.Value)) continue;
                currentValues[kv.Key] = kv.Value;
                bus?.Publish(new VariableRuntimeValueChangedEvent(kv.Key, oldValue, kv.Value));
            }
        }
    }
}