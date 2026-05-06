using UnityEngine.UIElements;

namespace NarrativeTool.UI.Docking
{
    /// <summary>
    /// Wraps an existing VisualElement-based panel (GraphsPanel, VariablesPanel…)
    /// as an <see cref="IDockablePanel"/> so it can be docked without modifying
    /// each panel class directly. Phase 3 may push <see cref="IDockablePanel"/>
    /// onto each panel directly.
    /// </summary>
    public sealed class DockablePanelAdapter : IDockablePanel
    {
        public string Id { get; }
        public string Title { get; }
        public VisualElement Content { get; }
        public bool IsCloseable { get; }
        public bool IsPinnedCenter { get; }

        public DockablePanelAdapter(string id, string title, VisualElement content,
                                    bool isCloseable = true, bool isPinnedCenter = false)
        {
            Id = id;
            Title = title;
            Content = content;
            IsCloseable = isCloseable;
            IsPinnedCenter = isPinnedCenter;
        }
    }
}
