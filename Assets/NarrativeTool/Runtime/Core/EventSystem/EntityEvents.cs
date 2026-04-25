using NarrativeTool.Data.Project;

namespace NarrativeTool.Core.EventSystem
{
    public readonly struct EntityAddedEvent
    {
        public readonly string ProjectId;
        public readonly string EntityId;
        public EntityAddedEvent(string p, string e) { ProjectId = p; EntityId = e; }
        public override string ToString() => $"{{ Entity={EntityId} added }}";
    }

    public readonly struct EntityRemovedEvent
    {
        public readonly string ProjectId;
        public readonly string EntityId;
        public EntityRemovedEvent(string p, string e) { ProjectId = p; EntityId = e; }
        public override string ToString() => $"{{ Entity={EntityId} removed }}";
    }

    public readonly struct EntityRenamedEvent
    {
        public readonly string ProjectId;
        public readonly string EntityId;
        public readonly string OldName, NewName;
        public EntityRenamedEvent(string p, string e, string o, string n)
        { ProjectId = p; EntityId = e; OldName = o; NewName = n; }
        public override string ToString() => $"{{ Entity={EntityId}, \"{OldName}\" -> \"{NewName}\" }}";
    }

    public readonly struct EntityFieldChangedEvent
    {
        public readonly string ProjectId;
        public readonly string EntityId;
        public readonly string FieldId;        // null/"" = whole field list changed (add/remove)
        public EntityFieldChangedEvent(string p, string e, string f)
        { ProjectId = p; EntityId = e; FieldId = f; }
        public override string ToString() => $"{{ Entity={EntityId} field={FieldId} changed }}";
    }

    public readonly struct EntityFolderAddedEvent
    {
        public readonly string ProjectId; public readonly string FolderPath;
        public EntityFolderAddedEvent(string p, string f) { ProjectId = p; FolderPath = f; }
        public override string ToString() => $"{{ EntityFolder \"{FolderPath}\" added }}";
    }

    public readonly struct EntityFolderRemovedEvent
    {
        public readonly string ProjectId; public readonly string FolderPath;
        public EntityFolderRemovedEvent(string p, string f) { ProjectId = p; FolderPath = f; }
        public override string ToString() => $"{{ EntityFolder \"{FolderPath}\" removed }}";
    }

    public readonly struct EntityFolderRenamedEvent
    {
        public readonly string ProjectId; public readonly string OldPath, NewPath;
        public EntityFolderRenamedEvent(string p, string o, string n)
        { ProjectId = p; OldPath = o; NewPath = n; }
        public override string ToString() => $"{{ EntityFolder \"{OldPath}\" -> \"{NewPath}\" }}";
    }
}
