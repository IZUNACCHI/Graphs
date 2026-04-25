using NarrativeTool.Core.Selection;
using System.Collections.Generic;

namespace NarrativeTool.Core.Commands
{
    /// <summary>
    /// A single atomic selection transition. Carries the before and after
    /// sets; Do applies 'after', Undo applies 'before'. Does not merge.
    /// </summary>
    public sealed class SetSelectionCmd : ICommand
    {
        public string Name => $"Selection ({before.Count} -> {after.Count})";

        private readonly SelectionService service;
        private readonly HashSet<ISelectable> before;
        private readonly HashSet<ISelectable> after;

        public SetSelectionCmd(SelectionService service,
                               HashSet<ISelectable> before,
                               HashSet<ISelectable> after)
        {
            this.service = service;
            this.before = before;
            this.after = after;
        }

        public void Do() => service.ApplyDirect(after);
        public void Undo() => service.ApplyDirect(before);
        public bool TryMerge(ICommand previous) => false;
    }
}