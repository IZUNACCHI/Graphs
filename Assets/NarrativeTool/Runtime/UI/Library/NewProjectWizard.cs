using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Library
{
    /// <summary>
    /// Modal overlay for "New Project". Three steps: Setup → Template →
    /// Locales. Step 2 (Template) is intentionally a placeholder — see the
    /// TODO inside RenderStep — and just shows a stub message; advancing
    /// from it is allowed.
    /// </summary>
    public sealed class NewProjectWizard : VisualElement
    {
        // Default locale list shown on step 3. en-US is always included.
        public static readonly string[] AvailableLocales =
        {
            "en-US", "en-GB", "fr-FR", "de-DE", "ja-JP", "zh-CN",
            "es-ES", "pt-BR", "ko-KR", "ru-RU", "it-IT", "pl-PL",
        };

        public Action OnCancel;
        // Fires when the user finalises step 3. The bootstrap creates the
        // ProjectModel + ProjectLibraryEntry from these values.
        public Action<NewProjectResult> OnCreate;

        // Step state
        private int step = 1;
        private string projectName = "Untitled Project";
        private string saveLocation = "/projects/";
        private readonly HashSet<string> selectedLocales = new() { "en-US" };

        // UI
        private Label step1Tab, step2Tab, step3Tab;
        private VisualElement bodyHost;
        private Button backBtn;
        private Button nextBtn;

        public NewProjectWizard()
        {
            AddToClassList("nt-wiz-overlay");
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;

            // Click-outside on the overlay (but not the box) cancels.
            RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.target == this) { OnCancel?.Invoke(); RemoveFromHierarchy(); }
            });

            var box = new VisualElement();
            box.AddToClassList("nt-wiz-box");
            // Eat clicks inside the box so they don't dismiss.
            box.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            Add(box);

            // ── Header ──
            var head = new VisualElement();
            head.AddToClassList("nt-wiz-head");
            var title = new Label("New Project");
            title.AddToClassList("nt-wiz-title");
            head.Add(title);

            var steps = new VisualElement();
            steps.AddToClassList("nt-wiz-steps");
            step1Tab = BuildStepTab("01 SETUP");
            step2Tab = BuildStepTab("02 TEMPLATE");
            step3Tab = BuildStepTab("03 LOCALES");
            steps.Add(step1Tab); steps.Add(step2Tab); steps.Add(step3Tab);
            head.Add(steps);

            var close = new Button(() => { OnCancel?.Invoke(); RemoveFromHierarchy(); }) { text = "×" };
            close.AddToClassList("nt-wiz-close");
            head.Add(close);
            box.Add(head);

            // ── Body ──
            bodyHost = new VisualElement();
            bodyHost.AddToClassList("nt-wiz-body");
            box.Add(bodyHost);

            // ── Footer ──
            var foot = new VisualElement();
            foot.AddToClassList("nt-wiz-foot");
            backBtn = new Button(() => Nav(-1)) { text = "Back" };
            backBtn.AddToClassList("nt-library-btn");
            nextBtn = new Button(() => Nav(+1)) { text = "Next →" };
            nextBtn.AddToClassList("nt-library-btn");
            nextBtn.AddToClassList("nt-library-btn--primary");
            foot.Add(backBtn);
            foot.Add(nextBtn);
            box.Add(foot);

            RenderStep();
        }

        private static Label BuildStepTab(string text)
        {
            var lbl = new Label(text);
            lbl.AddToClassList("nt-wiz-step");
            return lbl;
        }

        private void Nav(int dir)
        {
            if (dir == 1 && step == 3)
            {
                Finish();
                return;
            }
            step = Mathf.Clamp(step + dir, 1, 3);
            RenderStep();
        }

        private void Finish()
        {
            var result = new NewProjectResult
            {
                ProjectName = (projectName ?? "").Trim(),
                SaveLocation = (saveLocation ?? "").Trim(),
                Locales = new List<string>(selectedLocales),
            };
            if (string.IsNullOrEmpty(result.ProjectName))
            {
                Debug.LogWarning("[NewProject] Project name is empty.");
                step = 1; RenderStep();
                return;
            }
            OnCreate?.Invoke(result);
            RemoveFromHierarchy();
        }

        private void RenderStep()
        {
            // Tab states
            step1Tab.EnableInClassList("nt-wiz-step--active", step == 1);
            step1Tab.EnableInClassList("nt-wiz-step--done", step > 1);
            step2Tab.EnableInClassList("nt-wiz-step--active", step == 2);
            step2Tab.EnableInClassList("nt-wiz-step--done", step > 2);
            step3Tab.EnableInClassList("nt-wiz-step--active", step == 3);

            backBtn.style.visibility = step == 1 ? Visibility.Hidden : Visibility.Visible;
            nextBtn.text = step == 3 ? "Create Project ✓" : "Next →";

            bodyHost.Clear();
            switch (step)
            {
                case 1: BuildSetupBody(); break;
                case 2: BuildTemplateBody(); break;
                case 3: BuildLocalesBody(); break;
            }
        }

        private void BuildSetupBody()
        {
            var nameLbl = new Label("PROJECT NAME");
            nameLbl.AddToClassList("nt-wiz-label");
            bodyHost.Add(nameLbl);

            var nameField = new TextField { value = projectName };
            nameField.AddToClassList("nt-wiz-input");
            nameField.RegisterValueChangedCallback(evt => projectName = evt.newValue ?? "");
            bodyHost.Add(nameField);

            var locLbl = new Label("SAVE LOCATION");
            locLbl.AddToClassList("nt-wiz-label");
            bodyHost.Add(locLbl);

            var row = new VisualElement();
            row.AddToClassList("nt-wiz-row");
            var pathField = new TextField { value = saveLocation };
            pathField.AddToClassList("nt-wiz-input");
            pathField.style.flexGrow = 1;
            pathField.RegisterValueChangedCallback(evt => saveLocation = evt.newValue ?? "");
            row.Add(pathField);
            // TODO: wire Browse to a real folder picker (Unity SaveFilePanel
            // in editor builds, or a custom dialog in player builds).
            var browse = new Button(() => Debug.Log("[NewProject] Browse — TODO: folder picker."))
            { text = "Browse…" };
            browse.AddToClassList("nt-library-btn");
            row.Add(browse);
            bodyHost.Add(row);

            var hint = new Label(
                "Project will be saved as <name>.nproj in the selected folder. " +
                "A screenshots folder will be created automatically for library thumbnails.");
            hint.AddToClassList("nt-wiz-hint");
            bodyHost.Add(hint);
        }

        private void BuildTemplateBody()
        {
            // TODO templates: full template gallery with descriptions and pre-wired
            // example graphs/variables. For now this step is a stub — Next
            // simply advances to locales without setting any template.
            var hint = new Label(
                "Templates will live here . For now the project starts blank.");
            hint.AddToClassList("nt-wiz-hint");
            bodyHost.Add(hint);

            var stub = new Label("(template gallery not yet implemented)");
            stub.AddToClassList("nt-wiz-label");
            stub.style.marginTop = 12;
            bodyHost.Add(stub);
        }

        //TODO: this locale selector is just visual for now
        private void BuildLocalesBody()
        {
            var hint = new Label(
                "TODO: Select the locales this project will support. " +
                "Additional locales can be added later from Project Settings.");
            hint.AddToClassList("nt-wiz-hint");
            bodyHost.Add(hint);

            var list = new VisualElement();
            list.AddToClassList("nt-wiz-locale-list");
            foreach (var loc in AvailableLocales)
            {
                var captured = loc;
                var pill = new Button(() => ToggleLocale(captured)) { text = captured };
                pill.AddToClassList("nt-wiz-locale-pill");
                if (selectedLocales.Contains(captured))
                    pill.AddToClassList("nt-wiz-locale-pill--sel");
                list.Add(pill);
            }
            bodyHost.Add(list);

            var summary = new Label(
                $"{selectedLocales.Count} locale(s) selected · en-US is always required");
            summary.AddToClassList("nt-wiz-summary");
            bodyHost.Add(summary);
        }

        private void ToggleLocale(string locale)
        {
            if (locale == "en-US") return;   // always required
            if (!selectedLocales.Add(locale)) selectedLocales.Remove(locale);
            RenderStep();
        }
    }

    public sealed class NewProjectResult
    {
        public string ProjectName;
        public string SaveLocation;
        public List<string> Locales;
    }
}
