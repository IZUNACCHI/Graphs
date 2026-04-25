using System.Collections.Generic;

namespace NarrativeTool.Core.ContextMenu
{
    /// <summary>
    /// Contributes items to a context menu. Return null / empty if the
    /// target isn't something this provider handles.
    /// </summary>
    public interface IContextMenuProvider
    {
        IReadOnlyList<ContextMenuItem> GetItemsFor(object target);
    }
}