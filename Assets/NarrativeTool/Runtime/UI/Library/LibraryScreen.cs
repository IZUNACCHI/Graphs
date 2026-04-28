using NarrativeTool.Core.Utility;
using NarrativeTool.Data.Project;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Library
{
    /// <summary>
    /// Start-up screen showing pinned and recent projects. The bootstrap
    /// hosts this at launch and swaps it out for the editor view once the
    /// user opens or creates a project.
    /// </summary>
    public sealed class LibraryScreen : VisualElement
    {
        private ProjectLibrary library;
        private TextField searchField;
        private VisualElement pinnedSection;
        private VisualElement pinnedGrid;
        private VisualElement recentGrid;
        private string filter = "";

        public Action<ProjectLibraryEntry> OnOpenProject;
        public Action OnNewProject;
        public Action OnOpenFile;
        // Fires after any in-screen mutation (pin toggle for now). Bootstrap
        // hooks this to persist library.json.
        public Action OnLibraryChanged;

        public LibraryScreen()
        {
            AddToClassList("nt-library");
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            // ── Top bar ──
            var topbar = new VisualElement();
            topbar.AddToClassList("nt-library-topbar");
            var logo = new Label("nar.tool");
            logo.AddToClassList("nt-library-logo");
            topbar.Add(logo);

            var sep = new VisualElement();
            sep.AddToClassList("nt-library-sep");
            topbar.Add(sep);

            searchField = new TextField { value = "" };
            searchField.AddToClassList("nt-library-search");
            searchField.RegisterValueChangedCallback(evt =>
            {
                filter = evt.newValue ?? "";
                RebuildGrids();
            });
            topbar.Add(searchField);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            topbar.Add(spacer);

            var openBtn = new Button(() => OnOpenFile?.Invoke()) { text = "Open File…" };
            openBtn.AddToClassList("nt-library-btn");
            topbar.Add(openBtn);

            var newBtn = new Button(() => OnNewProject?.Invoke()) { text = "+ New Project" };
            newBtn.AddToClassList("nt-library-btn");
            newBtn.AddToClassList("nt-library-btn--primary");
            topbar.Add(newBtn);

            Add(topbar);

            // ── Body ──
            var bodyScroll = new ScrollView(ScrollViewMode.Vertical);
            bodyScroll.AddToClassList("nt-library-body");
            bodyScroll.style.flexGrow = 1;
            Add(bodyScroll);
            var body = bodyScroll.contentContainer;

            pinnedSection = new VisualElement();
            pinnedSection.AddToClassList("nt-library-section");
            pinnedSection.Add(BuildSectionLabel("PINNED", "#4a9a6a"));
            pinnedGrid = new VisualElement();
            pinnedGrid.AddToClassList("nt-library-grid");
            pinnedSection.Add(pinnedGrid);
            body.Add(pinnedSection);

            var recentSection = new VisualElement();
            recentSection.AddToClassList("nt-library-section");
            recentSection.Add(BuildSectionLabel("RECENT", "#383838"));
            recentGrid = new VisualElement();
            recentGrid.AddToClassList("nt-library-grid");
            recentSection.Add(recentGrid);
            body.Add(recentSection);
        }

        public void Bind(ProjectLibrary library)
        {
            this.library = library;
            RebuildGrids();
        }

        private VisualElement BuildSectionLabel(string text, string pipColor)
        {
            var row = new VisualElement();
            row.AddToClassList("nt-library-section-label");
            var pip = new VisualElement();
            pip.AddToClassList("nt-library-pip");
            pip.style.backgroundColor = ParseColor(pipColor);
            row.Add(pip);
            row.Add(new Label(text));
            return row;
        }

        private void RebuildGrids()
        {
            pinnedGrid.Clear();
            recentGrid.Clear();
            if (library == null) return;

            bool hasAnyPinned = false;
            foreach (var e in library.Entries)
            {
                if (!MatchesFilter(e)) continue;
                var card = BuildCard(e);
                if (e.Pinned) { pinnedGrid.Add(card); hasAnyPinned = true; }
                else recentGrid.Add(card);
            }
            pinnedSection.style.display = hasAnyPinned ? DisplayStyle.Flex : DisplayStyle.None;

            // Always-present "+ NEW PROJECT" tile at the end of recents.
            var newTile = new Button(() => OnNewProject?.Invoke()) { text = "" };
            newTile.AddToClassList("nt-library-newcard");
            var icon = new Label("+");
            icon.AddToClassList("nt-library-newcard-icon");
            newTile.Add(icon);
            var newLbl = new Label("NEW PROJECT");
            newLbl.AddToClassList("nt-library-newcard-text");
            newTile.Add(newLbl);
            recentGrid.Add(newTile);
        }

        private bool MatchesFilter(ProjectLibraryEntry e)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            return (e.Name ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private VisualElement BuildCard(ProjectLibraryEntry entry)
        {
            var card = new VisualElement();
            card.AddToClassList("nt-library-card");
            if (entry.Pinned) card.AddToClassList("nt-library-card--pinned");
            card.RegisterCallback<ClickEvent>(_ => OnOpenProject?.Invoke(entry));

            // Thumbnail (tinted block + node/edge stat in corner)
            var thumb = new VisualElement();
            thumb.AddToClassList("nt-library-card-thumb");
            thumb.style.backgroundColor = ThumbBg(entry.ThumbHueKey);

            var stat = new Label($"{entry.GraphCount} Graphs · {entry.NodeCount} Nodes");
            stat.AddToClassList("nt-library-card-stat");
            thumb.Add(stat);
            card.Add(thumb);

            // Info block
            var info = new VisualElement();
            info.AddToClassList("nt-library-card-info");
            var name = new Label(entry.Name);
            name.AddToClassList("nt-library-card-name");
            info.Add(name);
            var date = new Label(TimeUtils.RelativeTime(entry.LastOpened));
            date.AddToClassList("nt-library-card-date");
            info.Add(date);

            var foot = new VisualElement();
            foot.AddToClassList("nt-library-card-foot");
            var path = new Label(entry.Path ?? "");
            path.AddToClassList("nt-library-card-path");
            foot.Add(path);

            var pin = new Button(() =>
            {
                library.TogglePin(entry.Path);
                OnLibraryChanged?.Invoke();
                RebuildGrids();
            }) { text = entry.Pinned ? "◆" : "◇" };
            pin.AddToClassList("nt-library-pin-btn");
            if (entry.Pinned) pin.AddToClassList("nt-library-pin-btn--on");
            // Don't let the pin click open the project.
            pin.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            foot.Add(pin);

            info.Add(foot);
            card.Add(info);

            return card;
        }

        private static UnityEngine.Color ThumbBg(string hueKey) => hueKey switch
        {
            "te" => ParseColor("#0c2018"),
            "pu" => ParseColor("#181428"),
            "bl" => ParseColor("#0c1620"),
            "am" => ParseColor("#201808"),
            "rd" => ParseColor("#200c0c"),
            _    => ParseColor("#181818"),
        };

        private static UnityEngine.Color ParseColor(string hex)
        {
            UnityEngine.ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
