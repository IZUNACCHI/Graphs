using NarrativeTool.Data.Project;
using System.Collections.Generic;
using System.Linq;

namespace NarrativeTool.Core.Runtime
{
    public class RuntimeEntityStore : IEntityAccess
    {
        private readonly ProjectModel project;

        // Separate runtime state: entityId  (fieldName - currentValue)
        private readonly Dictionary<string, Dictionary<string, object>> runtimeValues = new();

        public RuntimeEntityStore(ProjectModel project)
        {
            this.project = project;
            InitialiseFromDefinitions();
        }

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
                if (fields.ContainsKey(fieldName))
                    fields[fieldName] = value;
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
    }
}