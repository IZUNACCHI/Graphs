using System.Collections.Generic;
using NarrativeTool.Core.Commands;
using NarrativeTool.Core.EventSystem;

namespace NarrativeTool.Core.Selection
{
    /// <summary>
    /// Per-graph selection set. Reference-based — holds ISelectable objects
    /// directly.
    ///
    /// Public mutations (Select/Deselect/Toggle/SelectOnly/Clear) route
    /// through the command system: each call pushes a single
    /// SetSelectionCmd. Ctrl+Z reverts selection changes (Unreal-BP style).
    ///
    /// Fires SelectionChangedEvent on the EventBus, tagged with the owner
    /// reference so cross-dock consumers can filter.
    ///
    /// TODO (docking): ClearSelectionOnBlur flag on the owning dock.
    /// </summary>
    public sealed class SelectionService
    {
        private readonly HashSet<ISelectable> selected = new();
        private readonly EventBus bus;
        private readonly CommandSystem commands;
        private readonly object owner;

        public object Owner => owner;
        public IReadOnlyCollection<ISelectable> Selected => selected;
        public int Count => selected.Count;

        public SelectionService(object owner, EventBus bus, CommandSystem commands)
        {
            this.owner = owner;
            this.bus = bus;
            this.commands = commands;
        }

        public bool IsSelected(ISelectable item) => item != null && selected.Contains(item);

        public void Select(ISelectable item)
        {
            if (item == null || selected.Contains(item)) return;
            var next = new HashSet<ISelectable>(selected) { item };
            PushTransition(next);
        }

        public void Deselect(ISelectable item)
        {
            if (item == null || !selected.Contains(item)) return;
            var next = new HashSet<ISelectable>(selected);
            next.Remove(item);
            PushTransition(next);
        }

        public void Toggle(ISelectable item)
        {
            if (item == null) return;
            var next = new HashSet<ISelectable>(selected);
            if (!next.Add(item)) next.Remove(item);
            PushTransition(next);
        }

        public void SelectOnly(ISelectable item)
        {
            var next = new HashSet<ISelectable>();
            if (item != null) next.Add(item);
            if (SetsEqual(next, selected)) return;
            PushTransition(next);
        }

        public void Clear()
        {
            if (selected.Count == 0) return;
            PushTransition(new HashSet<ISelectable>());
        }

        public void SelectSet(IEnumerable<ISelectable> items)
        {
            var next = new HashSet<ISelectable>();
            foreach (var i in items) if (i != null) next.Add(i);
            if (SetsEqual(next, selected)) return;
            PushTransition(next);
        }

        public List<ISelectable> Snapshot() => new(selected);

        /// <summary>
        /// Replace the selection WITHOUT going through the command system.
        /// Used by SetSelectionCmd's Do/Undo (avoids infinite recursion) and
        /// by canvas rebind logic (no undo entry desired).
        /// </summary>
        public void ApplyDirect(IReadOnlyCollection<ISelectable> newSet)
        {
            var leaving = new List<ISelectable>();
            foreach (var s in selected)
                if (newSet == null || !Contains(newSet, s))
                    leaving.Add(s);
            foreach (var s in leaving) { selected.Remove(s); s.OnDeselected(); }

            if (newSet != null)
            {
                foreach (var s in newSet)
                {
                    if (s == null) continue;
                    if (selected.Add(s)) s.OnSelected();
                }
            }

            bus?.Publish(new SelectionChangedEvent(owner, selected.Count));
        }

        public HashSet<ISelectable> CurrentSet() => new(selected);

        private void PushTransition(HashSet<ISelectable> next)
        {
            var prev = new HashSet<ISelectable>(selected);
            commands.Execute(new SetSelectionCmd(this, prev, next));
        }

        private static bool SetsEqual(HashSet<ISelectable> a, HashSet<ISelectable> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var x in a) if (!b.Contains(x)) return false;
            return true;
        }

        private static bool Contains(IReadOnlyCollection<ISelectable> coll, ISelectable target)
        {
            foreach (var x in coll) if (ReferenceEquals(x, target)) return true;
            return false;
        }
    }

    public readonly struct SelectionChangedEvent
    {
        public readonly object Owner;
        public readonly int Count;
        public SelectionChangedEvent(object owner, int count) { Owner = owner; Count = count; }
        public override string ToString() => $"{{ Owner={Owner}, Count={Count} }}";
    }
}