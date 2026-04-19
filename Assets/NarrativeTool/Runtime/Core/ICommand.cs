namespace NarrativeTool.Core
{
    /// <summary>
    /// The unit of undo/redo. Commands encapsulate a reversible mutation.
    ///
    /// Merging: continuous operations (e.g. dragging a node) produce many commands;
    /// implement TryMerge to collapse them into one on the undo stack.
    /// </summary>
    public interface ICommand
    {
        /// <summary>Human-readable name shown in the undo menu / logs.</summary>
        string Name { get; }

        /// <summary>Apply the mutation.</summary>
        void Do();

        /// <summary>Reverse the mutation. Must restore state exactly.</summary>
        void Undo();

        /// <summary>
        /// If this command can swallow the previous one on the stack (e.g. two
        /// consecutive MoveNodeCmds on the same node), mutate `this` to represent
        /// the combined operation and return true. The CommandSystem will discard
        /// `previous`. Return false if merging is not possible.
        /// </summary>
        bool TryMerge(ICommand previous);
    }
}