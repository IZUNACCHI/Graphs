using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;
using System.Collections.Generic;

namespace NarrativeTool.Core.Commands
{
    public sealed class AddEnumFolderCmd : ICommand
    {
        public string Name => $"Add enum folder \"{folderPath}\"";
        private readonly ProjectModel project; private readonly EventBus bus; private readonly string folderPath;

        public AddEnumFolderCmd(ProjectModel p, EventBus b, string folderPath)
        { project = p; bus = b; this.folderPath = folderPath; }

        public void Do()
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            if (project.Enums.Folders.Contains(folderPath)) return;
            project.Enums.Folders.Add(folderPath);
            bus.Publish(new EnumFolderAddedEvent(project.Id, folderPath));
        }
        public void Undo()
        {
            if (project.Enums.Folders.Remove(folderPath))
                bus.Publish(new EnumFolderRemovedEvent(project.Id, folderPath));
        }
        public bool TryMerge(ICommand previous) => false;
    }

    public sealed class RemoveEnumFolderCmd : ICommand
    {
        public string Name => $"Remove enum folder \"{folderPath}\"";
        private readonly ProjectModel project; private readonly EventBus bus; private readonly string folderPath;
        private int restoreIndex = -1;

        public RemoveEnumFolderCmd(ProjectModel p, EventBus b, string folderPath)
        { project = p; bus = b; this.folderPath = folderPath; }

        public void Do()
        {
            restoreIndex = project.Enums.Folders.IndexOf(folderPath);
            if (restoreIndex < 0) return;
            project.Enums.Folders.RemoveAt(restoreIndex);
            bus.Publish(new EnumFolderRemovedEvent(project.Id, folderPath));
        }
        public void Undo()
        {
            if (restoreIndex < 0) return;
            int idx = System.Math.Min(restoreIndex, project.Enums.Folders.Count);
            project.Enums.Folders.Insert(idx, folderPath);
            bus.Publish(new EnumFolderAddedEvent(project.Id, folderPath));
        }
        public bool TryMerge(ICommand previous) => false;
    }

    public sealed class RenameEnumFolderCmd : ICommand
    {
        public string Name => $"Rename enum folder \"{oldPath}\" -> \"{newPath}\"";
        private readonly ProjectModel project; private readonly EventBus bus;
        private readonly string oldPath, newPath;
        private List<string> capturedEnumIds;

        public RenameEnumFolderCmd(ProjectModel p, EventBus b, string oldPath, string newPath)
        { project = p; bus = b; this.oldPath = oldPath ?? ""; this.newPath = newPath ?? ""; }

        public void Do() => Apply(oldPath, newPath, capture: true);
        public void Undo() => Apply(newPath, oldPath, capture: false);

        private void Apply(string from, string to, bool capture)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || from == to) return;

            if (capture)
            {
                capturedEnumIds = new List<string>();
                foreach (var e in project.Enums.Enums)
                    if (e.FolderPath == from || (e.FolderPath != null && e.FolderPath.StartsWith(from + "/")))
                        capturedEnumIds.Add(e.Id);
            }

            for (int i = 0; i < project.Enums.Folders.Count; i++)
            {
                var f = project.Enums.Folders[i];
                if (f == from) project.Enums.Folders[i] = to;
                else if (f != null && f.StartsWith(from + "/"))
                    project.Enums.Folders[i] = to + f.Substring(from.Length);
            }

            if (capturedEnumIds != null)
            {
                foreach (var id in capturedEnumIds)
                {
                    var e = project.Enums.Find(id); if (e == null) continue;
                    if (e.FolderPath == from) e.FolderPath = to;
                    else if (e.FolderPath != null && e.FolderPath.StartsWith(from + "/"))
                        e.FolderPath = to + e.FolderPath.Substring(from.Length);
                }
            }

            bus.Publish(new EnumFolderRenamedEvent(project.Id, from, to));
        }

        public bool TryMerge(ICommand previous) => false;
    }
}
