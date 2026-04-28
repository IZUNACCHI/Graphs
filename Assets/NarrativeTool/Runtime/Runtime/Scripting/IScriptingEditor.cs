using UnityEngine.UIElements;

namespace NarrativeTool.Core.Scripting
{
    /// <summary>
    /// Provides a UI widget for editing a Lua script string.
    /// </summary>
    public interface IScriptingEditor
    {
        string ModeId { get; }
        string DisplayName { get; }
        /// <summary>Create and return a VisualElement for editing the script.</summary>
        VisualElement BuildUI(string initialScript);
    }
}