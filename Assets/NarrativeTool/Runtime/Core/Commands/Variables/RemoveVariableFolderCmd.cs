using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;
using System.Collections.Generic;

namespace NarrativeTool.Core.Commands
{
    /// <summary>
    /// Removes a folder. The caller is responsible for handling items inside
    /// it — typically by wrapping this in a transaction with RemoveVariableCmd
    /// for each child. This command itself only touches the folder list so
    /// undo precisely reverses.
    /// </summary>
    public sealed class RemoveVariableFolderCmd : ICommand
    {
        public string Name => $"Remove folder \"{folderPath}\"";

        private readonly ProjectModel project;
        private readonly EventBus bus;
        private readonly string folderPath;
        private int restoreIndex = -1;

        public RemoveVariableFolderCmd(ProjectModel project, EventBus bus, string folderPath)
        {
            this.project = project; this.bus = bus; this.folderPath = folderPath;
        }

        public void Do()
        {
            restoreIndex = project.Variables.Folders.IndexOf(folderPath);
            if (restoreIndex < 0) return;
            project.Variables.Folders.RemoveAt(restoreIndex);
            bus.Publish(new VariableFolderRemovedEvent(project.Id, folderPath));
        }

        public void Undo()
        {
            if (restoreIndex < 0) return;
            int idx = System.Math.Min(restoreIndex, project.Variables.Folders.Count);
            project.Variables.Folders.Insert(idx, folderPath);
            bus.Publish(new VariableFolderAddedEvent(project.Id, folderPath));
        }

        public bool TryMerge(ICommand previous) => false;
    }
}
