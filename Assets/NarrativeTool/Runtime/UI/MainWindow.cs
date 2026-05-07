using NarrativeTool.Canvas.Core;
using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Data.Project;
using NarrativeTool.UI.Debugger;
using NarrativeTool.UI.Docking;
using NarrativeTool.UI.Entities;
using NarrativeTool.UI.Graphs;
using NarrativeTool.UI.MenuBar;
using NarrativeTool.UI.Runtime;
using NarrativeTool.UI.Toolbar;
using NarrativeTool.UI.Variables;
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

        // Body — exposed so the host can wire runtime engine, save shortcuts, etc.
        public DockRoot Dock              { get; private set; }
        private DockDragManager dragManager;
        public GraphsPanel    GraphsPanelView    { get; private set; }
        public VariablesPanel VariablesPanelView { get; private set; }
        public EntitiesPanel  EntitiesPanelView  { get; private set; }
        public GraphCenterController CenterController { get; private set; }
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
            DockRegistry.Clear();

            // Order matters: panels are instantiated and registered first so the
            // menu bar can enumerate them; then menu/toolbar are mounted (top of
            // the column) before the body.
            CreatePanels();
            RegisterPanelDescriptors();
            BuildMenuBar();
            BuildToolbar();
            MountDock();
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
                Action = () => Debug.Log("[Settings] Preferences (TODO)"),
                IsSeparatorAfter = true });
            MenuBarRegistry.Register(new MenuItemDescriptor {
                Menu = "Settings", Path = "Reset Layout", Order = 10,
                Action = ResetLayout,
                IsSeparatorAfter = true });

            // Settings → Show <panel> for every registered dockable panel.
            int order = 100;
            foreach (var d in DockRegistry.All)
            {
                var captured = d;
                MenuBarRegistry.Register(new MenuItemDescriptor {
                    Menu = "Settings", Path = "Show " + captured.Title, Order = order,
                    IsChecked = () => Dock != null && Dock.IsOpen(captured.Id),
                    Action = () => TogglePanel(captured.Id),
                });
                order += 10;
            }
        }

        private void TogglePanel(string id)
        {
            if (Dock == null) return;
            if (Dock.IsOpen(id))
            {
                Dock.ClosePanel(id);
            }
            else
            {
                var d = DockRegistry.Find(id);
                if (d?.Factory != null)
                    Dock.OpenPanel(d.Factory(), d.DefaultZone);
            }
        }

        private void ResetLayout()
        {
            if (Dock == null) return;

            // Drop the persisted layout so next open uses defaults.
            try
            {
                if (System.IO.File.Exists(DockLayoutSerializer.DefaultPath))
                    System.IO.File.Delete(DockLayoutSerializer.DefaultPath);
            }
            catch (System.Exception ex) { Debug.LogWarning("[ResetLayout] " + ex.Message); }

            foreach (var d in DockRegistry.All) Dock.ClosePanel(d.Id);
            foreach (var d in DockRegistry.All)
            {
                var p = d.Factory?.Invoke();
                if (p != null) Dock.OpenPanel(p, d.DefaultZone);
            }
            Dock.RefreshZoneVisibility();
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

        // ───────────────────────── Body (DockRoot) ─────────────────────────

        private void CreatePanels()
        {
            GraphsPanelView    = new GraphsPanel();
            VariablesPanelView = new VariablesPanel();
            EntitiesPanelView  = new EntitiesPanel();
            GraphsPanelView.Bind(project, session, contextMenu);
            VariablesPanelView.Bind(project, session, contextMenu);
            EntitiesPanelView.Bind(project, session, contextMenu);

            RuntimePanel  = new RuntimePanel();
            RuntimePanel.style.display  = DisplayStyle.None;
            DebuggerPanel = new DebuggerPanel();
            DebuggerPanel.style.display = DisplayStyle.None;
        }

        private void MountDock()
        {
            Dock = new DockRoot();
            Add(Dock);

            // Try to restore a saved layout; if none, fall back to defaults.
            if (!DockLayoutSerializer.Load(Dock))
            {
                foreach (var d in DockRegistry.All)
                {
                    var panel = d.Factory?.Invoke();
                    if (panel != null) Dock.OpenPanel(panel, d.DefaultZone);
                }
            }
            Dock.RefreshZoneVisibility();

            // Drag-drop wiring (after panels are mounted so initial scan picks them up).
            dragManager = new DockDragManager(Dock);

            // Center controller drives graph-panel lifecycle on the real DockArea.
            CenterController = new GraphCenterController(Dock, session, contextMenu);
            CenterController.SubscribeToEvents(session.Bus);
            CenterController.OnCenterEmpty += () => EnsurePanelOpen("graphs");
            GraphsPanelView.OnGraphDoubleClicked += lazy => CenterController.OpenGraph(lazy);

            // Open the first graph automatically (preserves prior bootstrap UX).
            var firstGraphLazy = project?.Graphs.Items.FirstOrDefault();
            if (firstGraphLazy != null) CenterController.OpenGraph(firstGraphLazy);
        }

        /// <summary>Opens a panel by id if it isn't currently docked. Used by
        /// <see cref="GraphCenterController"/> when the last graph closes — we
        /// re-open the Graphs sidebar so the user can find their way back.</summary>
        public void EnsurePanelOpen(string id)
        {
            if (Dock == null || string.IsNullOrEmpty(id)) return;
            if (Dock.IsOpen(id)) return;
            var d = DockRegistry.Find(id);
            if (d?.Factory == null) return;
            Dock.OpenPanel(d.Factory(), d.DefaultZone);
        }

        private void RegisterPanelDescriptors()
        {
            DockRegistry.Register(new DockablePanelDescriptor {
                Id = "graphs", Title = "Graphs",
                DefaultZone = DockZoneKind.Left, DefaultOrder = 0,
                Factory = () => new DockablePanelAdapter("graphs", "Graphs", GraphsPanelView)
            });
            DockRegistry.Register(new DockablePanelDescriptor {
                Id = "variables", Title = "Variables",
                DefaultZone = DockZoneKind.Left, DefaultOrder = 10,
                Factory = () => new DockablePanelAdapter("variables", "Variables", VariablesPanelView)
            });
            DockRegistry.Register(new DockablePanelDescriptor {
                Id = "entities", Title = "Entities",
                DefaultZone = DockZoneKind.Left, DefaultOrder = 20,
                Factory = () => new DockablePanelAdapter("entities", "Entities", EntitiesPanelView)
            });
            DockRegistry.Register(new DockablePanelDescriptor {
                Id = "runtime", Title = "Runtime",
                DefaultZone = DockZoneKind.Right, DefaultOrder = 0,
                Factory = () => new DockablePanelAdapter("runtime", "Runtime", RuntimePanel)
            });
            DockRegistry.Register(new DockablePanelDescriptor {
                Id = "debugger", Title = "Debugger",
                DefaultZone = DockZoneKind.Right, DefaultOrder = 10,
                Factory = () => new DockablePanelAdapter("debugger", "Debugger", DebuggerPanel)
            });
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
            // Persist current layout before tearing down (per-user, global).
            if (Dock != null) DockLayoutSerializer.Save(Dock);

            CenterController?.UnsubscribeEvents();
            ToolbarRegistry.Clear();
            MenuBarRegistry.Clear();
            DockRegistry.Clear();
        }
    }
}
