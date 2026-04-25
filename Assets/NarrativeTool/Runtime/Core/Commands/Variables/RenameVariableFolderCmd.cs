using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;
using System.Collections.Generic;

namespace NarrativeTool.Core.Commands
{
    /// <summary>
    /// Renames a folder and reparents every variable currently in it. Treated
    /// as a single undo entry — folder list edit + bulk variable folder edit
    /// done atomically so undo restores the prior state cleanly.
    /// </summary>
    public sealed class RenameVariableFolderCmd : ICommand
    {
        public string Name => $"Rename folder \"{oldPath}\" -> \"{newPath}\"";

        private readonly ProjectModel project;
        private readonly EventBus bus;
        private readonly string oldPath;
        private readonly string newPath;
        private List<string> affectedVariableIds;  // captured on Do, replayed on Undo

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

            int idx = project.Variables.Folders.IndexOf(from);
            if (idx < 0) return;
            project.Variables.Folders[idx] = to;

            if (capture)
            {
                affectedVariableIds = new List<string>();
                foreach (var v in project.Variables.Variables)
                    if (v.FolderPath == from) affectedVariableIds.Add(v.Id);
            }

            if (affectedVariableIds != null)
            {
                foreach (var id in affectedVariableIds)
                {
                    var v = project.Variables.Find(id);
                    if (v != null) v.FolderPath = to;
                }
            }

            bus.Publish(new VariableFolderRenamedEvent(project.Id, from, to));
        }

        public bool TryMerge(ICommand previous) => false;
    }
}
