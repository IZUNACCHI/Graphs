using NarrativeTool.Core.EventSystem;
using NarrativeTool.Core.Runtime;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Runtime
{
    /// <summary>
    /// Dockable panel that shows the current dialogue / choices and provides playback controls.
    /// </summary>
    public sealed class RuntimePanel : VisualElement
    {
        private RuntimeEngine engine;
        private EventBus bus;

        // Subscriptions
        private IDisposable subDialogueLine, subChoicePresented, subContinueRequested, subStateChanged, subNodeEntered;

        // UI elements
        private VisualElement dialogueArea;
        private Label speakerName;
        private Label stageDirection;
        private Label dialogueText;
        private VisualElement choicesContainer;
        private Label choicesLabel;
        private Label footerInfo;

        private Button btnPlay, btnPause, btnStop, btnStep;
        private DropdownField speedDropdown;

        public RuntimePanel()
        {
            AddToClassList("runtime-panel");
            style.flexGrow = 1;

            // ── Title bar ──
            var titleBar = new VisualElement();
            titleBar.AddToClassList("runtime-panel__titlebar");

            var dot = new VisualElement();
            dot.AddToClassList("runtime-panel__title-dot");
            titleBar.Add(dot);

            var titleLabel = new Label("Runtime");
            titleLabel.AddToClassList("runtime-panel__title");
            titleBar.Add(titleLabel);

            var controls = new VisualElement();
            controls.AddToClassList("runtime-panel__controls");

            btnStep = new Button(() => engine?.Step()) { text = "⟩" };
            btnStep.AddToClassList("runtime-panel__ctrl-btn");
            controls.Add(btnStep);

            btnPlay = new Button(() => engine?.Start(null)) { text = "▶" };
            btnPlay.AddToClassList("runtime-panel__ctrl-btn");
            btnPlay.AddToClassList("runtime-panel__ctrl-btn--primary");
            controls.Add(btnPlay);

            btnPause = new Button(() => engine?.Stop()) { text = "⏸" };
            btnPause.AddToClassList("runtime-panel__ctrl-btn");
            controls.Add(btnPause);

            btnStop = new Button(() => engine?.Stop()) { text = "■" };
            btnStop.AddToClassList("runtime-panel__ctrl-btn");
            controls.Add(btnStop);

            speedDropdown = new DropdownField(new List<string> { "1×", "2×", "0.5×" }, 0);
            speedDropdown.AddToClassList("runtime-panel__speed-select");
            controls.Add(speedDropdown);

            titleBar.Add(controls);
            Add(titleBar);

            // ── Dialogue area ──
            dialogueArea = new VisualElement();
            dialogueArea.AddToClassList("runtime-panel__dialogue-area");

            var speakerRow = new VisualElement();
            speakerRow.AddToClassList("runtime-panel__speaker-row");
            var avatar = new VisualElement();
            avatar.AddToClassList("runtime-panel__speaker-avatar");
            var avatarInitial = new Label("?");
            avatarInitial.AddToClassList("runtime-panel__speaker-initial");
            avatar.Add(avatarInitial);
            speakerRow.Add(avatar);
            speakerName = new Label("");
            speakerName.AddToClassList("runtime-panel__speaker-name");
            speakerRow.Add(speakerName);
            dialogueArea.Add(speakerRow);

            stageDirection = new Label("");
            stageDirection.AddToClassList("runtime-panel__stage-direction");
            dialogueArea.Add(stageDirection);

            dialogueText = new Label("");
            dialogueText.AddToClassList("runtime-panel__dialogue-text");
            dialogueArea.Add(dialogueText);

            Add(dialogueArea);

            // ── Choices ──
            choicesContainer = new VisualElement();
            choicesContainer.AddToClassList("runtime-panel__choices");
            choicesLabel = new Label("PLAYER CHOICES");
            choicesLabel.AddToClassList("runtime-panel__choices-label");
            choicesContainer.Add(choicesLabel);
            choicesContainer.style.display = DisplayStyle.None;
            Add(choicesContainer);

            // ── Footer ──
            footerInfo = new Label("");
            footerInfo.AddToClassList("runtime-panel__footer");
            Add(footerInfo);
        }

        public void Bind(RuntimeEngine engine, EventBus bus)
        {
            Unbind();
            this.engine = engine;
            this.bus = bus;

            subDialogueLine = bus.Subscribe<DialogueLineEvent>(OnDialogueLine);
            subChoicePresented = bus.Subscribe<ChoicePresentedEvent>(OnChoicePresented);
            subContinueRequested = bus.Subscribe<ContinueRequestedEvent>(OnContinueRequested);
            subStateChanged = bus.Subscribe<RuntimeStateChanged>(OnStateChanged);
            subNodeEntered = bus.Subscribe<NodeEnteredEvent>(OnNodeEntered);
        }

        public void Unbind()
        {
            subDialogueLine?.Dispose(); subDialogueLine = null;
            subChoicePresented?.Dispose(); subChoicePresented = null;
            subContinueRequested?.Dispose(); subContinueRequested = null;
            subStateChanged?.Dispose(); subStateChanged = null;
            subNodeEntered?.Dispose(); subNodeEntered = null;
            engine = null;
            bus = null;
        }

        private void OnDialogueLine(DialogueLineEvent e)
        {
            speakerName.text = e.Speaker ?? "";
            var avatar = dialogueArea.Q<VisualElement>(className: "runtime-panel__speaker-avatar");
            if (avatar != null)
            {
                var initial = avatar.Q<Label>();
                if (initial != null) initial.text = string.IsNullOrEmpty(e.Speaker) ? "?" : e.Speaker[0].ToString();
            }
            stageDirection.text = e.StageDirections ?? "";   // NEW
            dialogueText.text = e.Line ?? "";

            choicesContainer.style.display = DisplayStyle.None;
            dialogueArea.style.display = DisplayStyle.Flex;
        }

        private void OnChoicePresented(ChoicePresentedEvent e)
        {
            // Set preamble if present
            if (e.HasPreamble)
            {
                speakerName.text = e.Speaker;
                stageDirection.text = e.StageDirections;
                dialogueText.text = e.DialogueText;
                dialogueArea.style.display = DisplayStyle.Flex;
            }
            else
            {
                dialogueArea.style.display = DisplayStyle.None;
            }

            // Show choices below the dialogue area
            choicesContainer.Clear();
            choicesContainer.Add(choicesLabel);

            for (int i = 0; i < e.Options.Count; i++)
            {
                int index = i;
                var rtOpt = e.Options[i];
                var btn = new Button { text = rtOpt.Label };
                btn.AddToClassList("runtime-panel__choice-btn");

                if (!rtOpt.Enabled)
                {
                    btn.SetEnabled(false);
                    btn.style.opacity = 0.4f;
                    btn.tooltip = "Condition not met";
                }
                else
                {
                    btn.clicked += () =>
                    {
                        engine?.GetContext().Interaction.Resolve(index);
                        engine?.ContinueAfterInteraction();
                    };
                }

                choicesContainer.Add(btn);
            }

            choicesContainer.style.display = DisplayStyle.Flex;
        }

        private void OnContinueRequested(ContinueRequestedEvent e)
        {
            dialogueArea.style.display = DisplayStyle.Flex;
            choicesContainer.style.display = DisplayStyle.None;
            footerInfo.text = e.Message ?? "Click to continue";

            dialogueArea.RegisterCallback<ClickEvent>(OnDialogueClicked);
        }

        private void OnDialogueClicked(ClickEvent evt)
        {
            dialogueArea.UnregisterCallback<ClickEvent>(OnDialogueClicked);
            engine?.GetContext().Interaction.Resolve(0);
            engine?.ContinueAfterInteraction();
        }

        private void OnStateChanged(RuntimeStateChanged e)
        {
            bool running = e.NewState == RuntimeState.Running;
            bool paused = e.NewState == RuntimeState.Paused;
            bool idle = e.NewState == RuntimeState.Idle || e.NewState == RuntimeState.Done;

            btnPlay.SetEnabled(!running && !paused);
            btnPause.SetEnabled(running || paused);
            btnStop.SetEnabled(running || paused);
            btnStep.SetEnabled(paused);

            if (idle)
            {
                dialogueArea.style.display = DisplayStyle.Flex;
                speakerName.text = "";
                stageDirection.text = "";
                dialogueText.text = "";
                choicesContainer.style.display = DisplayStyle.None;
                footerInfo.text = "";
            }
        }

        private void OnNodeEntered(NodeEnteredEvent e)
        {
            footerInfo.text = $"node · {e.NodeId} · {e.GraphId}";
        }
    }
}