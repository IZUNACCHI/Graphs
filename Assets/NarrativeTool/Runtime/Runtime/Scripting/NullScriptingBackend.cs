using System.Collections.Generic;
using UnityEngine;

namespace NarrativeTool.Core.Scripting
{
    public class NullScriptingBackend : IScriptingBackend
    {
        public string LanguageName => "None";

        public bool Evaluate(string script, out object result)
        {
            result = null;
            Debug.LogWarning($"[NullScripting] Evaluate called – no backend installed. Script: {script}");
            return false;
        }

        public bool Validate(string script, out List<ScriptError> errors)
        {
            errors = null;
            return true;
        }
    }
}