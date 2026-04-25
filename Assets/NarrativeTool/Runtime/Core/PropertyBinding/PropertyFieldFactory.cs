using System;
using NarrativeTool.Core.Commands;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Core.Widgets;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Core.Binding
{
    public static class PropertyFieldFactory
    {
        public static VisualElement Create(PropertyDefinition metadata,
                                           PropertyAccessor accessor,
                                           CommandSystem commands,
                                           EventBus bus = null)
        {
            switch (metadata.Type)
            {
                case PropertyType.String:
                    return BuildStringField(metadata, accessor, commands, bus);
                case PropertyType.Int:
                case PropertyType.Float:
                    return BuildNumericField(metadata, accessor, commands, bus);
                case PropertyType.Bool:
                    return BuildBoolField(metadata, accessor, commands, bus);
                case PropertyType.Enum:
                    return BuildEnumField(metadata, accessor, commands, bus);
                default:
                    return new Label($"Unsupported type: {metadata.Type}");
            }
        }

        // ── String (FlexTextField with local undo) ─────────────────
        private static VisualElement BuildStringField(PropertyDefinition meta,
            PropertyAccessor accessor, CommandSystem commands, EventBus bus)
        {
            var current = accessor.Getter() as string ?? "";

            var field = new FlexTextField(meta.Label, meta.Multiline);
            field.value = current;
            field.AddToClassList("nt-prop-field");
            if (meta.Multiline) field.AddToClassList("nt-prop-field--multiline");
            field.tooltip = meta.Tooltip;

            // Built‑in placeholder for strings
            if (!string.IsNullOrEmpty(meta.Placeholder))
            {
                field.textEdition.placeholder = meta.Placeholder;
                field.textEdition.hidePlaceholderOnFocus = true;
            }

            if (!meta.Editable)
            {
                field.SetEnabled(false);
                return WrapInContainer(field);
            }

            field.RegisterValueChangedCallback(evt => accessor.Setter(evt.newValue));
            field.OnCommit += (oldValue, newValue) =>
            {
                accessor.Setter(oldValue);
                commands.Execute(new SetPropertyCommand(meta.Id, accessor.Setter, oldValue, newValue, bus));
            };

            return WrapInContainer(field);
        }

        // ── Numeric (Int / Float) ───────────────────────────────────
        private static VisualElement BuildNumericField(PropertyDefinition meta,
            PropertyAccessor accessor, CommandSystem commands, EventBus bus)
        {
            VisualElement field;
            object current = accessor.Getter();

            if (meta.Type == PropertyType.Int)
            {
                int val = current is int i ? i : 0;
                var intField = new IntegerField(meta.Label) { value = val };
                intField.AddToClassList("nt-prop-field");
                intField.tooltip = meta.Tooltip;
                field = intField;

                if (!meta.Editable) { intField.SetEnabled(false); return WrapInContainer(field); }

                intField.RegisterValueChangedCallback(evt => accessor.Setter(evt.newValue));
                intField.RegisterCallback<FocusInEvent>(_ => current = accessor.Getter());
                intField.RegisterCallback<BlurEvent>(_ =>
                {
                    int newVal = (int)accessor.Getter();
                    int oldVal = Convert.ToInt32(current);
                    if (oldVal != newVal)
                    {
                        accessor.Setter(oldVal);
                        commands.Execute(new SetPropertyCommand(meta.Id, accessor.Setter, oldVal, newVal, bus));
                    }
                });
            }
            else // Float
            {
                float val = current is float f ? f : 0f;
                var floatField = new FloatField(meta.Label) { value = val };
                floatField.AddToClassList("nt-prop-field");
                floatField.tooltip = meta.Tooltip;
                field = floatField;

                if (!meta.Editable) { floatField.SetEnabled(false); return WrapInContainer(field); }

                floatField.RegisterValueChangedCallback(evt => accessor.Setter(evt.newValue));
                floatField.RegisterCallback<FocusInEvent>(_ => current = accessor.Getter());
                floatField.RegisterCallback<BlurEvent>(_ =>
                {
                    float newVal = (float)accessor.Getter();
                    float oldVal = Convert.ToSingle(current);
                    if (!Mathf.Approximately(oldVal, newVal))
                    {
                        accessor.Setter(oldVal);
                        commands.Execute(new SetPropertyCommand(meta.Id, accessor.Setter, oldVal, newVal, bus));
                    }
                });
            }

            // Apply placeholder showing the range (if both min & max are set)
            ApplyNumericPlaceholder(field, meta);

            return WrapInContainer(field);
        }

        // ── Bool ────────────────────────────────────────────────────
        private static VisualElement BuildBoolField(PropertyDefinition meta,
            PropertyAccessor accessor, CommandSystem commands, EventBus bus)
        {
            bool current = accessor.Getter() is bool b && b;
            var toggle = new Toggle(meta.Label) { value = current };
            toggle.AddToClassList("nt-prop-field");
            toggle.tooltip = meta.Tooltip;

            if (!meta.Editable) { toggle.SetEnabled(false); return WrapInContainer(toggle); }

            toggle.RegisterValueChangedCallback(evt => accessor.Setter(evt.newValue));
            bool oldValue = current;
            toggle.RegisterCallback<FocusInEvent>(_ => oldValue = accessor.Getter() is bool b && b);
            toggle.RegisterCallback<BlurEvent>(_ =>
            {
                bool newValue = accessor.Getter() is bool b && b;
                if (oldValue != newValue)
                {
                    accessor.Setter(oldValue);
                    commands.Execute(new SetPropertyCommand(meta.Id, accessor.Setter, oldValue, newValue, bus));
                }
            });

            return WrapInContainer(toggle);
        }

        // ── Enum (Dropdown) ─────────────────────────────────────────
        private static VisualElement BuildEnumField(PropertyDefinition meta,
            PropertyAccessor accessor, CommandSystem commands, EventBus bus)
        {
            if (meta.EnumOptions == null || meta.EnumOptions.Length == 0)
                return new Label("[No enum options]");

            string current = accessor.Getter()?.ToString() ?? meta.EnumOptions[0];
            var dropdown = new DropdownField(meta.Label,
                new System.Collections.Generic.List<string>(meta.EnumOptions),
                Array.IndexOf(meta.EnumOptions, current));
            dropdown.AddToClassList("nt-prop-field");
            dropdown.tooltip = meta.Tooltip;

            if (!meta.Editable) { dropdown.SetEnabled(false); return WrapInContainer(dropdown); }

            dropdown.RegisterValueChangedCallback(evt => accessor.Setter(evt.newValue));
            string oldValue = current;
            dropdown.RegisterCallback<FocusInEvent>(_ =>
                oldValue = accessor.Getter()?.ToString() ?? meta.EnumOptions[0]);
            dropdown.RegisterCallback<BlurEvent>(_ =>
            {
                string newValue = accessor.Getter()?.ToString() ?? meta.EnumOptions[0];
                if (oldValue != newValue)
                {
                    accessor.Setter(oldValue);
                    commands.Execute(new SetPropertyCommand(meta.Id, accessor.Setter, oldValue, newValue, bus));       
                }
            });

            return WrapInContainer(dropdown);
        }

        // ── Helpers ─────────────────────────────────────────────────
        private static VisualElement WrapInContainer(VisualElement field)
        {
            var container = new VisualElement();
            container.AddToClassList("nt-prop-container");
            container.Add(field);
            return container;
        }

        /// <summary>
        /// Sets the placeholder text of a numeric field's inner TextField to the
        /// allowed range (e.g., "0 – 100") if both Min and Max are explicitly set.
        /// </summary>
        private static void ApplyNumericPlaceholder(VisualElement numericField, PropertyDefinition meta)
        {
            // Only show range if both bounds are explicitly set (not default extremes)
            bool hasMin = meta.Min > float.MinValue + 0.5f;   // roughly not negative huge
            bool hasMax = meta.Max < float.MaxValue - 0.5f;
            if (!hasMin || !hasMax) return;

            // Find the inner TextField used for editing
            var textField = numericField.Q<TextField>();
            if (textField == null) return;

            string rangeText;
            if (meta.Type == PropertyType.Int)
            {
                int min = Mathf.RoundToInt(meta.Min);
                int max = Mathf.RoundToInt(meta.Max);
                rangeText = $"{min} – {max}";
            }
            else
            {
                // Format floats: if they are whole numbers, show without decimals
                bool minIsWhole = Mathf.Approximately(meta.Min % 1, 0);
                bool maxIsWhole = Mathf.Approximately(meta.Max % 1, 0);
                string minStr = minIsWhole ? meta.Min.ToString("0") : meta.Min.ToString("0.##");
                string maxStr = maxIsWhole ? meta.Max.ToString("0") : meta.Max.ToString("0.##");
                rangeText = $"{minStr} – {maxStr}";
            }

            textField.textEdition.placeholder = rangeText;
            textField.textEdition.hidePlaceholderOnFocus = true;
        }
    }
}