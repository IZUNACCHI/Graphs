using System.Collections.Generic;

namespace NarrativeTool.Core.Scripting
{
    public interface IScriptingBackend
    {
        string LanguageName { get; }
        bool Evaluate(string script, out object result);
        bool Validate(string script, out List<ScriptError> errors);
    }
}