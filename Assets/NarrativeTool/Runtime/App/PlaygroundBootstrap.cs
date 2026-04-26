using NarrativeTool.Canvas;
using NarrativeTool.Core;
using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using NarrativeTool.Data.Project;
using NarrativeTool.UI;
using NarrativeTool.UI.Library;
using System;
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

            session = new SessionState(bus);
            contextMenu = Services.Get<ContextMenuController>();

            library = new ProjectLibrary();
            SeedLibrary(library);
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
                // TODO persistence: actually load the project file at
                // entry.Path. For now we just spin up a fresh demo project
                // tagged with the entry's name so the editor opens and the
                // wiring is exercised end-to-end.
                var project = BuildDemoProject(entry.Name);
                library.RegisterOpened(entry);
                OpenEditor(project);
            };
            screen.OnNewProject = ShowWizard;
            screen.OnOpenFile = () =>
            {
                // TODO: open a real file picker for .nproj files. For now,
                // route this to the wizard so the path is at least usable.
                Debug.Log("[Library] Open File — TODO: file picker. Falling through to New Project wizard.");
                ShowWizard();
            };
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
                    var entry = new ProjectLibraryEntry
                    {
                        Name = project.Name,
                        Path = result.SaveLocation + project.Name + ".nproj",
                        OpenedDisplay = "Just now",
                        Pinned = false,
                        NodeCount = 0,
                        EdgeCount = 0,
                        ThumbHueKey = "gr",
                    };
                    library.RegisterOpened(entry);
                    OpenEditor(project);
                },
            };
            root.Add(wiz);
        }

        private void OpenEditor(ProjectModel project)
        {
            session.Project = project;
            ClearRoot();

            var split = new VisualElement();
            split.AddToClassList("nt-root");
            split.style.flexDirection = FlexDirection.Row;
            split.style.flexGrow = 1;
            root.Add(split);

            var sidebar = new ProjectSidebar();
            split.Add(sidebar);
            sidebar.Bind(project, session, contextMenu);

            var canvas = new GraphView();
            canvas.style.flexGrow = 1;
            split.Add(canvas);

            canvas.Bind(project.Graphs[0], session, contextMenu);
            canvas.Focus();

            // TODO: a "Back to Library" affordance somewhere in the editor
            // chrome (top-bar button or menu) that calls ShowLibrary().
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
