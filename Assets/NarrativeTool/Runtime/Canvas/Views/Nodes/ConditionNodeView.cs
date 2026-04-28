using NarrativeTool.Core;
using NarrativeTool.Core.Commands;
using NarrativeTool.Core.Scripting;
using NarrativeTool.Core.Scripting.Editors;
using NarrativeTool.Core.Widgets;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas.Views
{

    [NodeViewOf(typeof(ConditionNodeData))]
    public sealed class ConditionNodeView : NodeView
    {
        private DropdownField modeDropdown;
        private VisualElement editorContainer;
        private IScriptingEditor currentEditor;
        private FlexTextField textEditorField;
        private System.IDisposable propSub;

        public ConditionNodeView(NodeData data, GraphView canvas) : base(data, canvas) { }

        protected override void BuildCustomBody()
        {
            try
            {
                BuildCustomBodySafe();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ConditionNodeView] Failed to build body: {ex.Message}");
                extrasContainer.Add(new Label("Error building node UI. Check console."));
            }
        }

        private void BuildCustomBodySafe()
        {
            var node = (ConditionNodeData)Node;

            // Try to get the global registry; if missing, create a temporary one with TextScriptingEditor
            var registry = Services.TryGet<ScriptingEditorRegistry>();
            if (registry == null)
            {
                Debug.LogWarning("[ConditionNodeView] ScriptingEditorRegistry not registered globally. Using local fallback.");
                registry = new ScriptingEditorRegistry();
                registry.Register(new TextScriptingEditor());
            }

            // ── Mode dropdown ──
            var modeIds = registry.Editors.Select(e => e.ModeId).ToList();
            int currentIdx = modeIds.IndexOf(node.ScriptingMode);
            if (currentIdx < 0) currentIdx = 0;

            modeDropdown = new DropdownField("Mode", modeIds, currentIdx);
            modeDropdown.AddToClassList("nt-prop-field");
            extrasContainer.Add(modeDropdown);

            string oldMode = node.ScriptingMode;
            modeDropdown.RegisterValueChangedCallback(e =>
            {
                node.ScriptingMode = e.newValue;
                SwapEditor(node, registry);
            });

            modeDropdown.RegisterCallback<BlurEvent>(_ =>
            {
                if (node.ScriptingMode != oldMode)
                {
                    Canvas.Commands.Execute(new SetPropertyCommand(
                        "ScriptingMode",
                        v => node.ScriptingMode = (string)v,
                        oldMode,
                        node.ScriptingMode,
                        Canvas.Bus));
                    oldMode = node.ScriptingMode;
                }
            });

            // ── Script editor container ──
            editorContainer = new VisualElement();
            editorContainer.AddToClassList("nt-script-editor-container");
            extrasContainer.Add(editorContainer);

            SwapEditor(node, registry);

            // Subscribe to bus for external updates (undo/redo)
            propSub = Canvas.Bus.Subscribe<PropertyChangedEvent>(e =>
            {
                if (e.PropertyId == "ConditionScript")
                {
                    if (textEditorField != null && !textEditorField.IsFocused)
                        textEditorField.SetValueWithoutNotify(node.ConditionScript);
                }
                else if (e.PropertyId == "ScriptingMode")
                {
                    modeDropdown.SetValueWithoutNotify(node.ScriptingMode);
                    SwapEditor(node, registry);
                }
            });

            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                propSub?.Dispose();
            });
        }

        private void SwapEditor(ConditionNodeData node, ScriptingEditorRegistry registry)
        {
            editorContainer.Clear();
            currentEditor = registry.Get(node.ScriptingMode);
            if (currentEditor == null) return;

            var editorUI = currentEditor.BuildUI(node.ConditionScript);
            textEditorField = editorUI as FlexTextField;

            if (textEditorField != null)
            {
                textEditorField.RegisterValueChangedCallback(e =>
                    node.ConditionScript = e.newValue);

                textEditorField.OnCommit += (oldVal, newVal) =>
                {
                    Canvas.Commands.Execute(new SetPropertyCommand(
                        "ConditionScript",
                        v => node.ConditionScript = (string)v,
                        oldVal, newVal, Canvas.Bus));
                };
            }

            editorContainer.Add(editorUI);
        }
    }
}