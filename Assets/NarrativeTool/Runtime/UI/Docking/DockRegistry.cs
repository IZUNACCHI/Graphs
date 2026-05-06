using System;
using System.Collections.Generic;

namespace NarrativeTool.UI.Docking
{
    /// <summary>
    /// Global registry of dockable-panel descriptors. Populated at MainWindow
    /// construction; read by <see cref="DockRoot"/> to build the default layout
    /// and by the Settings menu when toggling panel visibility.
    /// </summary>
    public static class DockRegistry
    {
        private static readonly List<DockablePanelDescriptor> descriptors = new();

        public static event Action Changed;

        public static IReadOnlyList<DockablePanelDescriptor> All => descriptors;

        public static void Register(DockablePanelDescriptor d)
        {
            if (d == null || string.IsNullOrEmpty(d.Id)) return;
            descriptors.RemoveAll(x => x.Id == d.Id);
            descriptors.Add(d);
            Changed?.Invoke();
        }

        public static DockablePanelDescriptor Find(string id)
            => descriptors.Find(x => x.Id == id);

        public static void Clear()
        {
            if (descriptors.Count == 0) return;
            descriptors.Clear();
            Changed?.Invoke();
        }
    }
}
