using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;
using UnityEngine;

namespace NarrativeTool.Core.Commands
{
    /// <summary>
    /// Sets the default value of a variable. Mergeable within a 500ms idle
    /// window so dragging a number field doesn't blow up the undo stack.
    /// </summary>
    public sealed class SetVariableDefaultCmd : ICommand
    {
        public const float MergeWindowSeconds = 0.5f;

        public string Name => $"Set default {variableId}";

        private readonly ProjectModel project;
        private readonly EventBus bus;
        private readonly string variableId;
        private object oldValue;
        private readonly object newValue;
        private readonly float timestamp;

        public SetVariableDefaultCmd(ProjectModel project, EventBus bus, string variableId,
                                     object oldValue, object newValue)
        {
            this.project = project; this.bus = bus; this.variableId = variableId;
            this.oldValue = oldValue; this.newValue = newValue;
            this.timestamp = Time.unscaledTime;
        }

        public void Do() => Apply(newValue);
        public void Undo() => Apply(oldValue);

        private void Apply(object value)
        {
            var v = project.Variables.Find(variableId);
            if (v == null) return;
            v.DefaultValue = value;
            bus.Publish(new VariableDefaultChangedEvent(project.Id, variableId));
        }

        public bool TryMerge(ICommand previous)
        {
            if (previous is SetVariableDefaultCmd prev &&
                prev.variableId == variableId &&
                ReferenceEquals(prev.project, project) &&
                (timestamp - prev.timestamp) <= MergeWindowSeconds)
            {
                oldValue = prev.oldValue;
                return true;
            }
            return false;
        }
    }
}
