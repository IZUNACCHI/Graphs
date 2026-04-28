using NarrativeTool.Data.Project;

namespace NarrativeTool.Core.Runtime
{
    public interface IEntityAccess
    {
        /// <summary>Get an entity's field value by entity name and field name.</summary>
        object GetValue(string entityName, string fieldName);

        /// <summary>Set (mutate) an entity's field value at runtime.</summary>
        void SetValue(string entityName, string fieldName, object value);

        /// <summary>Returns all fields of an entity (for enumeration in scripts).</summary>
        EntityDefinition GetDefinition(string entityName);
    }
}