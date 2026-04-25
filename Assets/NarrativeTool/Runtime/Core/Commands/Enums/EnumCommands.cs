using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;

namespace NarrativeTool.Core.Commands
{
    public sealed class AddEnumCmd : ICommand
    {
        public string Name => $"Add enum {enumDef.Name}";
        private readonly ProjectModel project; private readonly EventBus bus;
        private readonly EnumDefinition enumDef; private readonly int index;

        public AddEnumCmd(ProjectModel p, EventBus b, EnumDefinition e, int index = -1)
        {
            project = p; bus = b; enumDef = e;
            this.index = index < 0 ? project.Enums.Enums.Count : index;
        }

        public void Do()
        {
            if (project.Enums.Find(enumDef.Id) != null) return;
            int idx = System.Math.Min(index, project.Enums.Enums.Count);
            project.Enums.Enums.Insert(idx, enumDef);
            bus.Publish(new EnumAddedEvent(project.Id, enumDef.Id));
        }
        public void Undo()
        {
            if (project.Enums.Find(enumDef.Id) == null) return;
            project.Enums.Enums.Remove(enumDef);
            bus.Publish(new EnumRemovedEvent(project.Id, enumDef.Id));
        }
        public bool TryMerge(ICommand previous) => false;
    }

    public sealed class RemoveEnumCmd : ICommand
    {
        public string Name => $"Remove enum {removed?.Name}";
        private readonly ProjectModel project; private readonly EventBus bus;
        private readonly string enumId;
        private EnumDefinition removed; private int restoreIndex;

        // TODO scripting: removing an enum should clear EnumTypeId on every
        // variable / entity field that referenced it, ideally inside a
        // transaction. Out of scope here — caller must handle.

        public RemoveEnumCmd(ProjectModel p, EventBus b, string enumId)
        { project = p; bus = b; this.enumId = enumId; }

        public void Do()
        {
            removed = project.Enums.Find(enumId); if (removed == null) return;
            restoreIndex = project.Enums.Enums.IndexOf(removed);
            project.Enums.Enums.RemoveAt(restoreIndex);
            bus.Publish(new EnumRemovedEvent(project.Id, enumId));
        }
        public void Undo()
        {
            if (removed == null) return;
            int idx = System.Math.Min(restoreIndex, project.Enums.Enums.Count);
            project.Enums.Enums.Insert(idx, removed);
            bus.Publish(new EnumAddedEvent(project.Id, removed.Id));
        }
        public bool TryMerge(ICommand previous) => false;
    }

    public sealed class RenameEnumCmd : ICommand
    {
        public string Name => $"Rename enum {oldName} -> {newName}";
        private readonly ProjectModel project; private readonly EventBus bus;
        private readonly string enumId, oldName, newName;

        public RenameEnumCmd(ProjectModel p, EventBus b, string enumId, string oldName, string newName)
        {
            project = p; bus = b; this.enumId = enumId;
            this.oldName = oldName ?? ""; this.newName = newName ?? "";
        }

        public void Do() => Apply(newName, oldName);
        public void Undo() => Apply(oldName, newName);
        private void Apply(string to, string from)
        {
            var e = project.Enums.Find(enumId); if (e == null) return;
            e.Name = to;
            bus.Publish(new EnumRenamedEvent(project.Id, enumId, from, to));
        }
        public bool TryMerge(ICommand previous) => false;
    }
}
