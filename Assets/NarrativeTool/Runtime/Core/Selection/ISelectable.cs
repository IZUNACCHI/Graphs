namespace NarrativeTool.Core.Selection
{
    /// <summary>
    /// Anything that can live in a SelectionService. Implementers react to
    /// selection-state changes by updating their own visual highlight.
    /// </summary>
    public interface ISelectable
    {
        void OnSelected();
        void OnDeselected();
    }
}