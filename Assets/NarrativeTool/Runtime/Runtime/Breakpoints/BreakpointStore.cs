using NarrativeTool.Core.EventSystem;
using System.Collections.Generic;

namespace NarrativeTool.Core.Runtime
{
    /// <summary>
    /// Single source of truth for graph-node breakpoints. Lives outside the
    /// engine so breakpoints persist across Stop / Start cycles and so the UI
    /// can mutate them without coupling to <see cref="RuntimeEngine"/>.
    ///
    /// A breakpoint is keyed by (graphId, nodeId) and either active or
    /// disabled. Disabled breakpoints stay in the list (so the user can
    /// re-enable them) but do not pause execution.
    /// </summary>
    public sealed class BreakpointStore
    {
        public readonly struct Key
        {
            public readonly string GraphId;
            public readonly string NodeId;
            public Key(string graphId, string nodeId) { GraphId = graphId; NodeId = nodeId; }
            public override bool Equals(object obj) =>
                obj is Key k && k.GraphId == GraphId && k.NodeId == NodeId;
            public override int GetHashCode() =>
                (GraphId, NodeId).GetHashCode();
        }

        private readonly EventBus bus;
        private readonly HashSet<Key> all = new();
        private readonly HashSet<Key> disabled = new();

        public BreakpointStore(EventBus bus) { this.bus = bus; }

        public bool Has(string graphId, string nodeId) =>
            all.Contains(new Key(graphId, nodeId));

        public bool IsEnabled(string graphId, string nodeId)
        {
            var k = new Key(graphId, nodeId);
            return all.Contains(k) && !disabled.Contains(k);
        }

        /// <summary>Returns true if the breakpoint is set AND enabled (i.e. should pause).</summary>
        public bool IsActive(string graphId, string nodeId) => IsEnabled(graphId, nodeId);

        public void Add(string graphId, string nodeId)
        {
            var k = new Key(graphId, nodeId);
            if (all.Add(k))
            {
                disabled.Remove(k);
                bus?.Publish(new BreakpointAddedEvent(graphId, nodeId));
            }
        }

        public void Remove(string graphId, string nodeId)
        {
            var k = new Key(graphId, nodeId);
            if (all.Remove(k))
            {
                disabled.Remove(k);
                bus?.Publish(new BreakpointRemovedEvent(graphId, nodeId));
            }
        }

        public void Toggle(string graphId, string nodeId)
        {
            if (Has(graphId, nodeId))
                Remove(graphId, nodeId);
            else
                Add(graphId, nodeId);
        }

        public void SetEnabled(string graphId, string nodeId, bool enabled)
        {
            var k = new Key(graphId, nodeId);
            if (!all.Contains(k)) return;
            bool changed = enabled ? disabled.Remove(k) : disabled.Add(k);
            if (changed)
                bus?.Publish(new BreakpointToggledEvent(graphId, nodeId, enabled));
        }

        public void Clear()
        {
            var snapshot = new List<Key>(all);
            all.Clear();
            disabled.Clear();
            foreach (var k in snapshot)
                bus?.Publish(new BreakpointRemovedEvent(k.GraphId, k.NodeId));
        }

        public IEnumerable<Key> GetAll() => all;

        public int Count => all.Count;
        public int ActiveCount => all.Count - disabled.Count;
    }
}
