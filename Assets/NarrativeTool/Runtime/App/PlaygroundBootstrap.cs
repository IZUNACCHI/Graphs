// ===== File: Assets/NarrativeTool/Runtime/App/PlaygroundBootstrap.cs =====
using NarrativeTool.Canvas;
using NarrativeTool.Core;
using NarrativeTool.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.App
{
    /// <summary>
    /// Attach to a GameObject with a UIDocument. Builds a tiny
    /// Start → TextNode → End graph for play-testing the canvas.
    ///
    /// Ctrl+Z / Ctrl+Y go through our CommandSystem everywhere (including
    /// when a text field is focused) for consistency.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PlaygroundBootstrap : MonoBehaviour
    {
        [SerializeField] private StyleSheet theme;

        private GraphCanvas canvas;
        private CommandSystem commands;

        private void Awake()
        {
            Services.Clear();
            Services.Register(new EventBus());
            Services.Register(new CommandSystem());
            commands = Services.Get<CommandSystem>();
        }

        private void Start()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc.rootVisualElement;
            root.style.flexGrow = 1;

            var sheet = theme != null ? theme : Resources.Load<StyleSheet>("Theme");
            if (sheet != null) root.styleSheets.Add(sheet);
            else Debug.LogWarning("[Playground] Theme.uss not found. Drop it into a Resources folder or assign it on PlaygroundBootstrap.");

            var project = BuildTestProject();
            var graph = project.Graphs[0];

            canvas = new GraphCanvas();
            canvas.AddToClassList("nt-root");
            canvas.style.flexGrow = 1;
            root.Add(canvas);

            canvas.Bind(graph, Services.Get<EventBus>(), commands);
            canvas.Focus();

            // Capture Ctrl+Z/Y at the root, before any inner focus target sees it.
            root.RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);

            Debug.Log("[Playground] Ready. Drag by the node header. Middle-mouse pan, wheel zoom, Ctrl+Z/Y undo/redo.");
        }

        private void OnKey(KeyDownEvent e)
        {
            bool ctrl = e.ctrlKey || e.commandKey;
            if (!ctrl) return;

            if (e.keyCode == KeyCode.Z && !e.shiftKey) { commands.Undo(); e.StopPropagation(); }
            else if (e.keyCode == KeyCode.Z && e.ctrlKey || (e.keyCode == KeyCode.Z && e.shiftKey))
            { commands.Redo(); e.StopPropagation();; }
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