using System.Collections.Generic;
using System.Linq;

namespace NarrativeTool.Core.Scripting
{
    public sealed class ScriptingEditorRegistry
    {
        private readonly Dictionary<string, IScriptingEditor> editors = new();

        public void Register(IScriptingEditor editor)
        {
            if (editor == null) return;
            editors[editor.ModeId] = editor;
        }

        public IScriptingEditor Get(string modeId)
        {
            editors.TryGetValue(modeId ?? "", out var editor);
            return editor ?? editors.Values.FirstOrDefault();
        }

        public IReadOnlyList<IScriptingEditor> Editors => editors.Values.ToList();
    }
}