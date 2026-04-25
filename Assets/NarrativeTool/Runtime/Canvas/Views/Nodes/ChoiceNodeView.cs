using NarrativeTool.Core.Commands;
using NarrativeTool.Core.Widgets;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas.Views
{
    [NodeViewOf(typeof(ChoiceNodeData))]
    public sealed class ChoiceNodeView : NodeView
    {
        private VisualElement preambleToggleSection;
        private Button preambleToggleBtn;
        private VisualElement preambleBody;
        private VisualElement optionsList;
        private Button addOptionBtn;

        private bool preambleVisible;
        private Dictionary<ChoiceOption, VisualElement> optionRows = new();

        public ChoiceNodeView(NodeData node, GraphView canvas) : base(node, canvas)
        {
            // After auto‑property builders run, we’ll group preamble fields.
            schedule.Execute(() =>
            {
                GroupPreambleFields();
                UpdatePreambleUI();
            }).StartingIn(0);
        }

        protected override void BuildCustomBody()
        {
            var data = (ChoiceNodeData)Node;

            // ── Preamble toggle section ──
            preambleToggleSection = new VisualElement();
            preambleToggleSection.AddToClassList("nt-preamble-toggle");
            extrasContainer.Add(preambleToggleSection);

            var leftLine = new VisualElement(); leftLine.AddToClassList("nt-preamble-toggle-line");
            var rightLine = new VisualElement(); rightLine.AddToClassList("nt-preamble-toggle-line");

            preambleToggleBtn = new Button(TogglePreamble) { text = "Preamble" };
            preambleToggleBtn.AddToClassList("nt-preamble-toggle-btn");

            preambleToggleSection.Add(leftLine);
            preambleToggleSection.Add(preambleToggleBtn);
            preambleToggleSection.Add(rightLine);

            // ── Preamble body (fields will be moved here by GroupPreambleFields) ──
            preambleBody = new VisualElement();
            preambleBody.AddToClassList("nt-preamble-body");
            extrasContainer.Add(preambleBody);

            // ── Options list ──
            optionsList = new VisualElement();
            optionsList.AddToClassList("nt-option-list");
            extrasContainer.Add(optionsList);

            addOptionBtn = new Button(AddOption) { text = "+ Add option" };
            addOptionBtn.AddToClassList("nt-option-add-btn");
            extrasContainer.Add(addOptionBtn);

            // Build rows for existing options
            for (int i = 0; i < data.Options.Count; i++)
                AddOptionRow(data.Options[i]);
        }

        // ── Preamble grouping ──
        private void GroupPreambleFields()
        {
            if (preambleBody == null) return;

            // Move the auto‑generated property fields for Speaker, StageDirections, DialogueText into the preamble body
            if (propWidgets.TryGetValue("Speaker", out var sp))
                preambleBody.Add(sp);
            if (propWidgets.TryGetValue("StageDirections", out var sd))
                preambleBody.Add(sd);
            if (propWidgets.TryGetValue("DialogueText", out var dt))
                preambleBody.Add(dt);
        }

        private void TogglePreamble()
        {
            var data = (ChoiceNodeData)Node;
            var oldVal = data.HasPreamble;
            var newVal = !oldVal;

            Canvas.Commands.Execute(new SetPropertyCommand("HasPreamble",
                v => data.HasPreamble = (bool)v,
                oldVal, newVal,
                Canvas.Bus));

            // Immediate UI update
            UpdatePreambleUI();
        }

        public void UpdatePreambleUI()
        {
            var data = (ChoiceNodeData)Node;
            if (preambleToggleBtn != null)
            {
                if (data.HasPreamble)
                    preambleToggleBtn.AddToClassList("nt-preamble-toggle-btn--active");
                else
                    preambleToggleBtn.RemoveFromClassList("nt-preamble-toggle-btn--active");
            }
            if (preambleBody != null)
                preambleBody.style.display = data.HasPreamble ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ── Option management (public for context menu) ──
        public void AddOption()
        {
            var data = (ChoiceNodeData)Node;
            int index = data.Options.Count;
            var option = new ChoiceOption
            {
                Id = $"opt_{System.Guid.NewGuid():N}".Substring(0, 8),
                Label = "New Option",
                ConditionScript = "",
                HideIfFalse = true,
                PortId = $"out_{index}"   // will be adjusted if reordering, but unique enough
            };

            Canvas.Commands.Execute(new AddChoiceOptionCmd(data, index, option,
                onDo: () => AddOptionRow(option, index),
                onUndo: () => RemoveOptionRow(option)));
        }

        private void RemoveOption(ChoiceOption option)
        {
            var data = (ChoiceNodeData)Node;
            int index = data.Options.IndexOf(option);
            if (index < 0) return;

            // Show confirmation bar instead of immediate removal
            var row = optionRows[option];
            ShowDeleteConfirmation(row, option, index);
        }

        private void ConfirmRemoveOption(ChoiceOption option, int index)
        {
            var data = (ChoiceNodeData)Node;
            Canvas.Commands.Execute(new RemoveChoiceOptionCmd(data, index, option,
                onDo: () => RemoveOptionRow(option),
                onUndo: () => AddOptionRow(option, index)));
        }

        private void MoveOption(ChoiceOption option, int delta)
        {
            var data = (ChoiceNodeData)Node;
            int index = data.Options.IndexOf(option);
            int newIndex = index + delta;
            if (newIndex < 0 || newIndex >= data.Options.Count) return;

            // Swap via command
            Canvas.Commands.Execute(new MoveOptionCmd(data, index, newIndex));
            // Reorder rows visually
            RebuildOptionRows();
        }

        // ── Row lifecycle ──
        private void AddOptionRow(ChoiceOption option, int index = -1)
        {
            if (index < 0) index = ((ChoiceNodeData)Node).Options.IndexOf(option);

            var row = BuildOptionRow(option, index);

            // Insert at correct position
            if (index >= optionsList.childCount)
                optionsList.Add(row);
            else
                optionsList.Insert(index, row);

            optionRows[option] = row;

            // Add output port (if not already)
            if (!portViews.ContainsKey(option.PortId))
            {
                var portData = new PortData(option.PortId, option.Label,
                    PortDirection.Output, PortCapacity.Single, "flow");
                var portView = new PortView(portData) { OwnerNode = this };
                portViews[option.PortId] = portView;
                // Add the port to the .nt-option container (positioned absolutely)
                row.Add(portView);
            }
        }

        private void RemoveOptionRow(ChoiceOption option)
        {
            if (optionRows.TryGetValue(option, out var row))
            {
                row.RemoveFromHierarchy();
                optionRows.Remove(option);
                if (portViews.TryGetValue(option.PortId, out var pv))
                {
                    pv.RemoveFromHierarchy();
                    portViews.Remove(option.PortId);
                }
            }
        }

        private void RebuildOptionRows()
        {
            // Clear all rows, rebuild from data
            optionsList.Clear();
            optionRows.Clear();
            var data = (ChoiceNodeData)Node;
            for (int i = 0; i < data.Options.Count; i++)
                AddOptionRow(data.Options[i], i);
        }

        // ── Row construction ──
        private VisualElement BuildOptionRow(ChoiceOption option, int index)
        {
            var data = (ChoiceNodeData)Node;

            var container = new VisualElement();
            container.AddToClassList("nt-option");
            container.userData = option;

            // ── Main row ──
            var row = new VisualElement();
            row.AddToClassList("nt-option-row");

            // Sort arrows
            var sortCol = new VisualElement();
            sortCol.AddToClassList("nt-option-sort");

            var upBtn = new Button(() => MoveOption(option, -1));
            upBtn.text = "▲";
            upBtn.AddToClassList("nt-option-sort-btn");
            upBtn.SetEnabled(index > 0);
            sortCol.Add(upBtn);

            var downBtn = new Button(() => MoveOption(option, 1));
            downBtn.text = "▼";
            downBtn.AddToClassList("nt-option-sort-btn");
            downBtn.SetEnabled(index < data.Options.Count - 1);
            sortCol.Add(downBtn);
            row.Add(sortCol);

            // Option text field
            var textField = new FlexTextField(multiline: false) { value = option.Label };
            textField.AddToClassList("nt-option-text");
            textField.RegisterValueChangedCallback(evt => option.Label = evt.newValue);
            textField.OnCommit += (oldVal, newVal) =>
            {
                option.Label = oldVal; // revert for command
                Canvas.Commands.Execute(new SetPropertyCommand("Option Label",
                    v => option.Label = (string)v, oldVal, newVal,
                    Canvas.Bus));
            };
            row.Add(textField);

            // Condition toggle button
            var condBtn = new Button(() => ToggleCondition(option));
            condBtn.text = "✦";
            condBtn.AddToClassList("nt-option-condition-btn");
            if (!string.IsNullOrEmpty(option.ConditionScript))
                condBtn.AddToClassList("nt-option-condition-btn--active");
            row.Add(condBtn);

            // Remove button
            var removeBtn = new Button(() => RemoveOption(option));
            removeBtn.text = "✕";
            removeBtn.AddToClassList("nt-option-remove-btn");
            row.Add(removeBtn);

            container.Add(row);

            // ── Condition body (shown when condition exists) ──
            var condBody = new VisualElement();
            condBody.AddToClassList("nt-option-condition-body");
            condBody.style.display = string.IsNullOrEmpty(option.ConditionScript)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            container.Add(condBody);

            // Condition field
            var condFieldRow = new VisualElement();
            condFieldRow.AddToClassList("nt-option-condition-field");
            var condLabel = new Label("Condition");
            condLabel.AddToClassList("nt-option-condition-field-label");
            condFieldRow.Add(condLabel);

            var condField = new FlexTextField(multiline: true) { value = option.ConditionScript };
            condField.RegisterValueChangedCallback(evt => option.ConditionScript = evt.newValue);
            condField.OnCommit += (oldVal, newVal) =>
            {
                option.ConditionScript = oldVal;
                Canvas.Commands.Execute(new SetPropertyCommand("ConditionScript",
                    v => option.ConditionScript = (string)v, oldVal, newVal,
                    Canvas.Bus));
            };
            condFieldRow.Add(condField);
            condBody.Add(condFieldRow);

            // Show-if-false toggle
            var toggleRow = new VisualElement();
            toggleRow.AddToClassList("nt-option-condition-toggle");
            var toggle = new Toggle("Show if false") { value = option.HideIfFalse };
            toggle.RegisterValueChangedCallback(evt =>
            {
                var oldVal = option.HideWhenConditionFalse;
                var newVal = evt.newValue;
                option.HideWhenConditionFalse = newVal;
                Canvas.Commands.Execute(new SetPropertyCommand("HideWhenConditionFalse",
                    v => option.HideWhenConditionFalse = (bool)v, oldVal, newVal,
                    Canvas.Bus));
            });
            toggleRow.Add(toggle);
            condBody.Add(toggleRow);

            // ── Output port positioned outside the box ──
            // The port view will be added later in AddOptionRow, but we’ll set a class for styling
            // The port is added directly to this container with absolute positioning via USS.
            // USS use: .nt-option > .nt-port { position: absolute; right: -16px; top: 50%; transform: translateY(-50%); }
            // We'll add a custom class for the port container.
            var portAnchor = new VisualElement();
            portAnchor.AddToClassList("nt-option-port-anchor"); // this will hold the port
            container.Add(portAnchor);

            return container;
        }

        // ── Condition toggle ──
        private void ToggleCondition(ChoiceOption option)
        {
            if (string.IsNullOrEmpty(option.ConditionScript))
            {
                // Enable condition with a default script
                option.ConditionScript = ""; // or "true"
            }
            else
            {
                option.ConditionScript = "";
            }
            // Refresh the row to show/hide condition body
            var row = optionRows[option];
            var condBody = row.Q<VisualElement>(className: "nt-option-condition-body");
            if (condBody != null)
                condBody.style.display = string.IsNullOrEmpty(option.ConditionScript)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;

            // Update the condition button active state
            var condBtn = row.Q<Button>(className: "nt-option-condition-btn");
            condBtn?.EnableInClassList("nt-option-condition-btn--active", !string.IsNullOrEmpty(option.ConditionScript));
        }

        // ── Delete confirmation bar ──
        private void ShowDeleteConfirmation(VisualElement optionRow, ChoiceOption option, int index)
        {
            // Replace the row's content with confirmation UI
            var confirmBar = new VisualElement();
            confirmBar.AddToClassList("nt-option-confirm"); // we'll reuse row styles but change background

            var label = new Label($"Remove \"{option.Label}\"?");
            label.style.color = ColorUtility.ToHexStringRGB(new Color(0.75f, 0.44f, 0.44f));
            label.style.fontSize = 10;
            confirmBar.Add(label);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.marginRight = 0;

            var removeBtn = new Button(() => ConfirmRemoveOption(option, index));
            removeBtn.text = "Remove";
            removeBtn.style.backgroundColor = ColorUtility.ToHexStringRGB(new Color(0.35f, 0.13f, 0.13f));
            removeBtn.style.borderColor = ColorUtility.ToHexStringRGB(new Color(0.48f, 0.19f, 0.19f));
            removeBtn.style.color = ColorUtility.ToHexStringRGB(new Color(0.88f, 0.5f, 0.5f));
            buttons.Add(removeBtn);

            var cancelBtn = new Button(() => RestoreOptionRow(optionRow));
            cancelBtn.text = "Cancel";
            cancelBtn.style.backgroundColor = ColorUtility.ToHexStringRGB(new Color(0.11f, 0.11f, 0.11f));
            cancelBtn.style.borderColor = ColorUtility.ToHexStringRGB(new Color(0.23f, 0.23f, 0.23f));
            buttons.Add(cancelBtn);

            confirmBar.Add(buttons);

            // Store original content and swap
            var originalContent = optionRow.Children().ToList();
            optionRow.userData = originalContent; // cache for restore
            optionRow.Clear();
            optionRow.Add(confirmBar);

            // Dismiss on click outside (via PointerDownEvent on whole canvas)
            // For simplicity, we add a one-time capture to the root
            var root = optionRow.hierarchy.parent;
            var handler = new EventCallback<PointerDownEvent>(e =>
            {
                if (!optionRow.ContainsPoint(e.localPosition))
                {
                    RestoreOptionRow(optionRow);
                    root.UnregisterCallback<PointerDownEvent>(handler);
                }
            });
            root.RegisterCallback<PointerDownEvent>(handler);
        }

        private void RestoreOptionRow(VisualElement optionRow)
        {
            var originalContent = optionRow.userData as List<VisualElement>;
            if (originalContent == null) return;
            optionRow.Clear();
            foreach (var child in originalContent)
                optionRow.Add(child);
            optionRow.userData = null;
        }
    }
}