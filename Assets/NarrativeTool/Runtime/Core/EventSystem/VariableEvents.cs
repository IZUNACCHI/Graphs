using NarrativeTool.Data.Project;

namespace NarrativeTool.Core.EventSystem
{
    public readonly struct VariableAddedEvent
    {
        public readonly string ProjectId;
        public readonly string VariableId;
        public VariableAddedEvent(string projectId, string variableId)
        { ProjectId = projectId; VariableId = variableId; }
        public override string ToString() => $"{{ Project={ProjectId}, Var={VariableId} }}";
    }

    public readonly struct VariableRemovedEvent
    {
        public readonly string ProjectId;
        public readonly string VariableId;
        public VariableRemovedEvent(string projectId, string variableId)
        { ProjectId = projectId; VariableId = variableId; }
        public override string ToString() => $"{{ Project={ProjectId}, Var={VariableId} }}";
    }

    public readonly struct VariableRenamedEvent
    {
        public readonly string ProjectId;
        public readonly string VariableId;
        public readonly string OldName;
        public readonly string NewName;
        public VariableRenamedEvent(string projectId, string variableId, string oldName, string newName)
        { ProjectId = projectId; VariableId = variableId; OldName = oldName; NewName = newName; }
        public override string ToString() => $"{{ Var={VariableId}, \"{OldName}\" -> \"{NewName}\" }}";
        // TODO scripting: a future rename refactor system will subscribe here
        // to update string-based references in script bodies. Id-based refs
        // do not need updating.
    }

    public readonly struct VariableTypeChangedEvent
    {
        public readonly string ProjectId;
        public readonly string VariableId;
        public readonly VariableType OldType;
        public readonly VariableType NewType;
        public VariableTypeChangedEvent(string projectId, string variableId, VariableType oldType, VariableType newType)
        { ProjectId = projectId; VariableId = variableId; OldType = oldType; NewType = newType; }
        public override string ToString() => $"{{ Var={VariableId}, {OldType} -> {NewType} }}";
    }

    public readonly struct VariableDefaultChangedEvent
    {
        public readonly string ProjectId;
        public readonly string VariableId;
        public VariableDefaultChangedEvent(string projectId, string variableId)
        { ProjectId = projectId; VariableId = variableId; }
        public override string ToString() => $"{{ Var={VariableId} default changed }}";
    }

    public readonly struct VariableEnumTypeChangedEvent
    {
        public readonly string ProjectId;
        public readonly string VariableId;
        public readonly string OldEnumTypeId;
        public readonly string NewEnumTypeId;
        public VariableEnumTypeChangedEvent(string projectId, string variableId, string oldEnumTypeId, string newEnumTypeId)
        { ProjectId = projectId; VariableId = variableId; OldEnumTypeId = oldEnumTypeId; NewEnumTypeId = newEnumTypeId; }
        public override string ToString() => $"{{ Var={VariableId}, enum {OldEnumTypeId} -> {NewEnumTypeId} }}";
    }

    public readonly struct VariableMovedEvent
    {
        public readonly string ProjectId;
        public readonly string VariableId;
        public readonly string OldFolder;
        public readonly string NewFolder;
        public VariableMovedEvent(string projectId, string variableId, string oldFolder, string newFolder)
        { ProjectId = projectId; VariableId = variableId; OldFolder = oldFolder; NewFolder = newFolder; }
        public override string ToString() => $"{{ Var={VariableId}, \"{OldFolder}\" -> \"{NewFolder}\" }}";
    }

    public readonly struct VariableFolderAddedEvent
    {
        public readonly string ProjectId;
        public readonly string FolderPath;
        public VariableFolderAddedEvent(string projectId, string folderPath)
        { ProjectId = projectId; FolderPath = folderPath; }
        public override string ToString() => $"{{ Project={ProjectId}, Folder=\"{FolderPath}\" }}";
    }

    public readonly struct VariableFolderRemovedEvent
    {
        public readonly string ProjectId;
        public readonly string FolderPath;
        public VariableFolderRemovedEvent(string projectId, string folderPath)
        { ProjectId = projectId; FolderPath = folderPath; }
        public override string ToString() => $"{{ Project={ProjectId}, Folder=\"{FolderPath}\" }}";
    }

    public readonly struct VariableFolderRenamedEvent
    {
        public readonly string ProjectId;
        public readonly string OldPath;
        public readonly string NewPath;
        public VariableFolderRenamedEvent(string projectId, string oldPath, string newPath)
        { ProjectId = projectId; OldPath = oldPath; NewPath = newPath; }
        public override string ToString() => $"{{ Folder \"{OldPath}\" -> \"{NewPath}\" }}";
    }
}
