namespace NarrativeTool.Data.Project
{
    /// <summary>
    /// One row in the project library: a recently-opened or pinned project
    /// the user can launch from the start screen. Held in
    /// <see cref="ProjectLibrary"/>.
    ///
    /// TODO persistence: this is currently in-memory only. Save/load to a
    /// JSON file under Application.persistentDataPath so the library
    /// survives editor restarts. The same loader will need to handle
    /// missing files (project moved/deleted) by surfacing a warning state
    /// on the card rather than crashing.
    /// </summary>
    public sealed class ProjectLibraryEntry
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string OpenedDisplay { get; set; }   // "2 hours ago" etc; pre-rendered for now
        public bool Pinned { get; set; }
        public int NodeCount { get; set; }
        public int EdgeCount { get; set; }
        // Hue key drives the thumbnail accent colour. Matches the keys used
        // in the mockup ("te"/"pu"/"bl"/"am"/"gr"/"rd").
        public string ThumbHueKey { get; set; } = "gr";
    }
}
