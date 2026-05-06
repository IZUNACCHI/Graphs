using UnityEngine.UIElements;

namespace NarrativeTool.UI.Docking
{
    /// <summary>
    /// A panel that can live inside a <see cref="DockArea"/>. The panel owns its
    /// content; the dock framework only manages where that content is parented.
    /// </summary>
    public interface IDockablePanel
    {
        /// <summary>Stable id used for layout persistence and registry lookups.</summary>
        string Id { get; }

        /// <summary>Display label for the tab header.</summary>
        string Title { get; }

        /// <summary>Root visual element. The dock framework re-parents this as the
        /// panel is moved across areas.</summary>
        VisualElement Content { get; }

        /// <summary>If false the close button is hidden and Settings → toggle is
        /// the only way to remove the panel.</summary>
        bool IsCloseable { get; }

        /// <summary>If true the panel may only live inside the center zone (e.g.
        /// the graph editor). Dock drag-drop hides drop targets in side zones for
        /// pinned panels.</summary>
        bool IsPinnedCenter { get; }
    }
}
