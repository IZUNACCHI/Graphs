using NarrativeTool.Canvas.Core;
using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Data.Project;
using NarrativeTool.UI.Debugger;
using NarrativeTool.UI.MenuBar;
using NarrativeTool.UI.Runtime;
using NarrativeTool.UI.Toolbar;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.UI
{
    /// <summary>Bundle of callbacks that connect <see cref="MainWindow"/> to the host
    /// (currently <c>PlaygroundBootstrap</c>). Keeps the window decoupled from
    /// project loading / runtime engine lifecycle.</summary>
    public sealed class MainWindowCallbacks
    {
        public Action OnBackToLibrary;
        public Action OnSave;
        public Action OnSaveAll;
        public Action OnPlayProject;
        public Action OnPlayGraph;
        public Action OnStop;
        public Func<bool> IsRunning;
        public Func<string> ProjectTitle;
    }

    /// <summary>
    /// Top-level editor window: menu bar + toolbar + body. The body is currently the
    /// legacy three-column layout; in Phase 2 it becomes a <c>DockRoot</c> tree.
    /// </summary>
    public sealed class MainWindow : VisualElement
    {
        private readonly ProjectModel project;
        private readonly SessionState session;
        private readonly ContextMenuController contextMenu;
        private readonly MainWindowCallbacks callbacks;

        public MenuBar.MenuBar MenuBarView { get; private set; }
        public Toolbar.Toolbar ToolbarView { get; private set; }

        // Body (Phase 1: legacy layout) — exposed so the host can wire runtime engine.
        public ProjectSidebar Sidebar     { get; private set; }
        public GraphTabManager TabManager { get; private set; }
        public RuntimePanel RuntimePanel  { get; private set; }
        public DebuggerPanel DebuggerPanel { get; private set; }

        public MainWindow(ProjectModel project,
                          SessionState session,
                          ContextMenuController contextMenu,
                          MainWindowCallbacks callbacks)
        {
            this.project = project;
            this.session = session;
            this.contextMenu = contextMenu;
            this.callbacks = callbacks ?? new MainWindowCallbacks();

            AddToClassList("nt-mainwindow");
            style.flexDirection = FlexDirection.Column;
            style.flexGrow = 1;
            focusable = true;

            // Registries are global — clear so we don't get duplicates if the editor
            // is opened more than once during a session.
            MenuBarRegistry.Clear();
            ToolbarRegistry.Clear();

            BuildMenuBar();
            BuildToolbar();
            BuildBody();
            RegisterShortcuts();
        }

        // ───────────────────────── Menu bar ─────────────────────────

        private void BuildMenuBar()
        {
            MenuBarView = new MenuBar.MenuBar();
            Add(MenuBarView);

            // File
            MenuBarRegistry.Register(new MenuItemDescriptor {
                Menu = "File", Path = "Save", Shortcut = "Ctrl S", Order = 0,
                Action = () => callbacks.OnSave?.Invoke() });
            MenuBarRegistry.Register(new MenuItemDescriptor {
                Menu = "File", Path = "Save All", Shortcut = "Ctrl Shift S", Order = 10,
                Action = () => callbacks.OnSaveAll?.Invoke(),
                IsSeparatorAfter = true });
            MenuBarRegistry.Register(new MenuItemDescriptor {
                Menu = "File", Path = "Close Project", Order = 20,
                Action = () => callbacks.OnBackToLibrary?.Invoke() });

            // Edit (placeholders — wire to actual commands later)
            MenuBarRegistry.Register(new MenuItemDescriptor {
                Menu = "Edit", Path = "Undo", Shortcut = "Ctrl Z", Order = 0,
                Action = () => Debug.Log("[Edit] Undo (TODO)") });
            MenuBarRegistry.Register(new MenuItemDescriptor {
                Menu = "Edit", Path = "Redo", Shortcut = "Ctrl Y", Order = 10,
                Action = () => Debug.Log("[Edit] Redo (TODO)") });

            // Tools
            MenuBarRegistry.Register(new MenuItemDescriptor {
                Menu = "Tools", Path = "Validate Project", Order = 0,
                Action = () => Debug.Log("[Tools] Validate Project (TODO)") });

            // Settings
            MenuBarRegistry.Register(new MenuItemDescriptor {
                Menu = "Settings", Path = "Preferences…", Order = 0,
                Action = () => Debug.Log("[Settings] Preferences (TODO)") });
            MenuBarRegistry.Register(new MenuItemDescriptor {
                Menu = "Settings", Path = "Reset Layout", Order = 10,
                Action = () => Debug.Log("[Settings] Reset Layout (TODO – Phase 2)") });
        }

        // ───────────────────────── Toolbar ─────────────────────────

        private void BuildToolbar()
        {
            ToolbarView = new Toolbar.Toolbar();
            Add(ToolbarView);

            // Left: navigation + project controls
            ToolbarRegistry.Register(new ToolbarItemDescriptor {
                Id = "nav.library", Side = ToolbarSide.Left, Group = "navigation", Order = 0,
                Build = () => MakeButton("◀ Library", () => callbacks.OnBackToLibrary?.Invoke(), "nt-toolbar__btn--library") });
            ToolbarRegistry.Register(new ToolbarItemDescriptor {
                Id = "nav.title", Side = ToolbarSide.Left, Group = "navigation", Order = 10,
                Build = () =>
                {
                    var lbl = new Label(callbacks.ProjectTitle?.Invoke() ?? project?.Name ?? "");
                    lbl.AddToClassList("nt-toolbar__title");
                    return lbl;
                } });
            ToolbarRegistry.Register(new ToolbarItemDescriptor {
                Id = "project.save", Side = ToolbarSide.Left, Group = "project", Order = 0,
                Build = () => MakeButton("Save", () => callbacks.OnSave?.Invoke()) });
            ToolbarRegistry.Register(new ToolbarItemDescriptor {
                Id = "project.saveAll", Side = ToolbarSide.Left, Group = "project", Order = 10,
                Build = () => MakeButton("Save All", () => callbacks.OnSaveAll?.Invoke()) });

            // Center: run controls
            ToolbarRegistry.Register(new ToolbarItemDescriptor {
                Id = "run.playProject", Side = ToolbarSide.Center, Group = "run", Order = 0,
                IsVisible = () => !(callbacks.IsRunning?.Invoke() ?? false),
                Build = () => MakeButton("▶ Project", () => callbacks.OnPlayProject?.Invoke(), "run-toolbar__btn", "run-toolbar__btn--project") });
            ToolbarRegistry.Register(new ToolbarItemDescriptor {
                Id = "run.playGraph", Side = ToolbarSide.Center, Group = "run", Order = 10,
                IsVisible = () => !(callbacks.IsRunning?.Invoke() ?? false),
                Build = () => MakeButton("▶ Graph", () => callbacks.OnPlayGraph?.Invoke(), "run-toolbar__btn", "run-toolbar__btn--graph") });
            ToolbarRegistry.Register(new ToolbarItemDescriptor {
                Id = "run.stop", Side = ToolbarSide.Center, Group = "run", Order = 20,
                IsVisible = () => callbacks.IsRunning?.Invoke() ?? false,
                Build = () => MakeButton("■ Stop", () => callbacks.OnStop?.Invoke(), "run-toolbar__btn", "run-toolbar__btn--stop") });
        }

        /// <summary>Re-evaluate <c>IsVisible</c> predicates of toolbar items.
        /// Called by the host whenever runtime state changes.</summary>
        public void NotifyRuntimeStateChanged() => ToolbarView?.Refresh();

        private static Button MakeButton(string text, Action action, params string[] extraClasses)
        {
            var b = new Button(action) { text = text };
            b.AddToClassList("nt-toolbar__btn");
            if (extraClasses != null)
                foreach (var c in extraClasses)
                    if (!string.IsNullOrEmpty(c)) b.AddToClassList(c);
            return b;
        }

        // ───────────────────────── Body (Phase 1: legacy three-column layout) ─────────────────────────

        private void BuildBody()
        {
            var split = new VisualElement();
            split.AddToClassList("nt-root");
            split.style.flexDirection = FlexDirection.Row;
            split.style.flexGrow = 1;
            split.focusable = true;
            Add(split);

            Sidebar = new ProjectSidebar();
            split.Add(Sidebar);
            Sidebar.Bind(project, session, contextMenu);

            TabManager = new GraphTabManager(session, contextMenu);
            TabManager.SubscribeToEvents(session.Bus);
            TabManager.style.flexGrow = 1;
            split.Add(TabManager);

            var firstGraphLazy = project?.Graphs.Items.FirstOrDefault();
            if (firstGraphLazy != null)
                TabManager.OpenGraph(firstGraphLazy);

            Sidebar.OnGraphDoubleClicked += lazy => TabManager.OpenGraph(lazy);

            RuntimePanel = new RuntimePanel();
            RuntimePanel.style.width = 250;
            RuntimePanel.style.display = DisplayStyle.None;
            split.Add(RuntimePanel);

            DebuggerPanel = new DebuggerPanel();
            DebuggerPanel.style.width = 280;
            DebuggerPanel.style.display = DisplayStyle.None;
            split.Add(DebuggerPanel);
        }

        // ───────────────────────── Shortcuts ─────────────────────────

        private void RegisterShortcuts()
        {
            RegisterCallback<KeyDownEvent>(e =>
            {
                if ((e.ctrlKey || e.commandKey) && e.keyCode == KeyCode.S)
                {
                    if (e.shiftKey) callbacks.OnSaveAll?.Invoke();
                    else            callbacks.OnSave?.Invoke();
                    e.StopPropagation();
                }
            });
        }

        public void Teardown()
        {
            TabManager?.UnsubscribeEvents();
            ToolbarRegistry.Clear();
            MenuBarRegistry.Clear();
        }
    }
}
