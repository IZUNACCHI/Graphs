using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;

namespace NarrativeTool.Core.Commands
{
    public sealed class AddVariableFolderCmd : ICommand
    {
        public string Name => $"Add folder \"{folderPath}\"";

        private readonly ProjectModel project;
        private readonly EventBus bus;
        private readonly string folderPath;

        public AddVariableFolderCmd(ProjectModel project, EventBus bus, string folderPath)
        {
            this.project = project; this.bus = bus; this.folderPath = folderPath;
        }

        public void Do()
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            if (project.Variables.Folders.Contains(folderPath)) return;
            project.Variables.Folders.Add(folderPath);
            bus.Publish(new VariableFolderAddedEvent(project.Id, folderPath));
        }

        public void Undo()
        {
            if (project.Variables.Folders.Remove(folderPath))
                bus.Publish(new VariableFolderRemovedEvent(project.Id, folderPath));
        }

        public bool TryMerge(ICommand previous) => false;
    }
}
