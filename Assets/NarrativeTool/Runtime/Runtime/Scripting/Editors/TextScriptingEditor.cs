using NarrativeTool.Core.Widgets;
using UnityEngine.UIElements;

namespace NarrativeTool.Core.Scripting.Editors
{
    public class TextScriptingEditor : IScriptingEditor
    {
        public string ModeId => "text";
        public string DisplayName => "Text";

        public VisualElement BuildUI(string initialScript)
        {
            var field = new FlexTextField("Script", multiline: true)
            {
                value = initialScript
            };
            field.AddToClassList("nt-script-editor");
            return field;
        }
    }
}