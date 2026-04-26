using System.Collections.Generic;

namespace NarrativeTool.Data.Project
{
    /// <summary>
    /// In-memory list of projects shown on the library start screen. Bound
    /// to <see cref="UI.Library.LibraryScreen"/>.
    ///
    /// TODO persistence: load/save to a JSON file under persistentDataPath
    /// (or the IDE-equivalent prefs store) so pins, opened-at timestamps,
    /// and recents survive across launches.
    /// </summary>
    public sealed class ProjectLibrary
    {
        public List<ProjectLibraryEntry> Entries { get; } = new();

        public void TogglePin(string path)
        {
            foreach (var e in Entries)
                if (e.Path == path) { e.Pinned = !e.Pinned; return; }
        }

        /// <summary>
        /// Push (or move-to-top) a freshly-opened project. Called on
        /// open/create so the most-recent entry is always first.
        /// </summary>
        public void RegisterOpened(ProjectLibraryEntry entry)
        {
            for (int i = 0; i < Entries.Count; i++)
                if (Entries[i].Path == entry.Path) { Entries.RemoveAt(i); break; }
            Entries.Insert(0, entry);
        }
    }
}
