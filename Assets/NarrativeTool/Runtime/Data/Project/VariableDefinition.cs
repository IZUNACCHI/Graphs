namespace NarrativeTool.Data.Project
{
    /// <summary>
    /// Project-scoped variable usable by future scripting (conditions, set
    /// commands, etc). Stored on <see cref="ProjectModel.Variables"/>.
    ///
    /// Identity: <see cref="Id"/> is a stable GUID generated at creation and
    /// is the canonical reference future scripts should bind against. <see
    /// cref="Name"/> is the user-facing display label and may change via
    /// rename. Keeping refs Id-based makes rename non-breaking.
    /// </summary>
    public sealed class VariableDefinition
    {
        public string Id { get; }                        // stable; never mutated
        public string Name { get; set; }                 // display, renameable
        public VariableType Type { get; set; }
        public object DefaultValue { get; set; }         // boxed; type matches Type

        // Only meaningful when Type == Enum. Refers to EnumDefinition.Id on
        // ProjectModel.Enums. When set, DefaultValue is an EnumMember.Id
        // (string). Null/empty for non-enum types.
        public string EnumTypeId { get; set; }

        // "" = root; "/"-delimited for nested folders, e.g. "player/stats".
        public string FolderPath { get; set; } = "";

        public VariableDefinition(string id, string name, VariableType type, object defaultValue,
                                  string folderPath = "", string enumTypeId = null)
        {
            Id = id;
            Name = name;
            Type = type;
            DefaultValue = defaultValue;
            FolderPath = folderPath ?? "";
            EnumTypeId = enumTypeId;
        }
    }

    public enum VariableType
    {
        Int,
        Float,
        Bool,
        String,
        Enum,
    }
}
