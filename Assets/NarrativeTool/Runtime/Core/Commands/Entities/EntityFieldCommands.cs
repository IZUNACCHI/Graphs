using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;

namespace NarrativeTool.Core.Commands
{
    public sealed class AddEntityFieldCmd : ICommand
    {
        public string Name => $"Add field {field.Name}";
        private readonly ProjectModel project; private readonly EventBus bus;
        private readonly string entityId; private readonly EntityField field; private readonly int index;

        public AddEntityFieldCmd(ProjectModel project, EventBus bus, string entityId, EntityField field, int index = -1)
        {
            this.project = project; this.bus = bus; this.entityId = entityId; this.field = field;
            var ent = project.Entities.Find(entityId);
            this.index = index < 0 && ent != null ? ent.Fields.Count : index;
        }

        public void Do()
        {
            var ent = project.Entities.Find(entityId);
            if (ent == null || ent.FindField(field.Id) != null) return;
            int idx = System.Math.Min(index, ent.Fields.Count);
            ent.Fields.Insert(idx, field);
            bus.Publish(new EntityFieldChangedEvent(project.Id, entityId, ""));
        }
        public void Undo()
        {
            var ent = project.Entities.Find(entityId);
            if (ent == null) return;
            ent.Fields.Remove(field);
            bus.Publish(new EntityFieldChangedEvent(project.Id, entityId, ""));
        }
        public bool TryMerge(ICommand previous) => false;
    }

    public sealed class RemoveEntityFieldCmd : ICommand
    {
        public string Name => $"Remove field {fieldId}";
        private readonly ProjectModel project; private readonly EventBus bus;
        private readonly string entityId; private readonly string fieldId;
        private EntityField removed; private int restoreIndex;

        public RemoveEntityFieldCmd(ProjectModel project, EventBus bus, string entityId, string fieldId)
        { this.project = project; this.bus = bus; this.entityId = entityId; this.fieldId = fieldId; }

        public void Do()
        {
            var ent = project.Entities.Find(entityId); if (ent == null) return;
            removed = ent.FindField(fieldId); if (removed == null) return;
            restoreIndex = ent.Fields.IndexOf(removed);
            ent.Fields.RemoveAt(restoreIndex);
            bus.Publish(new EntityFieldChangedEvent(project.Id, entityId, ""));
        }
        public void Undo()
        {
            var ent = project.Entities.Find(entityId); if (ent == null || removed == null) return;
            int idx = System.Math.Min(restoreIndex, ent.Fields.Count);
            ent.Fields.Insert(idx, removed);
            bus.Publish(new EntityFieldChangedEvent(project.Id, entityId, ""));
        }
        public bool TryMerge(ICommand previous) => false;
    }

    public sealed class RenameEntityFieldCmd : ICommand
    {
        public string Name => $"Rename field {oldName} -> {newName}";
        private readonly ProjectModel project; private readonly EventBus bus;
        private readonly string entityId, fieldId, oldName, newName;

        public RenameEntityFieldCmd(ProjectModel project, EventBus bus, string entityId, string fieldId, string oldName, string newName)
        {
            this.project = project; this.bus = bus; this.entityId = entityId; this.fieldId = fieldId;
            this.oldName = oldName ?? ""; this.newName = newName ?? "";
        }

        public void Do() => Apply(newName);
        public void Undo() => Apply(oldName);
        private void Apply(string to)
        {
            var ent = project.Entities.Find(entityId); if (ent == null) return;
            var f = ent.FindField(fieldId); if (f == null) return;
            f.Name = to;
            bus.Publish(new EntityFieldChangedEvent(project.Id, entityId, fieldId));
        }
        public bool TryMerge(ICommand previous) => false;
    }

    /// <summary>
    /// Changes a field's type. Resets the default to the new type's zero
    /// (the old default may not be representable). Clears EnumTypeId since
    /// switching types invalidates the enum reference; the user re-picks
    /// via SetEntityFieldEnumTypeCmd.
    /// </summary>
    public sealed class SetEntityFieldTypeCmd : ICommand
    {
        public string Name => $"Change field type {oldType} -> {newType}";
        private readonly ProjectModel project; private readonly EventBus bus;
        private readonly string entityId, fieldId;
        private readonly VariableType oldType, newType;
        private readonly object oldDefault, newDefault;
        private readonly string oldEnumTypeId;

        private readonly string newEnumTypeId;

        public SetEntityFieldTypeCmd(ProjectModel project, EventBus bus, string entityId, string fieldId,
                                     VariableType oldType, VariableType newType, object oldDefault, string oldEnumTypeId)
        {
            this.project = project; this.bus = bus; this.entityId = entityId; this.fieldId = fieldId;
            this.oldType = oldType; this.newType = newType;
            this.oldDefault = oldDefault; this.oldEnumTypeId = oldEnumTypeId;
            // Auto-bind to the first available enum when switching to Enum
            // so the picker isn't phantom-selected. Mirrors SetVariableTypeCmd.
            if (newType == VariableType.Enum && project.Enums.Enums.Count > 0)
            {
                this.newEnumTypeId = project.Enums.Enums[0].Id;
                this.newDefault = project.Enums.FirstMemberId(this.newEnumTypeId);
            }
            else
            {
                this.newEnumTypeId = null;
                this.newDefault = VariableStore.DefaultFor(newType);
            }
        }

        public void Do() => Apply(newType, newDefault, newEnumTypeId);
        public void Undo() => Apply(oldType, oldDefault, oldEnumTypeId);

        private void Apply(VariableType type, object defaultVal, string enumTypeId)
        {
            var ent = project.Entities.Find(entityId); if (ent == null) return;
            var f = ent.FindField(fieldId); if (f == null) return;
            f.Type = type; f.DefaultValue = defaultVal; f.EnumTypeId = enumTypeId;
            bus.Publish(new EntityFieldChangedEvent(project.Id, entityId, fieldId));
        }
        public bool TryMerge(ICommand previous) => false;
    }

    public sealed class SetEntityFieldEnumTypeCmd : ICommand
    {
        public string Name => $"Set field enum {oldEnumTypeId} -> {newEnumTypeId}";
        private readonly ProjectModel project; private readonly EventBus bus;
        private readonly string entityId, fieldId;
        private readonly string oldEnumTypeId, newEnumTypeId;
        private readonly object oldDefault, newDefault;

        public SetEntityFieldEnumTypeCmd(ProjectModel project, EventBus bus, string entityId, string fieldId,
                                         string oldEnumTypeId, string newEnumTypeId, object oldDefault)
        {
            this.project = project; this.bus = bus; this.entityId = entityId; this.fieldId = fieldId;
            this.oldEnumTypeId = oldEnumTypeId; this.newEnumTypeId = newEnumTypeId;
            this.oldDefault = oldDefault;
            this.newDefault = project.Enums.FirstMemberId(newEnumTypeId);
        }

        public void Do() => Apply(newEnumTypeId, newDefault);
        public void Undo() => Apply(oldEnumTypeId, oldDefault);

        private void Apply(string enumTypeId, object defaultVal)
        {
            var ent = project.Entities.Find(entityId); if (ent == null) return;
            var f = ent.FindField(fieldId); if (f == null) return;
            f.EnumTypeId = enumTypeId; f.DefaultValue = defaultVal;
            bus.Publish(new EntityFieldChangedEvent(project.Id, entityId, fieldId));
        }
        public bool TryMerge(ICommand previous) => false;
    }

    public sealed class SetEntityFieldDefaultCmd : ICommand
    {
        public const float MergeWindowSeconds = 0.5f;
        public string Name => $"Set field default {fieldId}";
        private readonly ProjectModel project; private readonly EventBus bus;
        private readonly string entityId, fieldId;
        private object oldValue; private readonly object newValue;
        private readonly float timestamp;

        public SetEntityFieldDefaultCmd(ProjectModel project, EventBus bus, string entityId, string fieldId,
                                        object oldValue, object newValue)
        {
            this.project = project; this.bus = bus; this.entityId = entityId; this.fieldId = fieldId;
            this.oldValue = oldValue; this.newValue = newValue;
            this.timestamp = UnityEngine.Time.unscaledTime;
        }

        public void Do() => Apply(newValue);
        public void Undo() => Apply(oldValue);
        private void Apply(object v)
        {
            var ent = project.Entities.Find(entityId); if (ent == null) return;
            var f = ent.FindField(fieldId); if (f == null) return;
            f.DefaultValue = v;
            bus.Publish(new EntityFieldChangedEvent(project.Id, entityId, fieldId));
        }

        public bool TryMerge(ICommand previous)
        {
            if (previous is SetEntityFieldDefaultCmd p
                && p.entityId == entityId && p.fieldId == fieldId
                && (timestamp - p.timestamp) <= MergeWindowSeconds)
            {
                oldValue = p.oldValue; return true;
            }
            return false;
        }
    }
}
