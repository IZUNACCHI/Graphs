using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.FolderTree
{
    /// <summary>
    /// Reusable tree view that groups items by a folder-path string.
    ///
    /// Folders are first-class (passed in via <see cref="GetFolders"/>) so
    /// empty folders persist. Items are placed under their folder via
    /// <see cref="GetItemFolder"/>. Selection lights up a row and reveals an
    /// inline detail block built by <see cref="BuildItemDetail"/> below the
    /// row header — no separate editor pane.
    ///
    /// The tree owns no domain data; it is wired up via lambdas. The
    /// VariablesPanel is the first user; a future GraphsPanel can reuse it
    /// against a parallel folder/item store.
    /// </summary>
    public sealed class FolderTreeView : VisualElement
    {
        // ── Data accessors ──
        public Func<IReadOnlyList<string>> GetFolders;
        public Func<IEnumerable<object>> GetItems;
        public Func<object, string> GetItemFolder;
        public Func<object, string> GetItemId;
        public Func<object, string> GetItemSearchText;  // optional; used for filter

        // ── Rendering ──
        public Func<object, VisualElement> BuildItemHeader;
        public Func<object, VisualElement> BuildItemDetail; // null = no expansion

        // ── Callbacks ──
        public Action<object> OnItemClicked;
        public Action<object, Vector2> OnItemContextMenu;
        public Action<string, Vector2> OnFolderContextMenu;
        public Action<string, Vector2> OnEmptyContextMenu; // arg = parent folder path

        // ── External state ──
        public string SelectedItemId { get; set; }
        public string Filter { get; set; } = "";

        // Display label for the always-present root folder (FolderPath = "").
        // Root is rendered as a regular folder header but its rename/delete
        // options are filtered out by the context-menu provider.
        public string RootDisplayName { get; set; } = "Root";

        // ── Inline folder rename ──
        // Set RenamingFolderPath and call Rebuild() to swap that folder's
        // label for an inline TextField; the panel handles the resulting
        // string via OnFolderRenameCommit.
        public string RenamingFolderPath { get; set; }
        public Action<string, string> OnFolderRenameCommit;  // (oldPath, newPath)

        // ── Internal ──
        private readonly HashSet<string> collapsed = new();
        private readonly VisualElement listContainer;

        public FolderTreeView()
        {
            AddToClassList("nt-tree");

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("nt-tree-scroll");
            listContainer = scroll.contentContainer;
            listContainer.AddToClassList("nt-tree-list");
            Add(scroll);

            // Right-click in the empty bottom area = "add at root".
            listContainer.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 1) return;
                if (e.target != listContainer) return;
                OnEmptyContextMenu?.Invoke("", e.position);
                e.StopPropagation();
            });
        }

        public bool IsFolderCollapsed(string path) => collapsed.Contains(path);
        public void SetFolderCollapsed(string path, bool isCollapsed)
        {
            if (isCollapsed) collapsed.Add(path);
            else collapsed.Remove(path);
        }

        // Internal node used to build the hierarchical render tree.
        private sealed class FolderNode
        {
            public string Path;
            public string DisplayName;
            public int Depth;
            public List<FolderNode> Children = new();
        }

        public void Rebuild()
        {
            listContainer.Clear();

            var folders = GetFolders?.Invoke() ?? Array.Empty<string>();
            var items = GetItems?.Invoke()?.ToList() ?? new List<object>();

            // Group items by folder path.
            var byFolder = new Dictionary<string, List<object>>();
            foreach (var item in items)
            {
                if (!MatchesFilter(item)) continue;
                var f = GetItemFolder?.Invoke(item) ?? "";
                if (!byFolder.TryGetValue(f, out var list))
                    byFolder[f] = list = new List<object>();
                list.Add(item);
            }

            // Build a folder tree from "/"-delimited paths, then render
            // recursively from the implicit root (path = "").
            var root = BuildFolderTree(folders);
            RenderFolderNode(root, byFolder);
        }

        private FolderNode BuildFolderTree(IReadOnlyList<string> paths)
        {
            var root = new FolderNode { Path = "", DisplayName = RootDisplayName, Depth = 0 };
            foreach (var p in paths.OrderBy(s => s))
            {
                if (string.IsNullOrEmpty(p)) continue;
                var segments = p.Split('/');
                var current = root;
                string sofar = "";
                for (int i = 0; i < segments.Length; i++)
                {
                    sofar = i == 0 ? segments[i] : sofar + "/" + segments[i];
                    var existing = current.Children.FirstOrDefault(c => c.Path == sofar);
                    if (existing == null)
                    {
                        existing = new FolderNode
                        {
                            Path = sofar,
                            DisplayName = segments[i],
                            Depth = current.Depth + 1,
                        };
                        current.Children.Add(existing);
                    }
                    current = existing;
                }
            }
            return root;
        }

        private void RenderFolderNode(FolderNode node, Dictionary<string, List<object>> byFolder)
        {
            // Skip rendering folders that don't match filter and have no
            // matching descendants. (Root is always rendered.)
            if (!string.IsNullOrEmpty(node.Path) && !FolderHasMatch(node, byFolder))
                return;

            listContainer.Add(BuildFolderHeader(node.Path, node.DisplayName, node.Depth));
            if (IsFolderCollapsed(node.Path)) return;

            // Items at this level
            if (byFolder.TryGetValue(node.Path, out var items))
            {
                foreach (var it in items) listContainer.Add(BuildItemRow(it, node.Depth + 1));
            }

            // Child folders alphabetically
            foreach (var child in node.Children.OrderBy(c => c.DisplayName))
                RenderFolderNode(child, byFolder);
        }

        private bool FolderHasMatch(FolderNode node, Dictionary<string, List<object>> byFolder)
        {
            if (string.IsNullOrEmpty(Filter)) return true;
            if (node.Path.IndexOf(Filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (byFolder.TryGetValue(node.Path, out var items) && items.Count > 0) return true;
            foreach (var c in node.Children) if (FolderHasMatch(c, byFolder)) return true;
            return false;
        }

        private bool MatchesFilter(object item)
        {
            if (string.IsNullOrEmpty(Filter)) return true;
            string text = GetItemSearchText?.Invoke(item) ?? "";
            return text.IndexOf(Filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private const int IndentPx = 12;

        private VisualElement BuildFolderHeader(string folderPath, string displayName, int depth)
        {
            bool isRoot = string.IsNullOrEmpty(folderPath);

            var row = new VisualElement();
            row.AddToClassList("nt-tree-folder");
            if (isRoot) row.AddToClassList("nt-tree-folder--root");
            row.style.paddingLeft = 6 + depth * IndentPx;
            row.userData = folderPath;

            bool isCollapsed = IsFolderCollapsed(folderPath);
            var caret = new Label(isCollapsed ? "▶" : "▼");
            caret.AddToClassList("nt-tree-folder-caret");
            row.Add(caret);

            // Rename UI doesn't apply to the root — it's undeletable / unrenameable.
            if (!isRoot && RenamingFolderPath == folderPath)
            {
                var input = new TextField { value = folderPath };
                input.AddToClassList("nt-tree-folder-rename");
                row.Add(input);
                input.schedule.Execute(() => { input.Focus(); input.SelectAll(); }).StartingIn(0);

                Action commit = () =>
                {
                    if (RenamingFolderPath != folderPath) return;
                    RenamingFolderPath = null;
                    var next = (input.value ?? "").Trim();
                    if (string.IsNullOrEmpty(next) || next == folderPath)
                    {
                        Rebuild();
                        return;
                    }
                    OnFolderRenameCommit?.Invoke(folderPath, next);
                };
                input.RegisterCallback<BlurEvent>(_ => commit());
                input.RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    {
                        commit();
                        e.StopPropagation();
                    }
                    else if (e.keyCode == KeyCode.Escape)
                    {
                        RenamingFolderPath = null;
                        Rebuild();
                        e.StopPropagation();
                    }
                });
                return row;
            }

            var label = new Label("📁 " + displayName);
            label.AddToClassList("nt-tree-folder-label");
            row.Add(label);

            row.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 0)
                {
                    SetFolderCollapsed(folderPath, !isCollapsed);
                    Rebuild();
                    e.StopPropagation();
                }
                else if (e.button == 1)
                {
                    OnFolderContextMenu?.Invoke(folderPath, e.position);
                    e.StopPropagation();
                }
            });
            return row;
        }

        private VisualElement BuildItemRow(object item, int depth)
        {
            string id = GetItemId(item);
            bool isSelected = id == SelectedItemId;

            var container = new VisualElement();
            container.AddToClassList("nt-tree-item");
            if (isSelected) container.AddToClassList("nt-tree-item--selected");
            container.style.paddingLeft = depth * IndentPx;
            container.userData = item;

            var header = new VisualElement();
            header.AddToClassList("nt-tree-item-header");
            var inner = BuildItemHeader?.Invoke(item);
            if (inner != null) header.Add(inner);
            container.Add(header);

            header.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 0)
                {
                    OnItemClicked?.Invoke(item);
                    e.StopPropagation();
                }
                else if (e.button == 1)
                {
                    OnItemContextMenu?.Invoke(item, e.position);
                    e.StopPropagation();
                }
            });

            // Inline detail (only built for the currently-selected row).
            if (isSelected && BuildItemDetail != null)
            {
                var detail = BuildItemDetail(item);
                if (detail != null)
                {
                    detail.AddToClassList("nt-tree-item-detail");
                    container.Add(detail);
                }
            }

            return container;
        }
    }
}
