// UI/Graphs/GraphsPanel.cs
using NarrativeTool.Core.Commands;
using NarrativeTool.Core.Commands.Generic;
using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using NarrativeTool.Data.Project;
using NarrativeTool.UI.FolderTree;
using NarrativeTool.UI.Widgets;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Graphs
{
    public sealed class GraphsPanel : VisualElement
    {
        private ProjectModel project;
        private SessionState session;
        private ContextMenuController contextMenu;
        private EventBus bus;

        private TextField filterField;
        private FolderTreeView tree;
        private string selectedGraphId;
        private string focusNameForGraphId;

        private readonly System.Collections.Generic.List<IDisposable> subs = new();

        public event Action<LazyGraph> OnGraphDoubleClicked;

        public int GraphCount => project?.Graphs.Items.Count ?? 0;

        public GraphsPanel()
        {
            AddToClassList("nt-graphs");
            focusable = true;

            // Filter row
            var filterRow = new VisualElement();
            filterRow.AddToClassList("nt-vars-filter-row");

            filterField = new TextField { value = "" };
            filterField.AddToClassList("nt-vars-filter");
            filterField.RegisterValueChangedCallback(evt =>
            {
                if (tree != null)
                {
                    tree.Filter = evt.newValue ?? "";
                    tree.Rebuild();
                }
            });
            filterRow.Add(filterField);

            var addBtn = new Button(() => AddGraph("")) { text = "+ New" };
            addBtn.AddToClassList("nt-vars-add-btn");
            filterRow.Add(addBtn);

            var addFolderBtn = new Button(() => AddFolder("")) { text = "📁" };
            addFolderBtn.AddToClassList("nt-vars-add-btn");
            addFolderBtn.tooltip = "New folder";
            filterRow.Add(addFolderBtn);

            Add(filterRow);

            // Tree (bare, no data callbacks yet)
            tree = new FolderTreeView
            {
                ShowSearchBar = false,
                RootDisplayName = null,
                BuildItemHeader = item => BuildGraphHeader((LazyGraph)item),
                BuildItemDetail = null,
            };
            Add(tree);

            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        public void Bind(ProjectModel project, SessionState session, ContextMenuController contextMenu)
        {
            Unbind();

            this.project = project;
            this.session = session;
            this.contextMenu = contextMenu;
            this.bus = session.Bus;

            // Data accessors
            tree.GetFolders = () => project.Graphs.Folders;
            tree.GetItems = () => project.Graphs.Items.Cast<object>();
            tree.GetItemFolder = item => ((LazyGraph)item).FolderPath ?? "";
            tree.GetItemId = item => ((LazyGraph)item).Id;
            tree.GetItemSearchText = item => ((LazyGraph)item).Name;
            tree.GetItemName = item => ((LazyGraph)item).Name;

            // Click / double‑click / context menu
            tree.OnItemClicked = item => SelectGraph(((LazyGraph)item).Id);
            tree.OnItemDoubleClicked = item => OnGraphDoubleClicked?.Invoke((LazyGraph)item);

            tree.OnItemContextMenu = (item, pos) =>
            {
                var g = (LazyGraph)item;
                SelectGraph(g.Id);
                contextMenu?.Open(new GraphContextTarget(this, g), pos);
            };
            tree.OnFolderContextMenu = (folder, pos) =>
                contextMenu?.Open(new GraphFolderContextTarget(this, folder), pos);
            tree.OnEmptyContextMenu = (parent, pos) =>
                contextMenu?.Open(new GraphFolderContextTarget(this, parent), pos);

            // Folder rename
            tree.OnFolderRenameCommit = (oldPath, newPath) =>
            {
                if (project.Graphs.Folders.Contains(newPath))
                {
                    Debug.LogWarning($"[Graphs] Folder '{newPath}' already exists.");
                    tree.Rebuild();
                    return;
                }
                Commands.Execute(new RenameFolderCmd<LazyGraph>(
                    "Graph",
                    project.Graphs,
                    oldPath, newPath,
                    onRename: (o, n) => bus.Publish(new GraphFolderRenamedEvent(project.Id, o, n)),
                    onItemPathChanged: (item, oldItemPath, newItemPath) => { }
                ));
            };

            tree.OnItemRenameCommit = (item, newName) =>
            {
                var g = (LazyGraph)item;
                if (newName == g.Name) return;
                Commands.Execute(new RenameItemCmd<LazyGraph>(
                    "Graph", g, g.Name, newName,
                    doPublish: () => bus.Publish(new GraphRenamedEvent(project.Id, g.Id, g.Name, newName)),
                    undoPublish: () => bus.Publish(new GraphRenamedEvent(project.Id, g.Id, newName, g.Name))
                ));
            };

            // Drag‑and‑drop
            tree.AllowDragDrop = true;

            tree.OnItemMoved = (item, newFolder) =>
            {
                var g = (LazyGraph)item;
                if (g.FolderPath == newFolder) return;
                Commands.Execute(new MoveItemCmd<LazyGraph>(
                    "Graph", g, project.Graphs,
                    g.FolderPath, newFolder,
                    doPublish: () => { }, undoPublish: () => { }
                ));
                Rebuild();
            };

            tree.OnFolderMoved = (folderPath, newParent) =>
            {
                string folderName = folderPath.Split('/').Last();
                string newPath = string.IsNullOrEmpty(newParent) ? folderName : newParent + "/" + folderName;
                Commands.Execute(new RenameFolderCmd<LazyGraph>(
                    "Graph", project.Graphs,
                    folderPath, newPath,
                    onRename: (o, n) => { },
                    onItemPathChanged: (item, oldItemPath, newItemPath) => { }
                ));
                Rebuild();
            };

            // Event subscriptions
            subs.Add(bus.Subscribe<GraphAddedEvent>(_ => Rebuild()));
            subs.Add(bus.Subscribe<GraphRemovedEvent>(e =>
            {
                if (selectedGraphId == e.GraphId) selectedGraphId = null;
                Rebuild();
            }));
            subs.Add(bus.Subscribe<GraphRenamedEvent>(_ => Rebuild()));
            subs.Add(bus.Subscribe<GraphFolderAddedEvent>(_ => Rebuild()));
            subs.Add(bus.Subscribe<GraphFolderRemovedEvent>(_ => Rebuild()));
            subs.Add(bus.Subscribe<GraphFolderRenamedEvent>(_ => Rebuild()));

            Rebuild();
        }

        public void Unbind()
        {
            foreach (var s in subs) s?.Dispose();
            subs.Clear();

            // Remove any open modals
            foreach (var child in Children().OfType<ModalConfirm>().ToList())
                child.RemoveFromHierarchy();

            // Clear tree bindings
            if (tree != null)
            {
                tree.GetFolders = null;
                tree.GetItems = null;
                tree.GetItemFolder = null;
                tree.GetItemId = null;
                tree.GetItemSearchText = null;
                tree.GetItemName = null;
                tree.OnItemClicked = null;
                tree.OnItemDoubleClicked = null;
                tree.OnItemContextMenu = null;
                tree.OnFolderContextMenu = null;
                tree.OnEmptyContextMenu = null;
                tree.OnFolderRenameCommit = null;
                tree.OnItemRenameCommit = null;
                tree.OnItemMoved = null;
                tree.OnFolderMoved = null;
                tree.AllowDragDrop = false;
                tree.Rebuild();
            }
        }

        private CommandSystem Commands => session.ProjectCommands;

        private void Rebuild()
        {
            tree.SelectedItemId = selectedGraphId;
            tree.Rebuild();
        }

        public void Refresh() => Rebuild();

        // ── Graph CRUD ──

        public void AddGraph(string folderPath)
        {
            string baseName = "NewGraph";
            string name = baseName;
            int n = 1;
            while (project.Graphs.Items.Any(g => g.FolderPath == folderPath && g.Name == name))
                name = $"{baseName} {++n}";

            var graphData = new GraphData("graph_" + Guid.NewGuid().ToString("N").Substring(0, 8), name);
            graphData.Nodes.Add(new StartNodeData("n_start", new Vector2(120, 140)));

            var lazy = new LazyGraph
            {
                Id = graphData.Id,
                Name = graphData.Name,
                FolderPath = folderPath
            };
            lazy.Update(graphData);

            Commands.Execute(new AddItemCmd<LazyGraph>(
                "Graph",
                project.Graphs,
                lazy,
                doPublish: () => bus.Publish(new GraphAddedEvent(project.Id, lazy.Id)),
                undoPublish: () => bus.Publish(new GraphRemovedEvent(project.Id, lazy.Id))
            ));
            selectedGraphId = lazy.Id;
            Rebuild();
        }

        public void RemoveGraph(string graphId)
        {
            if (project.Graphs.Items.Count <= 1)
            {
                Debug.LogWarning("[Graphs] Cannot delete the last graph.");
                return;
            }
            var graph = project.Graphs.Items.FirstOrDefault(g => g.Id == graphId);
            if (graph == null) return;

            var msg = $"Delete graph \"{graph.Name}\"?";
            Add(new ModalConfirm(msg, "Delete", () =>
            {
                Commands.Execute(new RemoveItemCmd<LazyGraph>(
                    "Graph",
                    project.Graphs,
                    graphId,
                    doPublish: () => bus.Publish(new GraphRemovedEvent(project.Id, graphId)),
                    undoPublish: () => bus.Publish(new GraphAddedEvent(project.Id, graphId))
                ));
                if (selectedGraphId == graphId) selectedGraphId = null;
                Rebuild();
            }));
        }

        public void BeginRenameGraph(string graphId)
        {
            selectedGraphId = graphId;
            tree.RenamingItemId = graphId;
            Rebuild();
        }

        // ── Folder CRUD ──

        public void AddFolder(string parent)
        {
            string parentPrefix = string.IsNullOrEmpty(parent) ? "" : parent + "/";
            string baseName = "NewFolder";
            string name = baseName;
            int n = 1;
            while (project.Graphs.Folders.Contains(parentPrefix + name))
                name = $"{baseName}{++n}";
            string fullPath = parentPrefix + name;

            Commands.Execute(new AddFolderCmd<LazyGraph>(
                "Graph",
                project.Graphs,
                fullPath,
                doPublish: () => bus.Publish(new GraphFolderAddedEvent(project.Id, fullPath)),
                undoPublish: () => bus.Publish(new GraphFolderRemovedEvent(project.Id, fullPath))
            ));
            if (!string.IsNullOrEmpty(parent)) tree.SetFolderCollapsed(parent, false);
            Rebuild();
        }

        public void RemoveFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;

            var msg = $"Delete folder \"{folderPath}\" and all its contents?";
            Add(new ModalConfirm(msg, "Delete", () =>
            {
                Commands.Execute(new RemoveFolderCmd<LazyGraph>(
                    "Graph",
                    project.Graphs,
                    folderPath,
                    onItemRemoved: g => bus.Publish(new GraphRemovedEvent(project.Id, g.Id)),
                    onFolderRemoved: f => bus.Publish(new GraphFolderRemovedEvent(project.Id, f)),
                    onItemRestored: g => bus.Publish(new GraphAddedEvent(project.Id, g.Id)),
                    onFolderRestored: f => bus.Publish(new GraphFolderAddedEvent(project.Id, f))
                ));
                Rebuild();
            }));
        }

        public void BeginRenameFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            tree.RenamingFolderPath = folderPath;
            tree.SetFolderCollapsed(folderPath, false);
            tree.Rebuild();
        }

        // ── Selection / helpers ──

        private void SelectGraph(string id)
        {
            selectedGraphId = id;
            Rebuild();
        }

        private static VisualElement BuildGraphHeader(LazyGraph graph)
        {
            var row = new VisualElement();
            row.AddToClassList("nt-vars-row");

            var swatch = new VisualElement();
            swatch.AddToClassList("nt-vars-swatch");
            swatch.AddToClassList("nt-vars-swatch--flow");
            row.Add(swatch);

            var name = new Label(graph.Name);
            name.AddToClassList("nt-vars-name");
            row.Add(name);

            return row;
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            if (Commands == null) return;
            bool ctrl = e.ctrlKey || e.commandKey;
            if (ctrl && e.keyCode == KeyCode.Z)
            {
                if (e.shiftKey) Commands.Redo();
                else Commands.Undo();
                e.StopPropagation();
            }
            else if (e.keyCode == KeyCode.Delete && !string.IsNullOrEmpty(selectedGraphId))
            {
                RemoveGraph(selectedGraphId);   // uses the existing method that shows modal and guards last graph
                e.StopPropagation();
            }
        }
    }

    // Context menu targets
    public sealed class GraphContextTarget
    {
        public GraphsPanel Panel { get; }
        public LazyGraph Graph { get; }
        public GraphContextTarget(GraphsPanel panel, LazyGraph graph)
        { Panel = panel; Graph = graph; }
    }

    public sealed class GraphFolderContextTarget
    {
        public GraphsPanel Panel { get; }
        public string FolderPath { get; }
        public GraphFolderContextTarget(GraphsPanel panel, string folderPath)
        { Panel = panel; FolderPath = folderPath ?? ""; }
    }
}