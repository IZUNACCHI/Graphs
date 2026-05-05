using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;
using System.Collections.Generic;
using System.Linq;

namespace NarrativeTool.Core.Runtime
{
    public class RuntimeEntityStore : IEntityAccess
    {
        private readonly ProjectModel project;
        private readonly EventBus bus;

        // Separate runtime state: entityId  (fieldName - currentValue)
        private readonly Dictionary<string, Dictionary<string, object>> runtimeValues = new();

        public RuntimeEntityStore(ProjectModel project, EventBus bus = null)
        {
            this.project = project;
            this.bus = bus;
            InitialiseFromDefinitions();
        }

        public IReadOnlyDictionary<string, Dictionary<string, object>> RuntimeValues => runtimeValues;

        /// <summary>
        /// Copy all entity field defaults into the runtime only dictionary.
        /// </summary>
        private void InitialiseFromDefinitions()
        {
            runtimeValues.Clear();
            foreach (var entity in project.Entities.Items)
            {
                var fields = new Dictionary<string, object>();
                foreach (var field in entity.Fields)
                    fields[field.Name] = field.DefaultValue;
                runtimeValues[entity.Name] = fields;
            }
        }

        /// <summary>Reset runtime state back to defaults. Call before a new playthrough.</summary>
        public void ResetToDefaults() => InitialiseFromDefinitions();

        public object GetValue(string entityName, string fieldName)
        {
            if (runtimeValues.TryGetValue(entityName, out var fields) &&
                fields.TryGetValue(fieldName, out var value))
                return value;

            UnityEngine.Debug.LogWarning($"[RuntimeEntity] '{entityName}.{fieldName}' not found.");
            return null;
        }

        public void SetValue(string entityName, string fieldName, object value)
        {
            if (runtimeValues.TryGetValue(entityName, out var fields))
            {
                if (fields.TryGetValue(fieldName, out var oldValue))
                {
                    fields[fieldName] = value;
                    if (!Equals(oldValue, value))
                        bus?.Publish(new EntityRuntimeValueChangedEvent(entityName, fieldName, oldValue, value));
                }
                else
                    UnityEngine.Debug.LogWarning($"[RuntimeEntity] Field '{fieldName}' not found on entity '{entityName}'.");
            }
            else
                UnityEngine.Debug.LogWarning($"[RuntimeEntity] Entity '{entityName}' not found.");
        }

        public EntityDefinition GetDefinition(string entityName)
        {
            return project.Entities.Items.FirstOrDefault(e => e.Name == entityName);
        }

        /// <summary>Deep-snapshot all entity field values.</summary>
        public Dictionary<string, Dictionary<string, object>> SnapshotValues()
        {
            var snap = new Dictionary<string, Dictionary<string, object>>();
            foreach (var kv in runtimeValues)
                snap[kv.Key] = new Dictionary<string, object>(kv.Value);
            return snap;
        }

        /// <summary>
        /// Restore entity field values from a snapshot. Publishes one
        /// <see cref="EntityRuntimeValueChangedEvent"/> per field that
        /// actually changed (used by Undo Step so the Watch panel updates).
        /// </summary>
        public void RestoreValues(IReadOnlyDictionary<string, Dictionary<string, object>> snapshot)
        {
            if (snapshot == null) return;
            foreach (var ekv in snapshot)
            {
                if (!runtimeValues.TryGetValue(ekv.Key, out var fields)) continue;
                foreach (var fkv in ekv.Value)
                {
                    if (!fields.TryGetValue(fkv.Key, out var oldValue)) continue;
                    if (Equals(oldValue, fkv.Value)) continue;
                    fields[fkv.Key] = fkv.Value;
                    bus?.Publish(new EntityRuntimeValueChangedEvent(ekv.Key, fkv.Key, oldValue, fkv.Value));
                }
            }
        }
    }
}