namespace NarrativeTool.Core.EventSystem
{
    /// <summary>
    /// Published by <see cref="NarrativeTool.Core.Runtime.RuntimeVariableStore"/>
    /// every time a variable's runtime value changes (either by a node executor
    /// or by manual override from the Debugger Watch panel).
    /// </summary>
    public readonly struct VariableRuntimeValueChangedEvent
    {
        public readonly string Name;
        public readonly object OldValue;
        public readonly object NewValue;
        public VariableRuntimeValueChangedEvent(string name, object oldValue, object newValue)
        { Name = name; OldValue = oldValue; NewValue = newValue; }
        public override string ToString() => $"{{ Var={Name}, {OldValue} -> {NewValue} }}";
    }

    /// <summary>
    /// Published by <see cref="NarrativeTool.Core.Runtime.RuntimeEntityStore"/>
    /// every time an entity field's runtime value changes.
    /// </summary>
    public readonly struct EntityRuntimeValueChangedEvent
    {
        public readonly string EntityName;
        public readonly string FieldName;
        public readonly object OldValue;
        public readonly object NewValue;
        public EntityRuntimeValueChangedEvent(string entityName, string fieldName, object oldValue, object newValue)
        { EntityName = entityName; FieldName = fieldName; OldValue = oldValue; NewValue = newValue; }
        public override string ToString() => $"{{ Entity={EntityName}.{FieldName}, {OldValue} -> {NewValue} }}";
    }

    /// <summary>
    /// Published by <see cref="NarrativeTool.Core.Runtime.RuntimeEngine"/> when
    /// execution is paused due to an active breakpoint on the node about to run.
    /// </summary>
    public readonly struct BreakpointHitEvent
    {
        public readonly string GraphId;
        public readonly string NodeId;
        public BreakpointHitEvent(string graphId, string nodeId)
        { GraphId = graphId; NodeId = nodeId; }
        public override string ToString() => $"{{ Breakpoint hit: Graph={GraphId}, Node={NodeId} }}";
    }

    public readonly struct BreakpointAddedEvent
    {
        public readonly string GraphId;
        public readonly string NodeId;
        public BreakpointAddedEvent(string graphId, string nodeId)
        { GraphId = graphId; NodeId = nodeId; }
        public override string ToString() => $"{{ +BP: Graph={GraphId}, Node={NodeId} }}";
    }

    public readonly struct BreakpointRemovedEvent
    {
        public readonly string GraphId;
        public readonly string NodeId;
        public BreakpointRemovedEvent(string graphId, string nodeId)
        { GraphId = graphId; NodeId = nodeId; }
        public override string ToString() => $"{{ -BP: Graph={GraphId}, Node={NodeId} }}";
    }

    public readonly struct BreakpointToggledEvent
    {
        public readonly string GraphId;
        public readonly string NodeId;
        public readonly bool Enabled;
        public BreakpointToggledEvent(string graphId, string nodeId, bool enabled)
        { GraphId = graphId; NodeId = nodeId; Enabled = enabled; }
        public override string ToString() => $"{{ BP toggled: Graph={GraphId}, Node={NodeId}, Enabled={Enabled} }}";
    }
}
