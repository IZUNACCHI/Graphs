using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;
using System.Collections.Generic;

namespace NarrativeTool.Core.Commands
{
    public sealed class AddEntityFolderCmd : ICommand
    {
        public string Name => $"Add entity folder \"{folderPath}\"";
        private readonly ProjectModel project; private readonly EventBus bus; private readonly string folderPath;

        public AddEntityFolderCmd(ProjectModel p, EventBus b, string folderPath)
        { project = p; bus = b; this.folderPath = folderPath; }

        public void Do()
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            if (project.Entities.Folders.Contains(folderPath)) return;
            project.Entities.Folders.Add(folderPath);
            bus.Publish(new EntityFolderAddedEvent(project.Id, folderPath));
        }
        public void Undo()
        {
            if (project.Entities.Folders.Remove(folderPath))
                bus.Publish(new EntityFolderRemovedEvent(project.Id, folderPath));
        }
        public bool TryMerge(ICommand previous) => false;
    }

    public sealed class RemoveEntityFolderCmd : ICommand
    {
        public string Name => $"Remove entity folder \"{folderPath}\"";
        private readonly ProjectModel project; private readonly EventBus bus; private readonly string folderPath;
        private int restoreIndex = -1;

        public RemoveEntityFolderCmd(ProjectModel p, EventBus b, string folderPath)
        { project = p; bus = b; this.folderPath = folderPath; }

        public void Do()
        {
            restoreIndex = project.Entities.Folders.IndexOf(folderPath);
            if (restoreIndex < 0) return;
            project.Entities.Folders.RemoveAt(restoreIndex);
            bus.Publish(new EntityFolderRemovedEvent(project.Id, folderPath));
        }
        public void Undo()
        {
            if (restoreIndex < 0) return;
            int idx = System.Math.Min(restoreIndex, project.Entities.Folders.Count);
            project.Entities.Folders.Insert(idx, folderPath);
            bus.Publish(new EntityFolderAddedEvent(project.Id, folderPath));
        }
        public bool TryMerge(ICommand previous) => false;
    }

    /// <summary>Cascades: also rewrites every nested folder + every entity in the subtree.</summary>
    public sealed class RenameEntityFolderCmd : ICommand
    {
        public string Name => $"Rename entity folder \"{oldPath}\" -> \"{newPath}\"";
        private readonly ProjectModel project; private readonly EventBus bus;
        private readonly string oldPath, newPath;
        private List<string> capturedEntityIds;

        public RenameEntityFolderCmd(ProjectModel p, EventBus b, string oldPath, string newPath)
        { project = p; bus = b; this.oldPath = oldPath ?? ""; this.newPath = newPath ?? ""; }

        public void Do() => Apply(oldPath, newPath, capture: true);
        public void Undo() => Apply(newPath, oldPath, capture: false);

        private void Apply(string from, string to, bool capture)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || from == to) return;

            if (capture)
            {
                capturedEntityIds = new List<string>();
                foreach (var e in project.Entities.Entities)
                    if (e.FolderPath == from || (e.FolderPath != null && e.FolderPath.StartsWith(from + "/")))
                        capturedEntityIds.Add(e.Id);
            }

            for (int i = 0; i < project.Entities.Folders.Count; i++)
            {
                var f = project.Entities.Folders[i];
                if (f == from) project.Entities.Folders[i] = to;
                else if (f != null && f.StartsWith(from + "/"))
                    project.Entities.Folders[i] = to + f.Substring(from.Length);
            }

            if (capturedEntityIds != null)
            {
                foreach (var id in capturedEntityIds)
                {
                    var e = project.Entities.Find(id); if (e == null) continue;
                    if (e.FolderPath == from) e.FolderPath = to;
                    else if (e.FolderPath != null && e.FolderPath.StartsWith(from + "/"))
                        e.FolderPath = to + e.FolderPath.Substring(from.Length);
                }
            }

            bus.Publish(new EntityFolderRenamedEvent(project.Id, from, to));
        }

        public bool TryMerge(ICommand previous) => false;
    }
}
