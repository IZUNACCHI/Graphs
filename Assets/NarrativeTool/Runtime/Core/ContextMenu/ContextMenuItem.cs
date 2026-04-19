using System;

namespace NarrativeTool.Core
{
    /// <summary>
    /// One entry in a context menu. Either actionable (Label + Action) or a
    /// horizontal separator.
    /// </summary>
    public sealed class ContextMenuItem
    {
        public string Label { get; set; }
        public Action Action { get; set; }
        public bool Enabled { get; set; } = true;
        public bool IsSeparator { get; set; }

        public static ContextMenuItem Separator() => new() { IsSeparator = true };

        public static ContextMenuItem Of(string label, Action action, bool enabled = true)
            => new() { Label = label, Action = action, Enabled = enabled };
    }
}