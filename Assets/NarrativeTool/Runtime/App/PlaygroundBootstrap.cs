using NarrativeTool.Canvas;
using NarrativeTool.Core;
using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using NarrativeTool.Data.Project;
using NarrativeTool.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.App
{
    /// <summary>
    /// Playground scene bootstrap. Creates app-global services (EventBus,
    /// ContextMenuController), per-project SessionState, and binds the
    /// canvas to the demo graph.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PlaygroundBootstrap : MonoBehaviour
    {
        [SerializeField] private StyleSheet theme;

        private GraphView canvas;
        private SessionState session;
        private ContextMenuController contextMenu;

        private void Awake()
        {
            Services.Clear();

            var bus = new EventBus();
            Services.Register(bus);
            Services.Register(new ContextMenuController());

            var nodeRegistry = new NodeRegistry();
            nodeRegistry.RegisterBuiltInTypes();   // scans attributes
            Services.Register(nodeRegistry);

            session = new SessionState(bus);
            contextMenu = Services.Get<ContextMenuController>();
        }

        private void Start()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc.rootVisualElement;
            root.style.flexGrow = 1;

            var sheet = theme != null ? theme : Resources.Load<StyleSheet>("Theme");
            if (sheet != null) root.styleSheets.Add(sheet);
            else Debug.LogWarning("[Playground] Theme.uss not found. Drop it into a Resources folder or assign it on PlaygroundBootstrap.");

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

            var project = BuildTestProject();
            var graph = project.Graphs[0];
            session.Project = project;

            // Layout: variables panel on the left, canvas filling the rest.
            var split = new VisualElement();
            split.AddToClassList("nt-root");
            split.style.flexDirection = FlexDirection.Row;
            split.style.flexGrow = 1;
            root.Add(split);

            var sidebar = new ProjectSidebar();
            split.Add(sidebar);
            sidebar.Bind(project, session, contextMenu);

            canvas = new GraphView();
            canvas.style.flexGrow = 1;
            split.Add(canvas);

            canvas.Bind(graph, session, contextMenu);
            canvas.Focus();

            Debug.Log("[Playground] Ready. Right-click canvas to add a node. Click to select (undoable). Delete / Backspace removes selection. Ctrl+Z / Ctrl+Shift+Z undo/redo.");
        }

        private static ProjectModel BuildTestProject()
        {
            var project = new ProjectModel { Id = "proj_01", Name = "Playground" };
            var graph = new GraphData("graph_01", "Main");
            project.Graphs.Add(graph);

            var start = new StartNodeData("n_start", new Vector2(80, 140));
            var text = new TextNodeData("n_text", "Text Node", new Vector2(340, 140), "Hello, traveller.");
            var dialog = new TestNodeData("n_dialog", "Dialog Node", new Vector2(500, 140));
            var end = new EndNodeData("n_end", new Vector2(720, 140));

            graph.Nodes.Add(start);
            graph.Nodes.Add(text);
            graph.Nodes.Add(dialog);
            graph.Nodes.Add(end);

            graph.Edges.Add(new Edge("e1", start.Id, StartNodeData.OutputPortId,
                                           text.Id, TextNodeData.InputPortId));
            graph.Edges.Add(new Edge("e2", text.Id, TextNodeData.OutputPortId,
                                           dialog.Id, TestNodeData.InputPortId));
            graph.Edges.Add(new Edge("e3", dialog.Id, TestNodeData.OutputPortId,
                                           end.Id, EndNodeData.InputPortId));

            // Seed an enum type so enum-typed variables have something to bind to.
            var moodEnum = new EnumDefinition("enum_seed_mood", "Mood");
            moodEnum.Members.Add(new EnumMember("mood_happy", "Happy"));
            moodEnum.Members.Add(new EnumMember("mood_sad", "Sad"));
            moodEnum.Members.Add(new EnumMember("mood_neutral", "Neutral"));
            project.Enums.Enums.Add(moodEnum);

            // Seed an entity type so the Entities tab has something to render.
            var character = new EntityDefinition("ent_seed_character", "Character");
            character.Fields.Add(new EntityField("f_name", "name", VariableType.String, ""));
            character.Fields.Add(new EntityField("f_age", "age", VariableType.Int, 0));
            character.Fields.Add(new EntityField("f_mood", "mood", VariableType.Enum, "mood_neutral", "enum_seed_mood"));
            project.Entities.Entities.Add(character);

            // Seed variables and folders so the panel has something to render.
            project.Variables.Folders.Add("player");
            project.Variables.Folders.Add("world");   // deliberately empty
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
    }
}