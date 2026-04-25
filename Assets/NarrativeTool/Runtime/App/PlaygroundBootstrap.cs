using NarrativeTool.Canvas;
using NarrativeTool.Core;
using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using NarrativeTool.Data.Project;
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

            var project = BuildTestProject();
            var graph = project.Graphs[0];

            canvas = new GraphView();
            canvas.AddToClassList("nt-root");
            canvas.style.flexGrow = 1;
            root.Add(canvas);

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

            return project;
        }
    }
}