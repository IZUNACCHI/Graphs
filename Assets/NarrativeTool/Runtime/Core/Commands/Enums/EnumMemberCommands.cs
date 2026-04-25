using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;

namespace NarrativeTool.Core.Commands
{
    public sealed class AddEnumMemberCmd : ICommand
    {
        public string Name => $"Add enum member {member.Name}";
        private readonly ProjectModel project; private readonly EventBus bus;
        private readonly string enumId; private readonly EnumMember member; private readonly int index;

        public AddEnumMemberCmd(ProjectModel p, EventBus b, string enumId, EnumMember member, int index = -1)
        {
            project = p; bus = b; this.enumId = enumId; this.member = member;
            var e = project.Enums.Find(enumId);
            this.index = index < 0 && e != null ? e.Members.Count : index;
        }

        public void Do()
        {
            var e = project.Enums.Find(enumId);
            if (e == null || e.FindMember(member.Id) != null) return;
            int idx = System.Math.Min(index, e.Members.Count);
            e.Members.Insert(idx, member);
            bus.Publish(new EnumMemberChangedEvent(project.Id, enumId, ""));
        }
        public void Undo()
        {
            var e = project.Enums.Find(enumId); if (e == null) return;
            e.Members.Remove(member);
            bus.Publish(new EnumMemberChangedEvent(project.Id, enumId, ""));
        }
        public bool TryMerge(ICommand previous) => false;
    }

    public sealed class RemoveEnumMemberCmd : ICommand
    {
        public string Name => $"Remove enum member {memberId}";
        private readonly ProjectModel project; private readonly EventBus bus;
        private readonly string enumId, memberId;
        private EnumMember removed; private int restoreIndex;

        // TODO scripting: removing a member should null out (or migrate)
        // any default values pointing at this member id. Out of scope here.

        public RemoveEnumMemberCmd(ProjectModel p, EventBus b, string enumId, string memberId)
        { project = p; bus = b; this.enumId = enumId; this.memberId = memberId; }

        public void Do()
        {
            var e = project.Enums.Find(enumId); if (e == null) return;
            removed = e.FindMember(memberId); if (removed == null) return;
            restoreIndex = e.Members.IndexOf(removed);
            e.Members.RemoveAt(restoreIndex);
            bus.Publish(new EnumMemberChangedEvent(project.Id, enumId, ""));
        }
        public void Undo()
        {
            var e = project.Enums.Find(enumId); if (e == null || removed == null) return;
            int idx = System.Math.Min(restoreIndex, e.Members.Count);
            e.Members.Insert(idx, removed);
            bus.Publish(new EnumMemberChangedEvent(project.Id, enumId, ""));
        }
        public bool TryMerge(ICommand previous) => false;
    }

    public sealed class RenameEnumMemberCmd : ICommand
    {
        public string Name => $"Rename enum member {oldName} -> {newName}";
        private readonly ProjectModel project; private readonly EventBus bus;
        private readonly string enumId, memberId, oldName, newName;

        public RenameEnumMemberCmd(ProjectModel p, EventBus b, string enumId, string memberId, string oldName, string newName)
        {
            project = p; bus = b; this.enumId = enumId; this.memberId = memberId;
            this.oldName = oldName ?? ""; this.newName = newName ?? "";
        }

        public void Do() => Apply(newName);
        public void Undo() => Apply(oldName);
        private void Apply(string to)
        {
            var e = project.Enums.Find(enumId); if (e == null) return;
            var m = e.FindMember(memberId); if (m == null) return;
            m.Name = to;
            bus.Publish(new EnumMemberChangedEvent(project.Id, enumId, memberId));
        }
        public bool TryMerge(ICommand previous) => false;
    }
}
