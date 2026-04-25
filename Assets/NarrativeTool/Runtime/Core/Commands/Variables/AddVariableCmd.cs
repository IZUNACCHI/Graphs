using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;

namespace NarrativeTool.Core.Commands
{
    public sealed class AddVariableCmd : ICommand
    {
        public string Name => $"Add variable {variable.Name}";

        private readonly ProjectModel project;
        private readonly EventBus bus;
        private readonly VariableDefinition variable;
        private readonly int index;

        public AddVariableCmd(ProjectModel project, EventBus bus, VariableDefinition variable, int index = -1)
        {
            this.project = project; this.bus = bus; this.variable = variable;
            this.index = index < 0 ? project.Variables.Variables.Count : index;
        }

        public void Do()
        {
            if (project.Variables.Find(variable.Id) != null) return;
            int idx = System.Math.Min(index, project.Variables.Variables.Count);
            project.Variables.Variables.Insert(idx, variable);
            bus.Publish(new VariableAddedEvent(project.Id, variable.Id));
        }

        public void Undo()
        {
            if (project.Variables.Find(variable.Id) == null) return;
            project.Variables.Variables.Remove(variable);
            bus.Publish(new VariableRemovedEvent(project.Id, variable.Id));
        }

        public bool TryMerge(ICommand previous) => false;
    }
}
