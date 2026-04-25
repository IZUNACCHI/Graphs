namespace NarrativeTool.Core.Commands
{
    /// <summary>
    /// The unit of undo/redo. Commands encapsulate a reversible mutation.
    /// Merging lets continuous operations (e.g. dragging) collapse into a
    /// single undo entry.
    /// </summary>
    public interface ICommand
    {
        string Name { get; }
        void Do();
        void Undo();
        bool TryMerge(ICommand previous);
    }
}