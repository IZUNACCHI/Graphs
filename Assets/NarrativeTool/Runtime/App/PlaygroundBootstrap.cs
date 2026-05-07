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
using NarrativeTool.UI.Debugger;
using NarrativeTool.UI.Library;
using NarrativeTool.UI.Runtime;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
// PlaygroundBootstrap is now a thin shell:
//   • Awake/Start: register services, load theme, register context-menu providers
//   • ShowLibrary / ShowWizard: project chooser
//   • OpenEditor: hand off to MainWindow, hold the runtime engine on its behalf
// All editor layout (menu bar, toolbar, sidebar, canvas, runtime panels) now
// lives in NarrativeTool.UI.MainWindow.

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
        private MainWindow mainWindow;
        private GraphCenterController centerController;  // alias of mainWindow.CenterController
        private RuntimePanel runtimePanel;                // alias of mainWindow.RuntimePanel
        private DebuggerPanel debuggerPanel;              // alias of mainWindow.DebuggerPanel

        // ── Runtime ──
        private RuntimeEngine runtimeEngine;
        private BreakpointStore breakpointStore;
        private IDisposable runtimeStateSub;

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

            // Single BreakpointStore lives across runs so the user's set is preserved.
            breakpointStore = new BreakpointStore(bus);
            Services.Register(breakpointStore);

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
            contextMenu.RegisterProvider(new WatchContextMenuProvider());
            contextMenu.RegisterProvider(new WatchPickerContextMenuProvider());
            contextMenu.RegisterProvider(new BreakpointContextMenuProvider());

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
            mainWindow?.Teardown();
            mainWindow = null;
            centerController = null;
            runtimePanel = null;
            debuggerPanel = null;
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

            mainWindow = new MainWindow(project, session, contextMenu, new MainWindowCallbacks
            {
                OnBackToLibrary = ShowLibrary,
                OnSave          = () => { mainWindow?.CenterController?.SaveActiveTab(); SaveCurrent(); },
                OnSaveAll       = SaveAll,
                OnPlayProject   = () => StartRuntime(),  // TODO: distinguish project-vs-graph entry point
                OnPlayGraph     = () => StartRuntime(),
                OnStop          = StopRuntime,
                IsRunning       = () => runtimeEngine != null
                                        && (runtimeEngine.State == RuntimeState.Running
                                            || runtimeEngine.State == RuntimeState.Paused),
                ProjectTitle    = () => session.Project?.Name,
            });
            root.Add(mainWindow);

            // Aliases keep StartRuntime/StopRuntime/etc. unchanged.
            centerController = mainWindow.CenterController;
            runtimePanel     = mainWindow.RuntimePanel;
            debuggerPanel    = mainWindow.DebuggerPanel;
        }

        private void SaveAll()
        {
            mainWindow?.CenterController?.SaveAllTabs();
            SaveCurrent();
        }

        // ───────── Runtime start/stop ─────────

        private void StartRuntime()
        {
            if (runtimeEngine != null && runtimeEngine.State != RuntimeState.Idle && runtimeEngine.State != RuntimeState.Done)
                return;

            GraphPanel targetPanel = centerController?.ActivePanel;

            if (targetPanel == null)
            {
                var firstLazy = session.Project?.Graphs.Items.FirstOrDefault();
                if (firstLazy != null)
                    targetPanel = centerController?.OpenGraph(firstLazy);
            }

            if (targetPanel == null)
            {
                Debug.LogWarning("[Bootstrap] No graph is open. Double‑click a graph to open it, then press Run.");
                return;
            }

            string graphId = targetPanel.Lazy.Id;   // ← use the stable ID from Lazy

            if (string.IsNullOrEmpty(graphId))
            {
                Debug.LogError("[Bootstrap] Active graph has no Id. Try re‑opening it.");
                return;
            }

            session.IsPlayMode = true;

            // Create runtime services 
            var varService = new RuntimeVariableStore(session.Project, session.Bus);
            var entityService = new RuntimeEntityStore(session.Project, session.Bus);
            var graphLoader = new ProjectGraphLoader(session.Project);
            // Inside StartRuntime, replace the null scripting backend:
            var luaBackend = new LuaScriptingBackend(varService, entityService);
            var context = new RuntimeContext(session.Project, session.Bus, graphLoader,
                luaBackend, varService, entityService);

            var executorRegistry = Services.Get<NodeExecutorRegistry>();
            runtimeEngine = new RuntimeEngine(context, executorRegistry, breakpointStore);

            runtimePanel.Bind(runtimeEngine, session.Bus);
            runtimePanel.style.display = DisplayStyle.Flex;

            debuggerPanel.Bind(session.Project, varService, entityService,
                breakpointStore, session.Bus, contextMenu);
            debuggerPanel.style.display = DisplayStyle.Flex;

            runtimeEngine.Start(graphId);

            runtimeStateSub = session.Bus.Subscribe<RuntimeStateChanged>(e =>
            {
                if (e.NewState == RuntimeState.Idle || e.NewState == RuntimeState.Done)
                {
                    if (runtimePanel != null) runtimePanel.style.display = DisplayStyle.None;
                    if (debuggerPanel != null) debuggerPanel.style.display = DisplayStyle.None;
                }
                // Toolbar visibility predicates read IsRunning(); refresh them.
                mainWindow?.NotifyRuntimeStateChanged();
            });

            mainWindow?.NotifyRuntimeStateChanged();
        }

        private void StopRuntime()
        {
            session.IsPlayMode = false;
            runtimeEngine?.Stop();
            runtimePanel?.Unbind();
            if (runtimePanel != null) runtimePanel.style.display = DisplayStyle.None;
            debuggerPanel?.Unbind();
            if (debuggerPanel != null) debuggerPanel.style.display = DisplayStyle.None;
            runtimeStateSub?.Dispose();
            runtimeStateSub = null;

            mainWindow?.NotifyRuntimeStateChanged();
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