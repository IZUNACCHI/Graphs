using System;

namespace NarrativeTool.UI.Docking
{
    public enum DockZoneKind { Left, Right, Bottom, Center }

    /// <summary>
    /// Describes how to instantiate / mount a dockable panel. Stored in
    /// <see cref="DockRegistry"/>; consumed by <see cref="DockRoot"/> when laying
    /// out the default arrangement and by the Settings menu when re-opening a
    /// closed panel.
    /// </summary>
    public sealed class DockablePanelDescriptor
    {
        public string Id;
        public string Title;
        public Func<IDockablePanel> Factory;
        public DockZoneKind DefaultZone = DockZoneKind.Left;
        public int DefaultOrder;
    }
}
