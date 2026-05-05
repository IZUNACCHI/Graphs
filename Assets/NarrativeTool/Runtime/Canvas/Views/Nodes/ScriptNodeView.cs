using NarrativeTool.Core;
using NarrativeTool.Core.Commands;
using NarrativeTool.Core.Scripting;
using NarrativeTool.Core.Scripting.Editors;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using System.Linq;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas.Views
{
    [NodeViewOf(typeof(ScriptNodeData))]
    public sealed class ScriptNodeView : NodeView
    {
        private DropdownField modeDropdown;
        private System.IDisposable propSub;

        public ScriptNodeView(NodeData data, GraphView canvas) : base(data, canvas) { }

        protected override void BuildCustomBody()
        {
            var node = (ScriptNodeData)Node;
            var registry = Services.TryGet<ScriptingEditorRegistry>();
            if (registry == null)
            {
                registry = new ScriptingEditorRegistry();
                registry.Register(new TextScriptingEditor());
            }

            // Replace the auto‑generated "Mode" text field with a dropdown
            if (propWidgets.TryGetValue("ScriptingMode", out var autoWidget))
            {
                extrasContainer.Remove(autoWidget);
                propWidgets.Remove("ScriptingMode");
            }

            var modeIds = registry.Editors.Select(e => e.ModeId).ToList();
            int currentIdx = modeIds.IndexOf(node.ScriptingMode);
            if (currentIdx < 0) currentIdx = 0;

            modeDropdown = new DropdownField("Mode", modeIds, currentIdx);
            modeDropdown.AddToClassList("nt-prop-field");
            extrasContainer.Insert(1, modeDropdown);

            string oldMode = node.ScriptingMode;
            modeDropdown.RegisterValueChangedCallback(e =>
            {
                node.ScriptingMode = e.newValue;
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

            propSub = Canvas.Bus.Subscribe<PropertyChangedEvent>(e =>
            {
                if (e.PropertyId == "ScriptingMode")
                    modeDropdown.SetValueWithoutNotify(node.ScriptingMode);
            });

            RegisterCallback<DetachFromPanelEvent>(_ => propSub?.Dispose());

            // Store the dropdown back so the base view can update it on undo/redo
            propWidgets["ScriptingMode"] = modeDropdown;
        }
    }
}