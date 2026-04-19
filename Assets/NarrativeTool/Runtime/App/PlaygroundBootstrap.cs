using NarrativeTool.Canvas;
using NarrativeTool.Core;
using NarrativeTool.Data;
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

        private GraphCanvas canvas;
        private SessionState session;
        private ContextMenuController contextMenu;

        private void Awake()
        {
            Services.Clear();

            var bus = new EventBus();
            Services.Register(bus);
            Services.Register(new ContextMenuController());

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

            var project = BuildTestProject();
            var graph = project.Graphs[0];

            canvas = new GraphCanvas();
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
            var graph = new GraphDocument("graph_01", "Main");
            project.Graphs.Add(graph);

            var start = new StartNode("n_start", new Vector2(80, 140));
            var text = new TextNode("n_text", "Text Node", new Vector2(340, 140), "Hello, traveller.");
            var end = new EndNode("n_end", new Vector2(680, 140));

            graph.Nodes.Add(start);
            graph.Nodes.Add(text);
            graph.Nodes.Add(end);

            graph.Edges.Add(new Edge("e1", start.Id, StartNode.OutputPortId,
                                           text.Id, TextNode.InputPortId));
            graph.Edges.Add(new Edge("e2", text.Id, TextNode.OutputPortId,
                                           end.Id, EndNode.InputPortId));

            return project;
        }
    }
}