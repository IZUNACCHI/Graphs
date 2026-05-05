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
        private readonly VisualElement listContainer;
        private readonly ScrollView scroll;
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

            scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            listContainer = new VisualElement();
            listContainer.style.flexDirection = FlexDirection.Column;
            scroll.Add(listContainer);
            Add(scroll);

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
            listContainer.Clear();
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
                listContainer.Clear();
                foreach (var en in entries) listContainer.Add(BuildRow(en));
            }
            else
            {
                listContainer.Add(BuildRow(entry));
            }
            UpdateFooter();
            scroll.scrollOffset = new Vector2(0, float.MaxValue);
        }

        private void UpdateFooter()
        {
            footerSummary.text = $"{entries.Count} steps";
        }

        private static VisualElement BuildRow(LogEntry e)
        {
            var row = new VisualElement();
            row.AddToClassList("debugger-row");
            if (e.Type == LogEntryType.BreakpointHit)
                row.AddToClassList("debugger-log__row--active");

            var time = new Label($"{e.TimeSeconds:0.0}s");
            time.AddToClassList("debugger-log__timestamp");
            row.Add(time);

            var step = new Label($"#{e.Step}");
            step.AddToClassList("debugger-log__step");
            row.Add(step);

            var nodeWrap = new VisualElement();
            nodeWrap.AddToClassList("debugger-log__node-label");
            if (e.Type == LogEntryType.BreakpointHit)
            {
                var dot = new VisualElement();
                dot.AddToClassList("debugger-log__active-dot");
                nodeWrap.Add(dot);
            }
            var nodeName = new Label(e.NodeId);
            nodeName.AddToClassList("debugger-log__node-name");
            if (e.Type == LogEntryType.BreakpointHit)
                nodeName.AddToClassList("debugger-log__node-name--active");
            nodeName.tooltip = e.Description;
            nodeWrap.Add(nodeName);
            row.Add(nodeWrap);

            var badge = new Label(TypeBadge(e.Type));
            badge.AddToClassList("debugger-log__type-badge");
            badge.AddToClassList(TypeBadgeClass(e.Type));
            row.Add(badge);

            return row;
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
