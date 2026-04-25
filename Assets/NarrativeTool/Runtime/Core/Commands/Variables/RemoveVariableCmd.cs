using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;

namespace NarrativeTool.Core.Commands
{
    public sealed class RemoveVariableCmd : ICommand
    {
        public string Name => $"Remove variable {variable?.Name}";

        private readonly ProjectModel project;
        private readonly EventBus bus;
        private readonly string variableId;
        private VariableDefinition variable;
        private int restoreIndex;

        // TODO scripting: when a variable is removed, any script bodies that
        // reference it will become invalid. A future system should either
        // (a) refuse removal if references exist, or (b) record refs so undo
        // and a rename-refactor pass can restore them. Captured here for now.

        public RemoveVariableCmd(ProjectModel project, EventBus bus, string variableId)
        {
            this.project = project; this.bus = bus; this.variableId = variableId;
        }

        public void Do()
        {
            variable = project.Variables.Find(variableId);
            if (variable == null) return;
            restoreIndex = project.Variables.Variables.IndexOf(variable);
            project.Variables.Variables.RemoveAt(restoreIndex);
            bus.Publish(new VariableRemovedEvent(project.Id, variableId));
        }

        public void Undo()
        {
            if (variable == null) return;
            int idx = System.Math.Min(restoreIndex, project.Variables.Variables.Count);
            project.Variables.Variables.Insert(idx, variable);
            bus.Publish(new VariableAddedEvent(project.Id, variable.Id));
        }

        public bool TryMerge(ICommand previous) => false;
    }
}
