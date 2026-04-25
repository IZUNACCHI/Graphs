using NarrativeTool.Core.Commands;
using NarrativeTool.Core.Widgets;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using System;
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
                v => data.HasPreamble = (bool)v, old, next, Canvas.Bus));
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
            var opt = ChoiceNodeData.MakeOption("New Option");
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
            using var tx = Canvas.Commands.BeginTransaction("Remove choice option");

            // Remove any edges connected to this option's port before removing the option
            var connectedEdges = Canvas.Graph.Edges
                .Where(e => (e.FromNodeId == Node.Id && e.FromPortId == option.PortId)
                         || (e.ToNodeId == Node.Id && e.ToPortId == option.PortId))
                .Select(e => e.Id)
                .ToList();
            foreach (var edgeId in connectedEdges)
                Canvas.Commands.Execute(new RemoveEdgeCmd(Canvas.Graph, Canvas.Bus, edgeId));

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
            Canvas.Commands.Execute(new MoveOptionCmd(data, idx, newIdx,
                onDo: RebuildOptionRows, onUndo: RebuildOptionRows));
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
                var portData = new PortData(option.PortId, "",
                    PortDirection.Output, PortCapacity.Single, "flow");
                var portView = new PortView(portData) { OwnerNode = this };
                portViews[option.PortId] = portView;
                portView.RegisterCallback<GeometryChangedEvent>(_ => ScheduleEdgeRefresh());
            }
            // Always re-parent: handles the case where the base NodeView added it to outputsColumn
            var pv = portViews[option.PortId];
            pv.RemoveFromHierarchy();
            row.Add(pv); // positioned absolutely via USS

            ScheduleEdgeRefresh();
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
                ScheduleEdgeRefresh();
            }
        }

        private void RebuildOptionRows()
        {
            optionsList.Clear();
            optionRows.Clear();
            // Keep portViews in dict so AddOptionRow can re-parent rather than recreate
            var data = (ChoiceNodeData)Node;
            for (int i = 0; i < data.Options.Count; i++)
                AddOptionRow(data.Options[i], i);
        }

        private void ScheduleEdgeRefresh()
        {
            schedule.Execute(() => Canvas?.EdgeLayer.RefreshEdgesForNode(Node.Id)).ExecuteLater(0);
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
            upBtn.text = "$";
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
                    v => option.Label = (string)v, oldVal, newVal, Canvas.Bus,
                    onRefresh: () => textField.SetValueWithoutNotify(option.Label)));
            };
            row.Add(textField);

            // Condition toggle button
            var condBody = new VisualElement();   // declared early so condBtn closure can reference it
            condBody.AddToClassList("nt-option-condition-body");

            var condBtn = new Button();
            condBtn.text = "Script";
            condBtn.AddToClassList("nt-option-condition-btn");
            condBtn.EnableInClassList("nt-option-condition-btn--active", option.ConditionEnabled);
            condBtn.clicked += () =>
            {
                bool oldVal = option.ConditionEnabled;
                bool newVal = !oldVal;
                Canvas.Commands.Execute(new SetPropertyCommand("ConditionEnabled",
                    v =>
                    {
                        option.ConditionEnabled = (bool)v;
                        condBtn.EnableInClassList("nt-option-condition-btn--active", option.ConditionEnabled);
                        condBody.style.display = option.ConditionEnabled ? DisplayStyle.Flex : DisplayStyle.None;
                    },
                    oldVal, newVal, Canvas.Bus));
            };
            row.Add(condBtn);

            // Remove button
            var removeBtn = new Button(() => RemoveOption(option));
            removeBtn.text = "X";
            removeBtn.AddToClassList("nt-option-remove-btn");
            row.Add(removeBtn);

            container.Add(row);

            // Condition body
            condBody.style.display = option.ConditionEnabled ? DisplayStyle.Flex : DisplayStyle.None;

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
                    v => option.ConditionScript = (string)v, oldVal, newVal, Canvas.Bus,
                    onRefresh: () => condField.SetValueWithoutNotify(option.ConditionScript)));
            };
            condFieldRow.Add(condField);
            condBody.Add(condFieldRow);

            // Hide-when-false toggle
            var toggleRow = new VisualElement();
            toggleRow.AddToClassList("nt-option-condition-toggle");
            var toggle = new Toggle("Hide when false") { value = option.HideWhenConditionFalse };
            toggle.RegisterValueChangedCallback(evt =>
            {
                Canvas.Commands.Execute(new SetPropertyCommand("HideWhenConditionFalse",
                    v => option.HideWhenConditionFalse = (bool)v,
                    evt.previousValue, evt.newValue, Canvas.Bus,
                    onRefresh: () => toggle.SetValueWithoutNotify(option.HideWhenConditionFalse)));
            });
            toggleRow.Add(toggle);
            condBody.Add(toggleRow);

            container.Add(condBody);

            return container;
        }

        private void ShowDeleteConfirmation(VisualElement optionRow, ChoiceOption option, int index)
        {
            var confirm = new VisualElement();
            confirm.AddToClassList("nt-option--confirm");

            var confirmRow = new VisualElement();
            confirmRow.AddToClassList("nt-option-confirm-row");

            var label = new Label($"Remove \"{option.Label}\"?");
            label.AddToClassList("nt-option-confirm-label");
            confirmRow.Add(label);

            var removeBtn = new Button(() => { DismissConfirm(optionRow); ConfirmRemoveOption(option, index); });
            removeBtn.text = "Remove";
            removeBtn.AddToClassList("nt-btn");
            removeBtn.AddToClassList("nt-btn--danger");
            confirmRow.Add(removeBtn);

            var cancelBtn = new Button(() => DismissConfirm(optionRow));
            cancelBtn.text = "Cancel";
            cancelBtn.AddToClassList("nt-btn");
            cancelBtn.AddToClassList("nt-btn--normal");
            confirmRow.Add(cancelBtn);

            confirm.Add(confirmRow);

            var original = optionRow.Children().ToList();
            optionRow.userData = original;
            optionRow.Clear();
            optionRow.Add(confirm);

            // Register on the canvas so any click anywhere outside the confirm dismisses it
            EventCallback<PointerDownEvent> clickOutside = null;
            clickOutside = evt =>
            {
                if (optionRow.userData is List<VisualElement> &&
                    !optionRow.worldBound.Contains(evt.position))
                {
                    DismissConfirm(optionRow);
                    Canvas.UnregisterCallback(clickOutside, TrickleDown.TrickleDown);
                }
            };
            Canvas.RegisterCallback(clickOutside, TrickleDown.TrickleDown);
        }

        private void DismissConfirm(VisualElement optionRow)
        {
            if (optionRow.userData is not List<VisualElement> original) return;
            optionRow.Clear();
            foreach (var child in original)
                optionRow.Add(child);
            optionRow.userData = null;
        }
    }
}