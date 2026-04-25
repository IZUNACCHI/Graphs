namespace NarrativeTool.Core.EventSystem
{
    // Events about enum *definitions* (CRUD on the enum types themselves).
    // For the per-variable VariableEnumTypeChangedEvent see VariableEvents.cs.

    public readonly struct EnumAddedEvent
    {
        public readonly string ProjectId; public readonly string EnumId;
        public EnumAddedEvent(string p, string e) { ProjectId = p; EnumId = e; }
        public override string ToString() => $"{{ Enum={EnumId} added }}";
    }

    public readonly struct EnumRemovedEvent
    {
        public readonly string ProjectId; public readonly string EnumId;
        public EnumRemovedEvent(string p, string e) { ProjectId = p; EnumId = e; }
        public override string ToString() => $"{{ Enum={EnumId} removed }}";
    }

    public readonly struct EnumRenamedEvent
    {
        public readonly string ProjectId; public readonly string EnumId;
        public readonly string OldName, NewName;
        public EnumRenamedEvent(string p, string e, string o, string n)
        { ProjectId = p; EnumId = e; OldName = o; NewName = n; }
        public override string ToString() => $"{{ Enum={EnumId}, \"{OldName}\" -> \"{NewName}\" }}";
    }

    public readonly struct EnumMemberChangedEvent
    {
        public readonly string ProjectId; public readonly string EnumId; public readonly string MemberId;
        public EnumMemberChangedEvent(string p, string e, string m)
        { ProjectId = p; EnumId = e; MemberId = m; }
        public override string ToString() => $"{{ Enum={EnumId} member={MemberId} changed }}";
    }

    public readonly struct EnumFolderAddedEvent
    {
        public readonly string ProjectId; public readonly string FolderPath;
        public EnumFolderAddedEvent(string p, string f) { ProjectId = p; FolderPath = f; }
        public override string ToString() => $"{{ EnumFolder \"{FolderPath}\" added }}";
    }

    public readonly struct EnumFolderRemovedEvent
    {
        public readonly string ProjectId; public readonly string FolderPath;
        public EnumFolderRemovedEvent(string p, string f) { ProjectId = p; FolderPath = f; }
        public override string ToString() => $"{{ EnumFolder \"{FolderPath}\" removed }}";
    }

    public readonly struct EnumFolderRenamedEvent
    {
        public readonly string ProjectId; public readonly string OldPath, NewPath;
        public EnumFolderRenamedEvent(string p, string o, string n)
        { ProjectId = p; OldPath = o; NewPath = n; }
        public override string ToString() => $"{{ EnumFolder \"{OldPath}\" -> \"{NewPath}\" }}";
    }
}
