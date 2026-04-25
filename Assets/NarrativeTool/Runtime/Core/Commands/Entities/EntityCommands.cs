using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;

namespace NarrativeTool.Core.Commands
{
    public sealed class AddEntityCmd : ICommand
    {
        public string Name => $"Add entity {entity.Name}";
        private readonly ProjectModel project; private readonly EventBus bus;
        private readonly EntityDefinition entity; private readonly int index;

        public AddEntityCmd(ProjectModel project, EventBus bus, EntityDefinition entity, int index = -1)
        {
            this.project = project; this.bus = bus; this.entity = entity;
            this.index = index < 0 ? project.Entities.Entities.Count : index;
        }

        public void Do()
        {
            if (project.Entities.Find(entity.Id) != null) return;
            int idx = System.Math.Min(index, project.Entities.Entities.Count);
            project.Entities.Entities.Insert(idx, entity);
            bus.Publish(new EntityAddedEvent(project.Id, entity.Id));
        }
        public void Undo()
        {
            if (project.Entities.Find(entity.Id) == null) return;
            project.Entities.Entities.Remove(entity);
            bus.Publish(new EntityRemovedEvent(project.Id, entity.Id));
        }
        public bool TryMerge(ICommand previous) => false;
    }

    public sealed class RemoveEntityCmd : ICommand
    {
        public string Name => $"Remove entity {entity?.Name}";
        private readonly ProjectModel project; private readonly EventBus bus;
        private readonly string entityId;
        private EntityDefinition entity; private int restoreIndex;

        public RemoveEntityCmd(ProjectModel project, EventBus bus, string entityId)
        { this.project = project; this.bus = bus; this.entityId = entityId; }

        public void Do()
        {
            entity = project.Entities.Find(entityId);
            if (entity == null) return;
            restoreIndex = project.Entities.Entities.IndexOf(entity);
            project.Entities.Entities.RemoveAt(restoreIndex);
            bus.Publish(new EntityRemovedEvent(project.Id, entityId));
        }
        public void Undo()
        {
            if (entity == null) return;
            int idx = System.Math.Min(restoreIndex, project.Entities.Entities.Count);
            project.Entities.Entities.Insert(idx, entity);
            bus.Publish(new EntityAddedEvent(project.Id, entity.Id));
        }
        public bool TryMerge(ICommand previous) => false;
    }

    public sealed class RenameEntityCmd : ICommand
    {
        public string Name => $"Rename entity {oldName} -> {newName}";
        private readonly ProjectModel project; private readonly EventBus bus;
        private readonly string entityId; private readonly string oldName, newName;

        public RenameEntityCmd(ProjectModel project, EventBus bus, string entityId, string oldName, string newName)
        {
            this.project = project; this.bus = bus; this.entityId = entityId;
            this.oldName = oldName ?? ""; this.newName = newName ?? "";
        }

        public void Do() => Apply(newName, oldName);
        public void Undo() => Apply(oldName, newName);
        private void Apply(string to, string from)
        {
            var e = project.Entities.Find(entityId);
            if (e == null) return;
            e.Name = to;
            bus.Publish(new EntityRenamedEvent(project.Id, entityId, from, to));
        }
        public bool TryMerge(ICommand previous) => false;
    }
}
