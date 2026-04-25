// NarrativeTool.Core/Commands/SetPropertyCommand.cs
using NarrativeTool.Core.EventSystem;
using System;

namespace NarrativeTool.Core.Commands
{
    /// <summary>
    /// Stores an undoable property change. The setter is used to apply the value.
    /// The property is identified by a string ID (C# property name or dynamic definition ID).
    /// </summary>
    public sealed class SetPropertyCommand : ICommand
    {
        private readonly string propertyId;
        private readonly Action<object> setter;
        private readonly object oldValue;
        private readonly object newValue;
        private readonly EventBus eventBus;
        private readonly string graphId;

        public string Name => $"Set '{propertyId}'";

        public SetPropertyCommand(string propertyId, Action<object> setter,
                                  object oldValue, object newValue, EventBus eventBus = null) 
        {
            this.propertyId = propertyId;
            this.setter = setter;
            this.oldValue = oldValue;
            this.newValue = newValue;
            this.eventBus = eventBus;
        }

        public void Do()
        {
            setter(newValue);
            eventBus?.Publish(new PropertyChangedEvent(propertyId, oldValue, newValue));
        }

        public void Undo()
        {
            setter(oldValue);
            eventBus?.Publish(new PropertyChangedEvent(propertyId, newValue, oldValue));
        }

        public bool TryMerge(ICommand previous)
        {
            // Merge if same property ID and previous command is the same type.
            return previous is SetPropertyCommand prev && prev.propertyId == propertyId;
        }
    }

    /// <summary>
    /// Lightweight event published when a property changes.
    /// </summary>
    public readonly struct PropertyChangedEvent
    {
        public readonly string PropertyId;
        public readonly object OldValue;
        public readonly object NewValue;
        public readonly string GraphId;   // nullable
        public PropertyChangedEvent(string propertyId, object oldValue, object newValue)
        {
            PropertyId = propertyId;
            OldValue = oldValue;
            NewValue = newValue;
            GraphId = null;
        }
        public override string ToString() => $"{PropertyId}: {OldValue} to {NewValue}";
    }
}