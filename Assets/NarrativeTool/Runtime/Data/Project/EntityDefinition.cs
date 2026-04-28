using System.Collections.Generic;

namespace NarrativeTool.Data.Project
{
    /// <summary>
    /// User-defined struct-like type. Has named, typed fields. Future
    /// scripting will let entity instances be created and queried via these
    /// definitions. As with variables/enums, <see cref="Id"/> is the stable
    /// reference future scripts bind against.
    /// </summary>
    public sealed class EntityDefinition : IFolderableItem, INamedItem
    {
        public string Id { get;  set; }
        public string Name { get; set; }
        public string FolderPath { get; set; } = "";
        public List<EntityField> Fields { get; } = new();

        public EntityDefinition(string id, string name, string folderPath = "")
        {
            Id = id; Name = name; FolderPath = folderPath ?? "";
        }

        public EntityDefinition() { }

        public EntityField FindField(string fieldId)
        {
            foreach (var f in Fields) if (f.Id == fieldId) return f;
            return null;
        }
    }

    /// <summary>
    /// One field on an entity. Mirrors VariableDefinition's shape (type +
    /// optional enum reference + boxed default) so an entity field can hold
    /// any of the same value kinds a top-level variable can.
    /// </summary>
    public sealed class EntityField
    {
        public string Id { get; }
        public string Name { get; set; }
        public VariableType Type { get; set; }
        public string EnumTypeId { get; set; }   // only for VariableType.Enum
        public object DefaultValue { get; set; }

        public EntityField(string id, string name, VariableType type, object defaultValue, string enumTypeId = null)
        {
            Id = id; Name = name; Type = type; DefaultValue = defaultValue; EnumTypeId = enumTypeId;
        }
    }
}
