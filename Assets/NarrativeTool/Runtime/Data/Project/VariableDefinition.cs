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

        // "" = root; "/"-delimited for nested folders, e.g. "player/stats".
        // Folders are implicit (derived by grouping definitions). No separate
        // folder objects; rename of a folder = bulk path edit on its members.
        public string FolderPath { get; set; } = "";

        public VariableDefinition(string id, string name, VariableType type, object defaultValue, string folderPath = "")
        {
            Id = id;
            Name = name;
            Type = type;
            DefaultValue = defaultValue;
            FolderPath = folderPath ?? "";
        }
    }

    public enum VariableType
    {
        Int,
        Float,
        Bool,
        String,
        // TODO scripting: enum-typed variables. Will need a separate store of
        // enum type definitions (name + members) referenced here by id.
    }
}
