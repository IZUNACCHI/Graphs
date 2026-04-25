using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;

namespace NarrativeTool.Core.Commands
{
    /// <summary>
    /// Changes a variable's type. Resets the default to the type's zero-value
    /// (the old default may not be representable in the new type) and stores
    /// the previous default for undo.
    ///
    /// TODO scripting: a type change can invalidate script expressions that
    /// rely on the old type (e.g. arithmetic on what's now a string). The
    /// future static checker should re-validate refs on this event.
    /// </summary>
    public sealed class SetVariableTypeCmd : ICommand
    {
        public string Name => $"Change type {oldType} -> {newType}";

        private readonly ProjectModel project;
        private readonly EventBus bus;
        private readonly string variableId;
        private readonly VariableType oldType;
        private readonly VariableType newType;
        private readonly object oldDefault;
        private readonly object newDefault;

        public SetVariableTypeCmd(ProjectModel project, EventBus bus, string variableId,
                                  VariableType oldType, VariableType newType, object oldDefault)
        {
            this.project = project; this.bus = bus; this.variableId = variableId;
            this.oldType = oldType; this.newType = newType;
            this.oldDefault = oldDefault;
            this.newDefault = VariableStore.DefaultFor(newType);
        }

        public void Do() => Apply(newType, newDefault, oldType);
        public void Undo() => Apply(oldType, oldDefault, newType);

        private void Apply(VariableType type, object defaultVal, VariableType from)
        {
            var v = project.Variables.Find(variableId);
            if (v == null) return;
            v.Type = type;
            v.DefaultValue = defaultVal;
            bus.Publish(new VariableTypeChangedEvent(project.Id, variableId, from, type));
            bus.Publish(new VariableDefaultChangedEvent(project.Id, variableId));
        }

        public bool TryMerge(ICommand previous) => false;
    }
}
