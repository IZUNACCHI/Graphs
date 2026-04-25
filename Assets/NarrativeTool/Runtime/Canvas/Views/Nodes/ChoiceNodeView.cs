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

        private Dictionary<ChoiceOption, VisualElement> optionRows = new();

        public ChoiceNodeView(NodeData node, GraphView canvas) : base(node, canvas)
        {
            schedule.Execute(() =>
            {
                GroupPreambleFields();
                UpdatePreambleUI();
            }).StartingIn(0);
        }

        protected override void BuildCustomBody()
        {
            var data = (ChoiceNodeData)Node;

            // ── Preamble toggle ──
            preambleToggleSection = new VisualElement();
            preambleToggleSection.AddToClassList("nt-preamble-toggle");
            extrasContainer.Add(preambleToggleSection);

            var leftLine = new VisualElement();
            leftLine.AddToClassList("nt-preamble-toggle-line");
            var rightLine = new VisualElement();
            rightLine.AddToClassList("nt-preamble-toggle-line");

            preambleToggleBtn = new Button(TogglePreamble) { text = "▾ Preamble" };
            preambleToggleBtn.AddToClassList("nt-preamble-toggle-btn");
            preambleToggleSection.Add(leftLine);
            preambleToggleSection.Add(preambleToggleBtn);
            preambleToggleSection.Add(rightLine);

            // ── Preamble body ──
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

            foreach (var opt in data.Options)
                AddOptionRow(opt);
        }

        private void GroupPreambleFields()
        {
            if (preambleBody == null) return;
            if (propWidgets.TryGetValue("Speaker", out var sp)) preambleBody.Add(sp);
            if (propWidgets.TryGetValue("StageDirections", out var sd)) preambleBody.Add(sd);
            if (propWidgets.TryGetValue("DialogueText", out var dt)) preambleBody.Add(dt);
        }

        private void TogglePreamble()
        {
            var data = (ChoiceNodeData)Node;
            var old = data.HasPreamble;
            var next = !old;
            Canvas.Commands.Execute(new SetPropertyCommand("HasPreamble",
                v => data.HasPreamble = (bool)v, old, next, Canvas.Bus, Canvas.Graph?.Id));
            UpdatePreambleUI();
        }

        public void UpdatePreambleUI()
        {
            var data = (ChoiceNodeData)Node;
            if (preambleToggleBtn != null)
                preambleToggleBtn.EnableInClassList("nt-preamble-toggle-btn--active", data.HasPreamble);
            if (preambleBody != null)
                preambleBody.style.display = data.HasPreamble ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void AddOption()
        {
            var data = (ChoiceNodeData)Node;
            int idx = data.Options.Count;
            var opt = new ChoiceOption
            {
                Id = $"opt_{Guid.NewGuid():N}".Substring(0, 8),
                Label = "New Option",
                PortId = $"out_{idx}"
            };
            Canvas.Commands.Execute(new AddChoiceOptionCmd(data, idx, opt,
                onDo: () => AddOptionRow(opt, idx),
                onUndo: () => RemoveOptionRow(opt)));
        }

        private void RemoveOption(ChoiceOption option)
        {
            var data = (ChoiceNodeData)Node;
            int idx = data.Options.IndexOf(option);
            if (idx < 0) return;
            ShowDeleteConfirmation(optionRows[option], option, idx);
        }

        private void ConfirmRemoveOption(ChoiceOption option, int idx)
        {
            var data = (ChoiceNodeData)Node;
            Canvas.Commands.Execute(new RemoveChoiceOptionCmd(data, idx, option,
                onDo: () => RemoveOptionRow(option),
                onUndo: () => AddOptionRow(option, idx)));
        }

        private void MoveOption(ChoiceOption option, int delta)
        {
            var data = (ChoiceNodeData)Node;
            int idx = data.Options.IndexOf(option);
            int newIdx = idx + delta;
            if (newIdx < 0 || newIdx >= data.Options.Count) return;
            Canvas.Commands.Execute(new MoveOptionCmd(data, idx, newIdx));
            RebuildOptionRows();
        }

        // ── Row building ──
        private void AddOptionRow(ChoiceOption option, int index = -1)
        {
            if (index < 0) index = ((ChoiceNodeData)Node).Options.IndexOf(option);
            var row = BuildOptionRow(option, index);
            optionsList.Insert(index, row);
            optionRows[option] = row;

            if (!portViews.ContainsKey(option.PortId))
            {
                var portData = new PortData(option.PortId, option.Label,
                    PortDirection.Output, PortCapacity.Single, "flow");
                var portView = new PortView(portData) { OwnerNode = this };
                portViews[option.PortId] = portView;
                row.Add(portView); // positioned absolutely via USS
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
            optionsList.Clear();
            optionRows.Clear();
            var data = (ChoiceNodeData)Node;
            for (int i = 0; i < data.Options.Count; i++)
                AddOptionRow(data.Options[i], i);
        }

        private VisualElement BuildOptionRow(ChoiceOption option, int index)
        {
            var data = (ChoiceNodeData)Node;

            var container = new VisualElement();
            container.AddToClassList("nt-option");
            container.userData = option;

            // Main row
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

            // Text field
            var textField = new FlexTextField(multiline: false) { value = option.Label };
            textField.AddToClassList("nt-option-text");
            textField.RegisterValueChangedCallback(evt => option.Label = evt.newValue);
            textField.OnCommit += (oldVal, newVal) =>
            {
                option.Label = oldVal;
                Canvas.Commands.Execute(new SetPropertyCommand("Option Label",
                    v => option.Label = (string)v, oldVal, newVal,
                    Canvas.Bus, Canvas.Graph?.Id));
            };
            row.Add(textField);

            // Condition toggle button
            var condBtn = new Button(() => ToggleCondition(option, container));
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

            // Condition body
            var condBody = new VisualElement();
            condBody.AddToClassList("nt-option-condition-body");
            condBody.style.display = string.IsNullOrEmpty(option.ConditionScript) ? DisplayStyle.None : DisplayStyle.Flex;

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
                    Canvas.Bus, Canvas.Graph?.Id));
            };
            condFieldRow.Add(condField);
            condBody.Add(condFieldRow);

            // Show-if-false toggle
            var toggleRow = new VisualElement();
            toggleRow.AddToClassList("nt-option-condition-toggle");
            var toggle = new Toggle("Show if false") { value = option.HideIfFalse };
            toggle.RegisterValueChangedCallback(evt =>
            {
                var oldVal = option.HideIfFalse;
                option.HideIfFalse = evt.newValue;
                Canvas.Commands.Execute(new SetPropertyCommand("HideIfFalse",
                    v => option.HideIfFalse = (bool)v, oldVal, evt.newValue,
                    Canvas.Bus, Canvas.Graph?.Id));
            });
            toggleRow.Add(toggle);
            condBody.Add(toggleRow);

            container.Add(condBody);

            return container;
        }

        private void ToggleCondition(ChoiceOption option, VisualElement container)
        {
            // Toggle condition data
            bool hasCondition = string.IsNullOrEmpty(option.ConditionScript);
            if (hasCondition)
                option.ConditionScript = ""; // enable with empty, or could default to "true"
            else
                option.ConditionScript = "";

            // Update UI
            var condBtn = container.Q<Button>(className: "nt-option-condition-btn");
            condBtn?.EnableInClassList("nt-option-condition-btn--active", hasCondition);
            var condBody = container.Q<VisualElement>(className: "nt-option-condition-body");
            if (condBody != null)
                condBody.style.display = hasCondition ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ShowDeleteConfirmation(VisualElement optionRow, ChoiceOption option, int index)
        {
            // Replace row content with confirmation bar using USS classes
            var confirm = new VisualElement();
            confirm.AddToClassList("nt-option--confirm");

            var confirmRow = new VisualElement();
            confirmRow.AddToClassList("nt-option-confirm-row");

            var label = new Label($"Remove \"{option.Label}\"?");
            label.AddToClassList("nt-option-confirm-label");
            confirmRow.Add(label);

            var removeBtn = new Button(() => ConfirmRemoveOption(option, index));
            removeBtn.text = "Remove";
            removeBtn.AddToClassList("nt-btn");
            removeBtn.AddToClassList("nt-btn--danger");
            confirmRow.Add(removeBtn);

            var cancelBtn = new Button(() => RestoreOptionRow(optionRow));
            cancelBtn.text = "Cancel";
            cancelBtn.AddToClassList("nt-btn");
            cancelBtn.AddToClassList("nt-btn--normal");
            confirmRow.Add(cancelBtn);

            confirm.Add(confirmRow);

            // Replace content
            var original = optionRow.Children().ToList();
            optionRow.userData = original;
            optionRow.Clear();
            optionRow.Add(confirm);

            // Dismiss when clicking outside
            var root = optionRow.hierarchy.parent;
            root.RegisterCallback<PointerDownEvent>(OnClickOutside);
        }

        private void RestoreOptionRow(VisualElement optionRow)
        {
            if (optionRow.userData is List<VisualElement> original)
            {
                optionRow.Clear();
                foreach (var child in original)
                    optionRow.Add(child);
                optionRow.userData = null;
            }
        }

        private void OnClickOutside(PointerDownEvent evt)
        {
            // Simple: if the target is not within any confirm row, restore all?
            // Better: store reference to the specific row.
            // For simplicity, we can check all option rows for confirm state.
            foreach (var kv in optionRows)
            {
                var row = kv.Value;
                if (row.userData is List<VisualElement> && !row.ContainsPoint(evt.localPosition))
                {
                    RestoreOptionRow(row);
                    break; // only one confirm at a time
                }
            }
        }
    }
}