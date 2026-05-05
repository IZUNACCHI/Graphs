using MoonSharp.Interpreter;
using NarrativeTool.Core.Runtime;
using System.Collections.Generic;

namespace NarrativeTool.Core.Scripting
{
    /// <summary>
    /// Lua scripting backend powered by MoonSharp.
    /// Pre‑registers <c>getVar(name)</c> and <c>setVar(name, value)</c>
    /// so conditions can read/write runtime state.
    /// </summary>
    public class LuaScriptingBackend : IScriptingBackend
    {
        public string LanguageName => "Lua";

        private Script moonScript;
        private IVariableAccess variables;
        private IEntityAccess entities;


        public LuaScriptingBackend(IVariableAccess variableAccess, IEntityAccess entityAccess)
        {
            variables = variableAccess;
            entities = entityAccess;
            InitialiseMoon();
        }

        private void InitialiseMoon()
        {
            moonScript = new Script();

            // --- Register get/set for variables ---
            moonScript.Globals["getVar"] = (System.Func<string, DynValue>)(name =>
            {
                object val = variables.GetValue(name);
                return DynValue.FromObject(moonScript, val);
            });

            moonScript.Globals["setVar"] = (System.Func<string, DynValue, DynValue>)((name, value) =>
            {
                variables.SetValue(name, value.ToObject());
                return DynValue.Nil;
            });

            moonScript.Globals["getEntityField"] = (System.Func<string, string, DynValue>)((entityName, fieldName) =>
            {
            object val = entities?.GetValue(entityName, fieldName);
            return DynValue.FromObject(moonScript, val);
            });

            // Stub for localization
            moonScript.Globals["loc"] = (System.Func<string, DynValue>)(key =>
            {
                // TODO: integrate with LocalizationManager
                return DynValue.NewString(key);
            });
        }

        /// <inheritdoc/>
        public bool Evaluate(string script, out object result)
        {
            result = null;
            if (string.IsNullOrEmpty(script))
            {
                UnityEngine.Debug.LogWarning("[Lua] Empty script evaluated; returning false.");
                return false;
            }

            try
            {
                // Wrap the expression in a return so MoonSharp handles it properly
                string wrapped = "return " + script;
                DynValue dynResult = moonScript.DoString(wrapped);
                result = dynResult?.ToObject();
                return true;
            }
            catch (MoonSharp.Interpreter.SyntaxErrorException ex)
            {
                UnityEngine.Debug.LogError($"[Lua] Syntax error in script: \"{script}\" — {ex.Message}");
                return false;
            }
            catch (ScriptRuntimeException ex)
            {
                UnityEngine.Debug.LogError($"[Lua] Runtime error: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public bool Validate(string script, out List<ScriptError> errors)
        {
            errors = new List<ScriptError>();
            if (string.IsNullOrEmpty(script)) return true;

            try
            {
                moonScript.LoadString(script);
                return true;
            }
            catch (MoonSharp.Interpreter.SyntaxErrorException ex)
            {
                errors.Add(new ScriptError { Message = ex.Message, Line = 0, Column = 0 });
                return false;
            }
        }
    }
}