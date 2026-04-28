using UnityEngine;

namespace NarrativeTool.Core.EventSystem
{
    public readonly struct GraphAddedEvent
    {
        public readonly string ProjectId;
        public readonly string GraphId;
        public GraphAddedEvent(string projectId, string graphId)
        {
            ProjectId = projectId;
            GraphId = graphId;
        }
        public override string ToString() => $"{{ Graph={GraphId} added }}";
    }

    public readonly struct GraphRemovedEvent
    {
        public readonly string ProjectId;
        public readonly string GraphId;
        public GraphRemovedEvent(string projectId, string graphId)
        {
            ProjectId = projectId;
            GraphId = graphId;
        }
        public override string ToString() => $"{{ Graph={GraphId} removed }}";
    }

    public readonly struct GraphRenamedEvent
    {
        public readonly string ProjectId;
        public readonly string GraphId;
        public readonly string OldName;
        public readonly string NewName;
        public GraphRenamedEvent(string projectId, string graphId, string oldName, string newName)
        {
            ProjectId = projectId;
            GraphId = graphId;
            OldName = oldName;
            NewName = newName;
        }
        public override string ToString() => $"{{ Graph={GraphId}, \"{OldName}\" -> \"{NewName}\" }}";
    }

    public readonly struct GraphFolderAddedEvent
    {
        public readonly string ProjectId;
        public readonly string FolderPath;
        public GraphFolderAddedEvent(string projectId, string folderPath)
        {
            ProjectId = projectId;
            FolderPath = folderPath;
        }
        public override string ToString() => $"{{ GraphFolder \"{FolderPath}\" added }}";
    }

    public readonly struct GraphFolderRemovedEvent
    {
        public readonly string ProjectId;
        public readonly string FolderPath;
        public GraphFolderRemovedEvent(string projectId, string folderPath)
        {
            ProjectId = projectId;
            FolderPath = folderPath;
        }
        public override string ToString() => $"{{ GraphFolder \"{FolderPath}\" removed }}";
    }

    public readonly struct GraphFolderRenamedEvent
    {
        public readonly string ProjectId;
        public readonly string OldPath;
        public readonly string NewPath;
        public GraphFolderRenamedEvent(string projectId, string oldPath, string newPath)
        {
            ProjectId = projectId;
            OldPath = oldPath;
            NewPath = newPath;
        }
        public override string ToString() => $"{{ GraphFolder \"{OldPath}\" -> \"{NewPath}\" }}";
    }

    public readonly struct NodeAddedEvent
    {
        public readonly string GraphId;
        public readonly string NodeId;
        public NodeAddedEvent(string graphId, string nodeId) { GraphId = graphId; NodeId = nodeId; }
        public override string ToString() => $"{{ Graph={GraphId}, Node={NodeId} }}";
    }

    public readonly struct NodeRemovedEvent
    {
        public readonly string GraphId;
        public readonly string NodeId;
        public NodeRemovedEvent(string graphId, string nodeId) { GraphId = graphId; NodeId = nodeId; }
        public override string ToString() => $"{{ Graph={GraphId}, Node={NodeId} }}";
    }

    public readonly struct NodeMovedEvent
    {
        public readonly string GraphId;
        public readonly string NodeId;
        public readonly Vector2 From;
        public readonly Vector2 To;
        public NodeMovedEvent(string graphId, string nodeId, Vector2 from, Vector2 to)
        { GraphId = graphId; NodeId = nodeId; From = from; To = to; }
        public override string ToString() => $"{{ Node={NodeId}, {From} -> {To} }}";
    }

    public readonly struct NodeTextChangedEvent
    {
        public readonly string GraphId;
        public readonly string NodeId;
        public readonly string OldText;
        public readonly string NewText;
        public NodeTextChangedEvent(string graphId, string nodeId, string oldText, string newText)
        { GraphId = graphId; NodeId = nodeId; OldText = oldText; NewText = newText; }
        public override string ToString() => $"{{ Node={NodeId}, \"{OldText}\" -> \"{NewText}\" }}";
    }

    public readonly struct EdgeAddedEvent
    {
        public readonly string GraphId;
        public readonly string EdgeId;
        public EdgeAddedEvent(string graphId, string edgeId) { GraphId = graphId; EdgeId = edgeId; }
        public override string ToString() => $"{{ Graph={GraphId}, Edge={EdgeId} }}";
    }

    public readonly struct EdgeRemovedEvent
    {
        public readonly string GraphId;
        public readonly string EdgeId;
        public EdgeRemovedEvent(string graphId, string edgeId) { GraphId = graphId; EdgeId = edgeId; }
        public override string ToString() => $"{{ Graph={GraphId}, Edge={EdgeId} }}";
    }

    public readonly struct EdgeLabelChangedEvent
    {
        public readonly string GraphId;
        public readonly string EdgeId;
        public readonly string OldLabel;
        public readonly string NewLabel;
        public EdgeLabelChangedEvent(string graphId, string edgeId, string oldLabel, string newLabel)
        { GraphId = graphId; EdgeId = edgeId; OldLabel = oldLabel; NewLabel = newLabel; }
        public override string ToString() => $"{{ Edge={EdgeId}, \"{OldLabel}\" -> \"{NewLabel}\" }}";
    }

    public readonly struct WaypointAddedEvent
    {
        public readonly string GraphId;
        public readonly string EdgeId;
        public readonly int Index;
        public WaypointAddedEvent(string graphId, string edgeId, int index)
        { GraphId = graphId; EdgeId = edgeId; Index = index; }
        public override string ToString() => $"{{ Edge={EdgeId}, Index={Index} }}";
    }

    public readonly struct WaypointRemovedEvent
    {
        public readonly string GraphId;
        public readonly string EdgeId;
        public readonly int Index;
        public WaypointRemovedEvent(string graphId, string edgeId, int index)
        { GraphId = graphId; EdgeId = edgeId; Index = index; }
        public override string ToString() => $"{{ Edge={EdgeId}, Index={Index} }}";
    }

    public readonly struct WaypointMovedEvent
    {
        public readonly string GraphId;
        public readonly string EdgeId;
        public readonly int Index;
        public readonly Vector2 From;
        public readonly Vector2 To;
        public WaypointMovedEvent(string graphId, string edgeId, int index, Vector2 from, Vector2 to)
        { GraphId = graphId; EdgeId = edgeId; Index = index; From = from; To = to; }
        public override string ToString() => $"{{ Edge={EdgeId}[{Index}], {From} -> {To} }}";
    }
}