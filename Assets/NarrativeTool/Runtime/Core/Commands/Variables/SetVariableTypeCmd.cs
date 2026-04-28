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
        private readonly string oldEnumTypeId;   // captured so undo restores it
        private readonly string newEnumTypeId;

        public SetVariableTypeCmd(ProjectModel project, EventBus bus, string variableId,
                                  VariableType oldType, VariableType newType,
                                  object oldDefault, string oldEnumTypeId)
        {
            this.project = project; this.bus = bus; this.variableId = variableId;
            this.oldType = oldType; this.newType = newType;
            this.oldDefault = oldDefault;
            this.oldEnumTypeId = oldEnumTypeId;
            // When switching to Enum, auto-bind to the first available enum
            // (and its first member) so the picker doesn't show a phantom
            // selection that doesn't match the underlying data. The user can
            // then change the enum via SetVariableEnumTypeCmd.
            if (newType == VariableType.Enum && project.Enums.Items.Count > 0)
            {
                this.newEnumTypeId = project.Enums.Items[0].Id;
                this.newDefault = project.Enums.FirstMemberId(this.newEnumTypeId);
            }
            else
            {
                this.newEnumTypeId = null;
                this.newDefault = VariableStore.DefaultFor(newType);
            }
        }

        public void Do() => Apply(newType, newDefault, newEnumTypeId, oldType, oldEnumTypeId);
        public void Undo() => Apply(oldType, oldDefault, oldEnumTypeId, newType, newEnumTypeId);

        private void Apply(VariableType type, object defaultVal, string enumTypeId,
                           VariableType from, string fromEnumTypeId)
        {
            var v = project.Variables.Find(variableId);
            if (v == null) return;
            v.Type = type;
            v.DefaultValue = defaultVal;
            v.EnumTypeId = enumTypeId;
            bus.Publish(new VariableTypeChangedEvent(project.Id, variableId, from, type));
            if (fromEnumTypeId != enumTypeId)
                bus.Publish(new VariableEnumTypeChangedEvent(project.Id, variableId, fromEnumTypeId, enumTypeId));
            bus.Publish(new VariableDefaultChangedEvent(project.Id, variableId));
        }

        public bool TryMerge(ICommand previous) => false;
    }
}
