using NarrativeTool.Data.Project;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Debugger
{
    /// <summary>
    /// Lightweight modal that asks the user for a new value of a typed
    /// variable / entity field, then commits it via the supplied callback.
    /// Mirrors the type-specific input controls used by VariablesPanel.
    /// </summary>
    public sealed class ValueOverrideModal : VisualElement
    {
        private readonly Action<object> onCommit;
        private readonly VariableType type;
        private readonly ProjectModel project;
        private readonly string enumTypeId;

        private IntegerField intField;
        private FloatField floatField;
        private Toggle boolField;
        private TextField stringField;
        private DropdownField enumField;

        public ValueOverrideModal(string targetName, VariableType type, object current,
            string enumTypeId, ProjectModel project, Action<object> onCommit)
        {
            this.type = type;
            this.enumTypeId = enumTypeId;
            this.project = project;
            this.onCommit = onCommit;

            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            style.backgroundColor = new Color(0, 0, 0, 0.5f);
            style.justifyContent = Justify.Center;
            style.alignItems = Align.Center;

            RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.target == this) Dismiss();
            });
            RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Escape) { Dismiss(); e.StopPropagation(); }
                else if (e.keyCode == KeyCode.Return) { Commit(); e.StopPropagation(); }
            });

            var box = new VisualElement();
            box.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            box.style.borderTopLeftRadius = 6; box.style.borderTopRightRadius = 6;
            box.style.borderBottomLeftRadius = 6; box.style.borderBottomRightRadius = 6;
            box.style.paddingTop = 12; box.style.paddingBottom = 12;
            box.style.paddingLeft = 16; box.style.paddingRight = 16;
            box.style.minWidth = 260;
            box.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());

            var title = new Label($"Set runtime value · {targetName}");
            title.style.color = Color.white;
            title.style.fontSize = 11;
            title.style.marginBottom = 8;
            box.Add(title);

            box.Add(BuildInputField(current));

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 10;

            var cancel = new Button(Dismiss) { text = "Cancel" };
            cancel.AddToClassList("nt-btn");
            cancel.AddToClassList("nt-btn--normal");
            buttons.Add(cancel);

            var ok = new Button(Commit) { text = "Set" };
            ok.AddToClassList("nt-btn");
            ok.AddToClassList("nt-btn--primary");
            buttons.Add(ok);

            box.Add(buttons);
            Add(box);

            focusable = true;
            schedule.Execute(() => Focus()).StartingIn(0);
        }

        private VisualElement BuildInputField(object current)
        {
            switch (type)
            {
                case VariableType.Int:
                    intField = new IntegerField { value = current is int i ? i : 0 };
                    return intField;
                case VariableType.Float:
                    floatField = new FloatField { value = current is float f ? f : 0f };
                    return floatField;
                case VariableType.Bool:
                    boolField = new Toggle { value = current is bool b && b };
                    return boolField;
                case VariableType.String:
                    stringField = new TextField { value = current as string ?? "" };
                    return stringField;
                case VariableType.Enum:
                    {
                        var def = project?.Enums.Items.FirstOrDefault(e => e.Id == enumTypeId);
                        if (def == null || def.Members.Count == 0)
                            return new Label("(enum has no members)");
                        var names = def.Members.Select(m => m.Name).ToList();
                        int idx = def.Members.FindIndex(m => m.Id == (current as string));
                        enumField = new DropdownField(names, Mathf.Max(0, idx));
                        return enumField;
                    }
                default:
                    return new Label("(unsupported type)");
            }
        }

        private void Commit()
        {
            object value = type switch
            {
                VariableType.Int => intField?.value ?? 0,
                VariableType.Float => floatField?.value ?? 0f,
                VariableType.Bool => boolField?.value ?? false,
                VariableType.String => stringField?.value ?? "",
                VariableType.Enum => ResolveEnumId(),
                _ => null,
            };
            onCommit?.Invoke(value);
            RemoveFromHierarchy();
        }

        private object ResolveEnumId()
        {
            if (enumField == null) return null;
            var def = project?.Enums.Items.FirstOrDefault(e => e.Id == enumTypeId);
            var member = def?.Members.FirstOrDefault(m => m.Name == enumField.value);
            return member?.Id;
        }

        private void Dismiss() => RemoveFromHierarchy();
    }
}
