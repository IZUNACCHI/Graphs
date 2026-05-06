using NarrativeTool.Core.EventSystem;
using NarrativeTool.Core.Runtime;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Debugger
{
    /// <summary>
    /// Log tab. Records one entry per significant runtime event (NodeEntered,
    /// SetVar, Dialogue, Choice, BreakpointHit). Shows time / step / node /
    /// type / description columns. The list is capped to a fixed size (FIFO).
    /// </summary>
    public sealed class LogTab : VisualElement, IDisposable
    {
        private const int MaxEntries = 1000;

        private readonly EventBus bus;
        private readonly ListView listView;
        private readonly Label footerSummary;

        private readonly List<LogEntry> entries = new();
        private int stepCounter;
        private float runStartTime = -1f;

        private IDisposable subState, subNode, subVar, subEntity, subDialogue, subChoice, subBreakpoint;

        public LogTab(EventBus bus)
        {
            this.bus = bus;

            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            // Column header
            var header = new VisualElement();
            header.AddToClassList("debugger-panel__col-header");
            header.Add(MakeHeaderCell("TIME", width: 36));
            header.Add(MakeHeaderCell("STEP", width: 28));
            header.Add(MakeHeaderCell("NODE", flex: 1));
            header.Add(MakeHeaderCell("TYPE", width: 52));
            Add(header);

            // Native, virtualised list (Phase 3 — replaces hand-rolled ScrollView).
            listView = new ListView
            {
                itemsSource = entries,
                fixedItemHeight = 22,
                makeItem = MakeRow,
                bindItem = BindRow,
                selectionType = SelectionType.None,
                showAlternatingRowBackgrounds = AlternatingRowBackground.None,
                showBorder = false,
                reorderable = false,
            };
            listView.style.flexGrow = 1;
            Add(listView);

            // Footer with Clear button
            var footer = new VisualElement();
            footer.AddToClassList("debugger-panel__footer");
            footerSummary = new Label("0 steps");
            footerSummary.AddToClassList("debugger-panel__footer-label");
            footer.Add(footerSummary);
            var clear = new Button(Clear) { text = "Clear" };
            clear.AddToClassList("debugger-panel__footer-btn");
            footer.Add(clear);
            Add(footer);

            subState = bus?.Subscribe<RuntimeStateChanged>(OnState);
            subNode = bus?.Subscribe<NodeEnteredEvent>(OnNodeEntered);
            subVar = bus?.Subscribe<VariableRuntimeValueChangedEvent>(OnVariable);
            subEntity = bus?.Subscribe<EntityRuntimeValueChangedEvent>(OnEntity);
            subDialogue = bus?.Subscribe<DialogueLineEvent>(OnDialogue);
            subChoice = bus?.Subscribe<ChoicePresentedEvent>(OnChoice);
            subBreakpoint = bus?.Subscribe<BreakpointHitEvent>(OnBreakpoint);
        }

        public void Dispose()
        {
            subState?.Dispose(); subNode?.Dispose(); subVar?.Dispose();
            subEntity?.Dispose(); subDialogue?.Dispose(); subChoice?.Dispose();
            subBreakpoint?.Dispose();
            subState = subNode = subVar = subEntity = subDialogue = subChoice = subBreakpoint = null;
        }

        private static Label MakeHeaderCell(string text, float? flex = null, float? width = null)
        {
            var l = new Label(text);
            l.AddToClassList("debugger-panel__col-header-cell");
            if (flex.HasValue) l.style.flexGrow = flex.Value;
            if (width.HasValue) l.style.width = width.Value;
            return l;
        }

        public new void Clear()
        {
            entries.Clear();
            stepCounter = 0;
            listView.RefreshItems();
            UpdateFooter();
        }

        private void OnState(RuntimeStateChanged e)
        {
            if (e.NewState == RuntimeState.Running && runStartTime < 0f)
            {
                runStartTime = Time.realtimeSinceStartup;
                Clear();
            }
            else if (e.NewState == RuntimeState.Idle || e.NewState == RuntimeState.Done)
            {
                runStartTime = -1f;
            }
        }

        private void OnNodeEntered(NodeEnteredEvent e)
        {
            stepCounter++;
            Append(new LogEntry
            {
                TimeSeconds = ElapsedTime(),
                Step = stepCounter,
                NodeId = e.NodeId,
                Type = LogEntryType.NodeEntered,
                Description = $"entered {e.NodeId}",
            });
        }

        private void OnVariable(VariableRuntimeValueChangedEvent e)
        {
            Append(new LogEntry
            {
                TimeSeconds = ElapsedTime(),
                Step = stepCounter,
                NodeId = e.Name,
                Type = LogEntryType.SetVar,
                Description = $"set {e.Name} = {e.NewValue}",
            });
        }

        private void OnEntity(EntityRuntimeValueChangedEvent e)
        {
            Append(new LogEntry
            {
                TimeSeconds = ElapsedTime(),
                Step = stepCounter,
                NodeId = $"{e.EntityName}.{e.FieldName}",
                Type = LogEntryType.SetVar,
                Description = $"set {e.EntityName}.{e.FieldName} = {e.NewValue}",
            });
        }

        private void OnDialogue(DialogueLineEvent e)
        {
            Append(new LogEntry
            {
                TimeSeconds = ElapsedTime(),
                Step = stepCounter,
                NodeId = e.NodeId,
                Type = LogEntryType.Dialogue,
                Description = string.IsNullOrEmpty(e.Speaker) ? e.Line : $"{e.Speaker}: {e.Line}",
            });
        }

        private void OnChoice(ChoicePresentedEvent e)
        {
            Append(new LogEntry
            {
                TimeSeconds = ElapsedTime(),
                Step = stepCounter,
                NodeId = e.NodeId,
                Type = LogEntryType.Choice,
                Description = $"{e.Options.Count} option(s)",
            });
        }

        private void OnBreakpoint(BreakpointHitEvent e)
        {
            Append(new LogEntry
            {
                TimeSeconds = ElapsedTime(),
                Step = stepCounter,
                NodeId = e.NodeId,
                Type = LogEntryType.BreakpointHit,
                Description = "breakpoint hit",
            });
        }

        private float ElapsedTime() =>
            runStartTime < 0f ? 0f : (Time.realtimeSinceStartup - runStartTime);

        private void Append(LogEntry entry)
        {
            entries.Add(entry);
            if (entries.Count > MaxEntries)
            {
                int drop = entries.Count - MaxEntries;
                entries.RemoveRange(0, drop);
            }
            listView.RefreshItems();
            UpdateFooter();
            // Auto-scroll to the newest entry.
            listView.ScrollToItem(entries.Count - 1);
        }

        private void UpdateFooter()
        {
            footerSummary.text = $"{entries.Count} steps";
        }

        // ListView builds rows via makeItem (one VisualElement reused per slot)
        // and updates them via bindItem when the slot is bound to an entry index.
        private static VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("debugger-row");

            var time = new Label();
            time.AddToClassList("debugger-log__timestamp");
            time.name = "time";
            row.Add(time);

            var step = new Label();
            step.AddToClassList("debugger-log__step");
            step.name = "step";
            row.Add(step);

            var nodeWrap = new VisualElement();
            nodeWrap.AddToClassList("debugger-log__node-label");
            nodeWrap.name = "node-wrap";
            var dot = new VisualElement();
            dot.AddToClassList("debugger-log__active-dot");
            dot.name = "active-dot";
            nodeWrap.Add(dot);
            var nodeName = new Label();
            nodeName.AddToClassList("debugger-log__node-name");
            nodeName.name = "node-name";
            nodeWrap.Add(nodeName);
            row.Add(nodeWrap);

            var badge = new Label();
            badge.AddToClassList("debugger-log__type-badge");
            badge.name = "badge";
            row.Add(badge);

            return row;
        }

        private void BindRow(VisualElement row, int index)
        {
            if (index < 0 || index >= entries.Count) return;
            var e = entries[index];
            bool isBp = e.Type == LogEntryType.BreakpointHit;

            row.EnableInClassList("debugger-log__row--active", isBp);

            var time = row.Q<Label>("time");
            time.text = $"{e.TimeSeconds:0.0}s";

            var step = row.Q<Label>("step");
            step.text = $"#{e.Step}";

            var dot = row.Q<VisualElement>("active-dot");
            dot.style.display = isBp ? DisplayStyle.Flex : DisplayStyle.None;

            var nodeName = row.Q<Label>("node-name");
            nodeName.text = e.NodeId;
            nodeName.tooltip = e.Description;
            nodeName.EnableInClassList("debugger-log__node-name--active", isBp);

            var badge = row.Q<Label>("badge");
            badge.text = TypeBadge(e.Type);
            // Reset badge variant classes before adding the right one.
            badge.RemoveFromClassList("debugger-log__type-badge--choice");
            badge.RemoveFromClassList("debugger-log__type-badge--dialogue");
            badge.RemoveFromClassList("debugger-log__type-badge--setvar");
            badge.RemoveFromClassList("debugger-log__type-badge--default");
            badge.AddToClassList(TypeBadgeClass(e.Type));
        }

        private static string TypeBadge(LogEntryType t) => t switch
        {
            LogEntryType.NodeEntered => "Node",
            LogEntryType.SetVar => "SetVar",
            LogEntryType.Dialogue => "Dia.",
            LogEntryType.Choice => "Choice",
            LogEntryType.BreakpointHit => "BP",
            _ => "—",
        };

        private static string TypeBadgeClass(LogEntryType t) => t switch
        {
            LogEntryType.Choice => "debugger-log__type-badge--choice",
            LogEntryType.Dialogue => "debugger-log__type-badge--dialogue",
            LogEntryType.SetVar => "debugger-log__type-badge--setvar",
            _ => "debugger-log__type-badge--default",
        };
    }

    public enum LogEntryType { NodeEntered, SetVar, Dialogue, Choice, BreakpointHit }

    public sealed class LogEntry
    {
        public float TimeSeconds;
        public int Step;
        public string NodeId;
        public LogEntryType Type;
        public string Description;
    }
}
