using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;

namespace NarrativeTool.Core.Commands
{
    /// <summary>
    /// Renames a variable's display name. The variable's stable Id is
    /// untouched, so any future Id-based script references stay valid. A
    /// later "rename refactor" feature will scan script bodies for the OLD
    /// name and rewrite them; that system can subscribe to
    /// <see cref="VariableRenamedEvent"/>.
    /// </summary>
    public sealed class RenameVariableCmd : ICommand
    {
        public string Name => $"Rename variable {oldName} -> {newName}";

        private readonly ProjectModel project;
        private readonly EventBus bus;
        private readonly string variableId;
        private readonly string oldName;
        private readonly string newName;

        public RenameVariableCmd(ProjectModel project, EventBus bus, string variableId, string oldName, string newName)
        {
            this.project = project; this.bus = bus; this.variableId = variableId;
            this.oldName = oldName ?? ""; this.newName = newName ?? "";
        }

        public void Do() => Apply(newName, oldName);
        public void Undo() => Apply(oldName, newName);

        private void Apply(string to, string from)
        {
            var v = project.Variables.Find(variableId);
            if (v == null) return;
            v.Name = to;
            bus.Publish(new VariableRenamedEvent(project.Id, variableId, from, to));
        }

        public bool TryMerge(ICommand previous) => false;
    }
}
