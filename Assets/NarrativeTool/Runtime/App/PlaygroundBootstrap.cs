using NarrativeTool.Canvas.Core;
using NarrativeTool.Core;
using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Core.ContextMenu.Providers;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Core.Runtime;
using NarrativeTool.Core.Scripting;
using NarrativeTool.Core.Scripting.Editors;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using NarrativeTool.Data.Project;
using NarrativeTool.Data.Serialization;
using NarrativeTool.UI;
using NarrativeTool.UI.Library;
using NarrativeTool.UI.Runtime;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.App
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class PlaygroundBootstrap : MonoBehaviour
    {
        [SerializeField] private StyleSheet theme;

        // ── Core services ──
        private SessionState session;
        private ContextMenuController contextMenu;
        private ProjectLibrary library;

        // ── Editor / canvas ──
        private VisualElement root;
        private GraphTabManager tabManager;

        // ── Runtime ──
        private RuntimeEngine runtimeEngine;
        private RuntimePanel runtimePanel;
        private IDisposable runtimeStateSub;

        // ── Toolbar controls ──
        private Button btnPlayGraph, btnStopRuntime;
        private Label runIndicator;

        private void Awake()
        {
            Services.Clear();

            var bus = new EventBus();
            Services.Register(bus);
            Services.Register(new ContextMenuController());

            var nodeRegistry = new NodeRegistry();
            nodeRegistry.RegisterBuiltInTypes();
            Services.Register(nodeRegistry);

            var executorRegistry = new NodeExecutorRegistry();
            executorRegistry.ScanAssemblies();
            Services.Register(executorRegistry);

            session = new SessionState(bus);
            contextMenu = Services.Get<ContextMenuController>();

            // Serialization
            SerializerRegistry.Register(new JsonNetSerializer());
            SerializerRegistry.SetCurrent("json");

            library = new ProjectLibrary();
            LibrarySerializer.Load(library);

            var scriptRegistry = new ScriptingEditorRegistry();
            scriptRegistry.Register(new TextScriptingEditor());
            Services.Register(scriptRegistry);

            // Lua scripting backend
            var scriptingBackend = new LuaScriptingBackend(null, null);  // will be connected at runtime
            Services.Register<IScriptingBackend>(scriptingBackend);
        }

        private void Start()
        {
            var doc = GetComponent<UIDocument>();
            root = doc.rootVisualElement;
            root.style.flexGrow = 1;

            var sheet = theme != null ? theme : Resources.Load<StyleSheet>("Theme");
            if (sheet != null) root.styleSheets.Add(sheet);
            else Debug.LogWarning("[Bootstrap] Theme.uss not found.");

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
            contextMenu.RegisterProvider(new GraphContextMenuProvider());
            contextMenu.RegisterProvider(new GraphFolderContextMenuProvider());

            ShowLibrary();
        }

        // ───────── Screens ─────────

        private void ShowLibrary()
        {
            session.Clear();
            contextMenu.Close();
            CloseEditor();

            ClearRoot();

            var screen = new LibraryScreen();
            screen.OnOpenProject = entry =>
            {
                var project = ProjectSerializer.Load(entry.Path);
                library.RegisterOpened(entry);
                LibrarySerializer.Save(library);
                OpenEditor(project, entry.Path, entry);
            };
            screen.OnNewProject = ShowWizard;
            screen.OnOpenFile = () =>
            {
                Debug.Log("[Library] Open File — TODO: file picker.");
                ShowWizard();
            };
            screen.OnLibraryChanged = () => LibrarySerializer.Save(library);
            screen.Bind(library);
            root.Add(screen);
        }

        private void CloseEditor()
        {
            StopRuntime();
            tabManager?.UnsubscribeEvents();
            tabManager = null;
        }

        private void ShowWizard()
        {
            var wiz = new NewProjectWizard
            {
                OnCancel = () => { },
                OnCreate = result =>
                {
                    var project = BuildBlankProject(result);
                    var path = ProjectSerializer.UserPathFor(project.Name, result.SaveLocation);
                    ProjectSerializer.Save(project, path);

                    var entry = new ProjectLibraryEntry
                    {
                        Name = project.Name,
                        Path = path,
                        LastOpened = DateTime.Now,
                        Pinned = false,
                        GraphCount = 1,
                        NodeCount = 1,
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

            // ── Run toolbar (top bar) ──
            var runToolbar = new VisualElement();
            runToolbar.AddToClassList("run-toolbar");
            runToolbar.style.flexDirection = FlexDirection.Row;

            btnPlayGraph = new Button(() => StartRuntime());
            btnPlayGraph.text = "▶ Run Graph";
            btnPlayGraph.AddToClassList("run-toolbar__btn");
            btnPlayGraph.AddToClassList("run-toolbar__btn--graph");

            btnStopRuntime = new Button(() => StopRuntime());
            btnStopRuntime.text = "■ Stop";
            btnStopRuntime.AddToClassList("run-toolbar__btn");
            btnStopRuntime.AddToClassList("run-toolbar__btn--stop");

            runIndicator = new Label("");
            runIndicator.AddToClassList("run-toolbar__run-indicator");

            runToolbar.Add(btnPlayGraph);
            runToolbar.Add(btnStopRuntime);
            runToolbar.Add(runIndicator);
            root.Add(runToolbar);

            // ── Main split: sidebar + canvas + runtime panel ──
            var split = new VisualElement();
            split.AddToClassList("nt-root");
            split.style.flexDirection = FlexDirection.Row;
            split.style.flexGrow = 1;
            split.focusable = true;
            root.Add(split);

            var sidebar = new ProjectSidebar();
            split.Add(sidebar);
            sidebar.Bind(project, session, contextMenu);

            tabManager = new GraphTabManager(session, contextMenu);
            tabManager.SubscribeToEvents(session.Bus);
            tabManager.style.flexGrow = 1;
            split.Add(tabManager);

            // Open the first graph as a tab (if any)
            var firstGraphLazy = project.Graphs.Items.FirstOrDefault();
            if (firstGraphLazy != null)
                tabManager.OpenGraph(firstGraphLazy);

            // Subscribe to sidebar double‑click → open tab
            sidebar.OnGraphDoubleClicked += lazy => tabManager.OpenGraph(lazy);

            // Runtime panel (hidden until Play is pressed)
            runtimePanel = new RuntimePanel();
            runtimePanel.style.width = 250;
            runtimePanel.style.display = DisplayStyle.None;
            split.Add(runtimePanel);

            // Ctrl+S saves the active tab & project
            split.RegisterCallback<KeyDownEvent>(e =>
            {
                if ((e.ctrlKey || e.commandKey) && e.keyCode == KeyCode.S)
                {
                    tabManager.SaveActiveTab();
                    SaveCurrent();
                    e.StopPropagation();
                }
            });
        }

        // ───────── Runtime start/stop ─────────

        private void StartRuntime()
        {
            if (runtimeEngine != null && runtimeEngine.State != RuntimeState.Idle && runtimeEngine.State != RuntimeState.Done)
                return;

            GraphTab targetTab = tabManager?.ActiveTab;

            if (targetTab == null)
            {
                var firstLazy = session.Project?.Graphs.Items.FirstOrDefault();
                if (firstLazy != null)
                    targetTab = tabManager.OpenGraph(firstLazy);
            }

            if (targetTab == null)
            {
                Debug.LogWarning("[Bootstrap] No graph is open. Double‑click a graph to open it, then press Run.");
                return;
            }

            string graphId = targetTab.Lazy.Id;   // ← use the stable ID from Lazy

            if (string.IsNullOrEmpty(graphId))
            {
                Debug.LogError("[Bootstrap] Active graph has no Id. Try re‑opening it.");
                return;
            }

            session.IsPlayMode = true;

            // Create runtime services (use your actual class names)
            var varService = new RuntimeVariableStore(session.Project);
            var entityService = new RuntimeEntityStore(session.Project);
            var graphLoader = new ProjectGraphLoader(session.Project);
            // Inside StartRuntime, replace the null scripting backend:
            var luaBackend = new LuaScriptingBackend(varService, entityService);
            var context = new RuntimeContext(session.Project, session.Bus, graphLoader,
                luaBackend, varService, entityService);

            var executorRegistry = Services.Get<NodeExecutorRegistry>();
            runtimeEngine = new RuntimeEngine(context, executorRegistry);

            runtimePanel.Bind(runtimeEngine, session.Bus);
            runtimePanel.style.display = DisplayStyle.Flex;

            runtimeEngine.Start(graphId);

            runtimeStateSub = session.Bus.Subscribe<RuntimeStateChanged>(e =>
            {
                bool running = e.NewState == RuntimeState.Running || e.NewState == RuntimeState.Paused;
                btnPlayGraph?.SetEnabled(!running);
                btnStopRuntime?.SetEnabled(running);
                if (runIndicator != null) runIndicator.text = running ? "● Running" : "";
                if (e.NewState == RuntimeState.Idle || e.NewState == RuntimeState.Done)
                {
                    if (runtimePanel != null) runtimePanel.style.display = DisplayStyle.None;
                    // Don't change button states here – StopRuntime already did
                }
            });
        }

        private void StopRuntime()
        {
            session.IsPlayMode = false;
            runtimeEngine?.Stop();
            runtimePanel?.Unbind();
            if (runtimePanel != null) runtimePanel.style.display = DisplayStyle.None;
            runtimeStateSub?.Dispose();
            runtimeStateSub = null;

            btnPlayGraph?.SetEnabled(true);
            btnStopRuntime?.SetEnabled(false);
            if (runIndicator != null) runIndicator.text = "";
        }

        // ───────── Save ─────────

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

            if (currentEntry != null && project.Graphs.Items.Count > 0)
            {
                int totalNodes = project.Graphs.Items.Sum(g => g.CachedNodeCount);
                int totalEdges = project.Graphs.Items.Sum(g => g.CachedEdgeCount);
                currentEntry.GraphCount = project.Graphs.Items.Count;
                currentEntry.NodeCount = totalNodes;
                LibrarySerializer.Save(library);
            }
        }

        // ───────── Helpers ─────────

        private void ClearRoot()
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                root.RemoveAt(i);
        }

        private static ProjectModel BuildBlankProject(NewProjectResult result)
        {
            var id = "proj_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var project = new ProjectModel { Id = id, Name = result.ProjectName };
            foreach (var l in result.Locales)
                if (l != "en-US" && !project.Locales.Contains(l))
                    project.Locales.Add(l);

            var graph = new GraphData("graph_main", "Main");
            graph.Nodes.Add(new StartNodeData("n_start", new Vector2(120, 140)));

            var lazy = new LazyGraph { Id = graph.Id, Name = graph.Name };
            lazy.Update(graph);
            project.Graphs.Items.Add(lazy);
            return project;
        }
    }
}