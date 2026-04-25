using System.Collections.Generic;

namespace NarrativeTool.Data.Project
{
    /// <summary>
    /// Container for a project's <see cref="VariableDefinition"/>s. Order in
    /// the list is the user-visible order within each folder.
    /// </summary>
    public sealed class VariableStore
    {
        public List<VariableDefinition> Variables { get; } = new();

        // Declared folder paths. Stored separately from variables so empty
        // folders persist and so a folder rename/delete is a single operation.
        // Paths are flat strings for now; "/"-delimited nesting can be added
        // later — the FolderTreeView already groups on the path string.
        public List<string> Folders { get; } = new();

        public VariableDefinition Find(string id)
        {
            foreach (var v in Variables) if (v.Id == id) return v;
            return null;
        }

        public bool FolderExists(string path)
        {
            path ??= "";
            if (string.IsNullOrEmpty(path)) return true; // root always exists
            return Folders.Contains(path);
        }

        /// <summary>
        /// True if any other variable shares this name within the same folder.
        /// Names are unique per folder so future scripts can resolve
        /// "player.reputation" unambiguously even before a full rename system
        /// is in place.
        /// </summary>
        public bool NameExistsInFolder(string folderPath, string name, string excludeId = null)
        {
            folderPath ??= "";
            foreach (var v in Variables)
            {
                if (v.Id == excludeId) continue;
                if (v.FolderPath == folderPath && v.Name == name) return true;
            }
            return false;
        }

        public static object DefaultFor(VariableType type) => type switch
        {
            VariableType.Int => 0,
            VariableType.Float => 0f,
            VariableType.Bool => false,
            VariableType.String => "",
            _ => null,
        };
    }
}
