using NarrativeTool.Canvas;
using NarrativeTool.Core;
using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using NarrativeTool.Data.Project;
using NarrativeTool.Data.Serialization;
using NarrativeTool.Playback;
using NarrativeTool.Playback.Handlers;
using NarrativeTool.UI;
using NarrativeTool.UI.Library;
using NarrativeTool.UI.Playback;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.App
{
    /// <summary>
    /// App bootstrap. Sets up shared services, then shows the project
    /// library start screen. When the user opens or creates a project, the
    /// root view is swapped for the editor (sidebar + canvas).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PlaygroundBootstrap : MonoBehaviour
    {
        [SerializeField] private StyleSheet theme;

        private SessionState session;
        private ContextMenuController contextMenu;
        private ProjectLibrary library;

        private VisualElement root;

        private void Awake()
        {
            Services.Clear();

            var bus = new EventBus();
            Services.Register(bus);
            Services.Register(new ContextMenuController());

            var nodeRegistry = new NodeRegistry();
            nodeRegistry.RegisterBuiltInTypes();
            Services.Register(nodeRegistry);

            // Runtime/playback handlers — one impl per built-in node type.
            // External node types should register their own handler the same
            // way before they're first played.
            var playbackRegistry = new PlaybackRegistry();
            playbackRegistry.Register<StartNodeData>(new StartNodeRuntime());
            playbackRegistry.Register<EndNodeData>(new EndNodeRuntime());
            playbackRegistry.Register<TextNodeData>(new TextNodeRuntime());
            playbackRegistry.Register<DialogNodeData>(new DialogNodeRuntime());
            playbackRegistry.Register<TestNodeData>(new TestNodeRuntime());
            playbackRegistry.Register<ChoiceNodeData>(new ChoiceNodeRuntime());
            playbackRegistry.Register<ConditionNodeData>(new ConditionNodeRuntime());
            Services.Register(playbackRegistry);

            session = new SessionState(bus);
            contextMenu = Services.Get<ContextMenuController>();

            library = new ProjectLibrary();
            // Try to load the persisted library; fall back to mockup seeds
            // if there's nothing on disk yet (first launch).
            if (!LibrarySerializer.Load(library))
            {
                SeedLibrary(library);
                LibrarySerializer.Save(library);
            }
        }

        private void Start()
        {
            var doc = GetComponent<UIDocument>();
            root = doc.rootVisualElement;
            root.style.flexGrow = 1;

            var sheet = theme != null ? theme : Resources.Load<StyleSheet>("Theme");
            if (sheet != null) root.styleSheets.Add(sheet);
            else Debug.LogWarning("[Bootstrap] Theme.uss not found. Drop it into a Resources folder or assign it on PlaygroundBootstrap.");

            contextMenu.SetRootHost(root);
            contextMenu.RegisterProvider(new CanvasContextMenuProvider());
            contextMenu.RegisterProvider(new NodeContextMenuProvider());
            contextMenu.RegisterProvider(new EdgeContextMenuProvider());
            contextMenu.RegisterProvider(new WaypointContextMenuProvider());
            contextMenu.RegisterProvider(new EdgeDropContextMenuProvider());
            contextMenu.RegisterProvider(new VariableContextMenuProvider());
            contextMenu.RegisterProvider(new VariableFolderContextMenuProvider());
            contextMenu.RegisterProvider(new EntityContextMenuProvider());
            contextMenu.RegisterProvider(new EntityFolderContextMenuProvider());
            contextMenu.RegisterProvider(new EnumDefContextMenuProvider());
            contextMenu.RegisterProvider(new EnumFolderContextMenuProvider());

            ShowLibrary();
        }

        // ───────── Screens ─────────

        private void ShowLibrary()
        {
            // Drop any open project's per-graph state when returning here.
            session.Clear();
            contextMenu.Close();

            ClearRoot();

            var screen = new LibraryScreen();
            screen.OnOpenProject = entry =>
            {
                // Try to load from disk; fall back to a fresh demo project
                // (tagged with the entry's name) if the file is missing —
                // happens for the seeded mockup entries on first launch.
                var project = ProjectSerializer.Load(entry.Path)
                              ?? BuildDemoProject(entry.Name);
                library.RegisterOpened(entry);
                LibrarySerializer.Save(library);
                OpenEditor(project, entry.Path, entry);
            };
            screen.OnNewProject = ShowWizard;
            screen.OnOpenFile = () =>
            {
                // TODO: open a real file picker for .nproj files. For now,
                // route this to the wizard so the path is at least usable.
                Debug.Log("[Library] Open File — TODO: file picker. Falling through to New Project wizard.");
                ShowWizard();
            };
            screen.OnLibraryChanged = () => LibrarySerializer.Save(library);
            screen.Bind(library);
            root.Add(screen);
        }

        private void ShowWizard()
        {
            var wiz = new NewProjectWizard
            {
                OnCancel = () => { /* RemoveFromHierarchy handled by the wizard */ },
                OnCreate = result =>
                {
                    var project = BuildBlankProject(result);
                    // Wizard-supplied save location is decorative for now;
                    // route everything through the default scheme so the
                    // file actually lands somewhere persistent.
                    var path = ProjectSerializer.DefaultPathFor(project.Name);
                    ProjectSerializer.Save(project, path);

                    var entry = new ProjectLibraryEntry
                    {
                        Name = project.Name,
                        Path = path,
                        OpenedDisplay = "Just now",
                        Pinned = false,
                        NodeCount = project.Graphs[0].Nodes.Count,
                        EdgeCount = project.Graphs[0].Edges.Count,
                        ThumbHueKey = "gr",
                    };
                    library.RegisterOpened(entry);
                    LibrarySerializer.Save(library);
                    OpenEditor(project, path, entry);
                },
            };
            root.Add(wiz);
        }

        private ProjectLibraryEntry currentEntry;

        private void OpenEditor(ProjectModel project, string path, ProjectLibraryEntry entry)
        {
            session.Project = project;
            session.ProjectPath = path;
            currentEntry = entry;

            ClearRoot();

            var split = new VisualElement();
            split.AddToClassList("nt-root");
            split.style.flexDirection = FlexDirection.Row;
            split.style.flexGrow = 1;
            split.focusable = true;
            root.Add(split);

            var sidebar = new ProjectSidebar();
            split.Add(sidebar);
            sidebar.Bind(project, session, contextMenu);

            // Right pane: canvas on top, playback overlay docked below.
            var rightPane = new VisualElement();
            rightPane.style.flexGrow = 1;
            rightPane.style.flexDirection = FlexDirection.Column;
            split.Add(rightPane);

            var canvas = new GraphView();
            canvas.style.flexGrow = 1;
            rightPane.Add(canvas);

            canvas.Bind(project.Graphs[0], session, contextMenu);
            canvas.Focus();

            var overlay = new PlaybackOverlay(project, project.Graphs[0],
                                              Services.Get<PlaybackRegistry>(), canvas);
            rightPane.Add(overlay);

            // "Start playback here" on a node's right-click menu routes
            // through the GraphView so the bootstrap can hand it to the
            // overlay without canvas-internal coupling.
            canvas.OnStartPlayback = nodeId => overlay.StartAt(nodeId);

            // Ctrl+S writes the open project back to its file. Registered
            // at root so it works regardless of which subview has focus.
            split.RegisterCallback<KeyDownEvent>(e =>
            {
                if ((e.ctrlKey || e.commandKey) && e.keyCode == KeyCode.S)
                {
                    SaveCurrent();
                    e.StopPropagation();
                }
            });

            // TODO: a "Back to Library" affordance somewhere in the editor
            // chrome (top-bar button or menu) that calls ShowLibrary().
        }

        private void SaveCurrent()
        {
            var project = session.Project;
            var path = session.ProjectPath;
            if (project == null || string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[Bootstrap] Save: no open project / path.");
                return;
            }
            ProjectSerializer.Save(project, path);

            // Refresh the entry's stat counts so the library tile reflects
            // the latest state on next render.
            if (currentEntry != null && project.Graphs.Count > 0)
            {
                var g = project.Graphs[0];
                currentEntry.NodeCount = g.Nodes.Count;
                currentEntry.EdgeCount = g.Edges.Count;
                LibrarySerializer.Save(library);
            }
        }

        private void ClearRoot()
        {
            // Keep style sheets attached; just drop the visual children.
            for (int i = root.childCount - 1; i >= 0; i--)
                root.RemoveAt(i);
        }

        // ───────── Project factories ─────────

        private static ProjectModel BuildBlankProject(NewProjectResult result)
        {
            var id = "proj_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var project = new ProjectModel { Id = id, Name = result.ProjectName };
            // en-US is added by ProjectModel's initializer — append the rest.
            foreach (var l in result.Locales)
                if (l != "en-US" && !project.Locales.Contains(l))
                    project.Locales.Add(l);

            var graph = new GraphData("graph_main", "Main");
            project.Graphs.Add(graph);
            // Seed a single Start node so the canvas isn't empty.
            graph.Nodes.Add(new StartNodeData("n_start", new Vector2(120, 140)));
            return project;
        }

        private static ProjectModel BuildDemoProject(string name)
        {
            var project = new ProjectModel { Id = "proj_demo", Name = name };
            var graph = new GraphData("graph_01", "Main");
            project.Graphs.Add(graph);

            var start = new StartNodeData("n_start", new Vector2(80, 140));
            var text = new TextNodeData("n_text", "Text Node", new Vector2(340, 140), "Hello, traveller.");
            var dialog = new TestNodeData("n_dialog", "Dialog Node", new Vector2(500, 140));
            var end = new EndNodeData("n_end", new Vector2(720, 140));

            graph.Nodes.Add(start); graph.Nodes.Add(text);
            graph.Nodes.Add(dialog); graph.Nodes.Add(end);

            graph.Edges.Add(new Edge("e1", start.Id, StartNodeData.OutputPortId,
                                           text.Id, TextNodeData.InputPortId));
            graph.Edges.Add(new Edge("e2", text.Id, TextNodeData.OutputPortId,
                                           dialog.Id, TestNodeData.InputPortId));
            graph.Edges.Add(new Edge("e3", dialog.Id, TestNodeData.OutputPortId,
                                           end.Id, EndNodeData.InputPortId));

            // Seed an enum, an entity, and a few variables (mirrors the
            // previous BuildTestProject content).
            var moodEnum = new EnumDefinition("enum_seed_mood", "Mood");
            moodEnum.Members.Add(new EnumMember("mood_happy", "Happy"));
            moodEnum.Members.Add(new EnumMember("mood_sad", "Sad"));
            moodEnum.Members.Add(new EnumMember("mood_neutral", "Neutral"));
            project.Enums.Enums.Add(moodEnum);

            var character = new EntityDefinition("ent_seed_character", "Character");
            character.Fields.Add(new EntityField("f_name", "name", VariableType.String, ""));
            character.Fields.Add(new EntityField("f_age", "age", VariableType.Int, 0));
            character.Fields.Add(new EntityField("f_mood", "mood", VariableType.Enum, "mood_neutral", "enum_seed_mood"));
            project.Entities.Entities.Add(character);

            project.Variables.Folders.Add("player");
            project.Variables.Folders.Add("world");
            project.Variables.Variables.Add(new VariableDefinition(
                "var_seed_rep", "reputation", VariableType.Int, 0, "player"));
            project.Variables.Variables.Add(new VariableDefinition(
                "var_seed_met", "hasMetElara", VariableType.Bool, false, "player"));
            project.Variables.Variables.Add(new VariableDefinition(
                "var_seed_mood", "mood", VariableType.Enum, "mood_neutral", "player",
                enumTypeId: "enum_seed_mood"));
            project.Variables.Variables.Add(new VariableDefinition(
                "var_seed_act", "act", VariableType.Int, 1, ""));

            return project;
        }

        private static void SeedLibrary(ProjectLibrary lib)
        {
            // TODO persistence: replace this with a real load from disk.
            // The seeds below mirror the mockup data so the screen has
            // something to render on first launch.
            lib.Entries.Add(new ProjectLibraryEntry { Name = "Thornwood Chronicles", Path = "/projects/thornwood/thornwood.nproj", OpenedDisplay = "2 hours ago",      Pinned = true,  NodeCount = 42, EdgeCount = 61, ThumbHueKey = "te" });
            lib.Entries.Add(new ProjectLibraryEntry { Name = "Echoes of Kael",       Path = "/projects/kael/kael.nproj",            OpenedDisplay = "Yesterday, 14:32", Pinned = true,  NodeCount = 18, EdgeCount = 22, ThumbHueKey = "pu" });
            lib.Entries.Add(new ProjectLibraryEntry { Name = "Station Nine",         Path = "/projects/station9/station9.nproj",    OpenedDisplay = "3 days ago",       Pinned = false, NodeCount = 94, EdgeCount = 130, ThumbHueKey = "bl" });
            lib.Entries.Add(new ProjectLibraryEntry { Name = "Miriam — Demo",        Path = "/demos/miriam/miriam.nproj",           OpenedDisplay = "1 week ago",       Pinned = false, NodeCount = 11, EdgeCount = 9,   ThumbHueKey = "am" });
            lib.Entries.Add(new ProjectLibraryEntry { Name = "Untitled Project",     Path = "/projects/untitled/untitled.nproj",    OpenedDisplay = "2 weeks ago",      Pinned = false, NodeCount = 3,  EdgeCount = 1,   ThumbHueKey = "gr" });
            lib.Entries.Add(new ProjectLibraryEntry { Name = "Lowland Heist [WIP]",  Path = "/projects/lowland/lowland.nproj",      OpenedDisplay = "3 weeks ago",      Pinned = false, NodeCount = 27, EdgeCount = 38,  ThumbHueKey = "rd" });
        }
    }
}
