using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;

namespace NarrativeTool.Core.Commands
{
    /// <summary>
    /// Changes which enum a Type=Enum variable points at. Snaps DefaultValue
    /// to the new enum's first member (the old member id likely doesn't
    /// exist on the new type). Only meaningful when the variable's Type is
    /// already Enum — callers should pair this with SetVariableTypeCmd in a
    /// transaction if changing both.
    /// </summary>
    public sealed class SetVariableEnumTypeCmd : ICommand
    {
        public string Name => $"Set enum type {oldEnumTypeId} -> {newEnumTypeId}";

        private readonly ProjectModel project;
        private readonly EventBus bus;
        private readonly string variableId;
        private readonly string oldEnumTypeId;
        private readonly string newEnumTypeId;
        private readonly object oldDefault;
        private readonly object newDefault;

        public SetVariableEnumTypeCmd(ProjectModel project, EventBus bus, string variableId,
                                      string oldEnumTypeId, string newEnumTypeId, object oldDefault)
        {
            this.project = project; this.bus = bus; this.variableId = variableId;
            this.oldEnumTypeId = oldEnumTypeId;
            this.newEnumTypeId = newEnumTypeId;
            this.oldDefault = oldDefault;
            this.newDefault = project.Enums.FirstMemberId(newEnumTypeId);
        }

        public void Do() => Apply(newEnumTypeId, newDefault, oldEnumTypeId);
        public void Undo() => Apply(oldEnumTypeId, oldDefault, newEnumTypeId);

        private void Apply(string enumTypeId, object defaultVal, string from)
        {
            var v = project.Variables.Find(variableId);
            if (v == null) return;
            v.EnumTypeId = enumTypeId;
            v.DefaultValue = defaultVal;
            bus.Publish(new VariableEnumTypeChangedEvent(project.Id, variableId, from, enumTypeId));
            bus.Publish(new VariableDefaultChangedEvent(project.Id, variableId));
        }

        public bool TryMerge(ICommand previous) => false;
    }
}
