using NarrativeTool.Data.Graph;
using System.Collections.Generic;

namespace NarrativeTool.Data.Project
{
    public sealed class ProjectModel
    {
        public string Id { get; set; } // stable GUID generated at creation; should never change and is the canonical reference for this project
        public string Name { get; set; } // user-facing name; can be edited freely and is not guaranteed to be unique
        public int SchemaVersion { get; set; } = 1; // for future compatibility checks; bump when making breaking changes to the data format

        public FolderTreeStore<LazyGraph> Graphs { get; } = new(); // all graphs in the project, stored in a folder hierarchy; each graph is lazily deserialized from its RawData when needed

        public FolderTreeStore<VariableDefinition> Variables { get; } = new(); // all project-scoped variables; these are not tied to any specific graph and can be referenced globally (e.g., in conditions, set commands, etc)
        public FolderTreeStore<EnumDefinition> Enums { get; } = new(); // all project-scoped enums; these are not tied to any specific graph and can be referenced globally
        public FolderTreeStore<EntityDefinition> Entities { get; } = new(); // all project-scoped entities; these are not tied to any specific graph and can be referenced globally

        // Locales the project supports. en-US is always present and cannot
        // be removed (enforced at the wizard level for now). Settable so
        // JSON deserialization replaces the default-init list cleanly.
        public List<string> Locales { get; set; } = new() { "en-US" };

        public LazyGraph FindGraph(string id)
        {
            foreach (var g in Graphs.Items) if (g.Id == id) return g;
            return null;
        }
    }
}