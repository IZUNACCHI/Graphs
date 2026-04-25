using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;
using System.Collections.Generic;

namespace NarrativeTool.Core.Commands
{
    /// <summary>
    /// Renames a folder and cascades the change to every nested folder and to
    /// every variable currently anywhere within the renamed subtree. Treated
    /// as a single undo entry — undo restores the prior state cleanly.
    /// </summary>
    public sealed class RenameVariableFolderCmd : ICommand
    {
        public string Name => $"Rename folder \"{oldPath}\" -> \"{newPath}\"";

        private readonly ProjectModel project;
        private readonly EventBus bus;
        private readonly string oldPath;
        private readonly string newPath;

        // Captured on Do for replay on Undo: the exact old folder paths and
        // the exact set of variable ids that were within the renamed subtree.
        private List<string> capturedFolderPaths;
        private List<string> capturedVariableIds;

        public RenameVariableFolderCmd(ProjectModel project, EventBus bus, string oldPath, string newPath)
        {
            this.project = project; this.bus = bus;
            this.oldPath = oldPath ?? ""; this.newPath = newPath ?? "";
        }

        public void Do() => Apply(oldPath, newPath, capture: true);
        public void Undo() => Apply(newPath, oldPath, capture: false);

        private void Apply(string from, string to, bool capture)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;
            if (from == to) return;

            // Affected folder paths: exact match + any starting with "from/"
            var affectedFolders = capture ? new List<string>() : capturedFolderPaths;
            if (capture)
            {
                foreach (var f in project.Variables.Folders)
                {
                    if (f == from || (f != null && f.StartsWith(from + "/")))
                        affectedFolders.Add(f);
                }
                capturedFolderPaths = affectedFolders;
            }

            // Affected variable ids: any whose FolderPath is in the subtree
            var affectedVars = capture ? new List<string>() : capturedVariableIds;
            if (capture)
            {
                foreach (var v in project.Variables.Variables)
                {
                    if (v.FolderPath == from || (v.FolderPath != null && v.FolderPath.StartsWith(from + "/")))
                        affectedVars.Add(v.Id);
                }
                capturedVariableIds = affectedVars;
            }

            // Rewrite folder list
            for (int i = 0; i < project.Variables.Folders.Count; i++)
            {
                var f = project.Variables.Folders[i];
                if (f == from) project.Variables.Folders[i] = to;
                else if (f != null && f.StartsWith(from + "/"))
                    project.Variables.Folders[i] = to + f.Substring(from.Length);
            }

            // Rewrite affected variables' FolderPath
            foreach (var id in affectedVars)
            {
                var v = project.Variables.Find(id);
                if (v == null) continue;
                if (v.FolderPath == from) v.FolderPath = to;
                else if (v.FolderPath != null && v.FolderPath.StartsWith(from + "/"))
                    v.FolderPath = to + v.FolderPath.Substring(from.Length);
            }

            bus.Publish(new VariableFolderRenamedEvent(project.Id, from, to));
        }

        public bool TryMerge(ICommand previous) => false;
    }
}
