using UnityEngine;

namespace NarrativeTool.Data
{
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