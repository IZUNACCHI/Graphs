using NarrativeTool.Data.Project;
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
        public Func<object, string> GetItemSearchText;  // used for filter

        // ── Rendering ──
        public Func<object, VisualElement> BuildItemHeader;
        public Func<object, VisualElement> BuildItemDetail; // null = no expansion

        // ── Callbacks ──
        public Action<object> OnItemClicked;
        public Action<object, Vector2> OnItemContextMenu;
        public Action<string, Vector2> OnFolderContextMenu;
        public Action<string, Vector2> OnEmptyContextMenu; // arg = parent folder path
        public Action<object> OnItemDoubleClicked; //double-click to do something different from single-click
        /// <summary>Fires when the user confirms an inline rename. Parameters: (item, newName).</summary>
        public Action<object, string> OnItemRenameCommit;

        public string SelectedItemId { get; set; }
        /// <summary>Returns the human‑readable name of an item (used as rename initial value). If not set, falls back to GetItemSearchText.</summary>
        public Func<object, string> GetItemName;

        /// <summary>When set, the item with this id shows an inline text field for renaming.</summary>
        public string RenamingItemId { get; set; }
        public string Filter { get; set; } = "";

        private object lastClickedItem;
        private float lastClickTime;

        // Display label for the always-present root folder (FolderPath = "").
        // Root is rendered as a regular folder header but its rename/delete
        // options are filtered out by the context-menu provider.

        // is deprecated because the root folder is now always rendered with the same label and no header (since it can't be renamed or interacted with like normal folders). Left in place in case we want to re-enable a visible root header in the future.
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

        // Drag and drop 
        private bool allowDragDrop;
        public bool AllowDragDrop
        {
            get => allowDragDrop;
            set
            {
                allowDragDrop = value;
                if (value) EnableDragDrop();
            }
        }
        public Action<object, string> OnItemMoved;
        public Action<string, string> OnFolderMoved;

        // Internal drag state
        private VisualElement dragGhost;
        private bool isDragging;
        private object draggedItem;
        private string draggedFolderPath;
        private Vector2 dragStartPosition;
        private VisualElement draggedRow;
        private int dragPointerId;

        public bool ShowSearchBar
        {
            get => showSearchBar;
            set
            {
                showSearchBar = value;
                if (value && searchField == null)
                    CreateSearchBar();
                else if (!value && searchField != null)
                    RemoveSearchBar();
            }
        }

        private bool showSearchBar;

        private TextField searchField;

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


        private void EnableDragDrop()
        {
            RegisterCallback<PointerDownEvent>(OnDragPointerDown, TrickleDown.TrickleDown);
            RegisterCallback<PointerMoveEvent>(OnDragPointerMove);
            RegisterCallback<PointerUpEvent>(OnDragPointerUp);
        }

        private void OnDragPointerDown(PointerDownEvent e)
        {
            if (!AllowDragDrop || e.button != 0) return;

            var target = e.target as VisualElement;
            var row = target?.GetFirstAncestorWithClass("nt-tree-item") ??
                      target?.GetFirstAncestorWithClass("nt-tree-folder");
            if (row == null) return;

            if (row.ClassListContains("nt-tree-item") && row.userData != null)
            {
                draggedItem = row.userData;
                draggedFolderPath = null;
            }
            else if (row.ClassListContains("nt-tree-folder") && row.userData is string f)
            {
                draggedFolderPath = f;
                draggedItem = null;
            }
            else return;

            // Store the row and pointer id for later
            draggedRow = row;
            dragPointerId = e.pointerId;
            dragStartPosition = e.position;
            isDragging = false;
            // Do NOT stop propagation – we want clicks to work normally
        }

        private void OnDragPointerMove(PointerMoveEvent e)
        {
            if (draggedRow == null) return;

            if (!isDragging)
            {
                // Start dragging only after moving a few pixels
                if (Mathf.Abs(e.position.x - dragStartPosition.x) > 3 ||
                    Mathf.Abs(e.position.y - dragStartPosition.y) > 3)
                {
                    isDragging = true;
                    this.CapturePointer(dragPointerId);          // capture the pointer
                    CreateGhost(draggedRow);               // create visual ghost
                    dragGhost.style.visibility = Visibility.Visible;
                    e.StopPropagation();                    // now we own the event
                }
                else
                {
                    return;   // not enough movement, let normal events proceed
                }
            }

            // Move ghost with the pointer
            if (dragGhost != null)
            {
                dragGhost.style.left = e.localPosition.x + 8;
                dragGhost.style.top = e.localPosition.y - 10;
            }
            e.StopPropagation();
        }

        private void OnDragPointerUp(PointerUpEvent e)
        {
            if (!isDragging || dragGhost == null)
            {
                CleanupDrag();
                return;
            }

            // Release the captured pointer
            this.ReleasePointer(dragPointerId);

            // Determine drop target
            string targetFolder = "";
            var pickTarget = panel?.Pick(e.position);
            var folderRow = pickTarget?.GetFirstAncestorWithClass("nt-tree-folder");
            if (folderRow != null && folderRow.userData is string path)
                targetFolder = path;

            if (draggedItem != null)
            {
                string oldFolder = GetItemFolder?.Invoke(draggedItem) ?? "";
                if (targetFolder != oldFolder)
                    OnItemMoved?.Invoke(draggedItem, targetFolder);
            }
            else if (draggedFolderPath != null)
            {
                if (targetFolder != draggedFolderPath && !targetFolder.StartsWith(draggedFolderPath + "/"))
                    OnFolderMoved?.Invoke(draggedFolderPath, targetFolder);
            }

            CleanupDrag();
            e.StopPropagation();
        }

        private void CreateGhost(VisualElement source)
        {
            dragGhost = new VisualElement();
            dragGhost.AddToClassList("nt-tree-drag-ghost");
            dragGhost.style.position = Position.Absolute;
            dragGhost.style.opacity = 0.7f;
            dragGhost.pickingMode = PickingMode.Ignore;   // so it doesn't interfere with hit tests

            string name = draggedItem != null
                ? (GetItemSearchText?.Invoke(draggedItem) ?? GetItemId?.Invoke(draggedItem) ?? "Item")
                : draggedFolderPath.Split('/').Last();

            var label = new Label(name);
            label.AddToClassList("nt-tree-drag-label");
            dragGhost.Add(label);
            Add(dragGhost);
        }

        private void CleanupDrag()
        {
            if (dragGhost != null)
            {
                dragGhost.RemoveFromHierarchy();
                dragGhost = null;
            }
            isDragging = false;
            draggedItem = null;
            draggedFolderPath = null;
            draggedRow = null;
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

        private void CreateSearchBar()
        {
            searchField = new TextField();
            searchField.AddToClassList("nt-tree-search");
            searchField.RegisterValueChangedCallback(evt =>
            {
                Filter = evt.newValue;
                Rebuild();
            });
            // Insert at the beginning of the scroll container (before listContainer)
            Insert(0, searchField);
        }

        private void RemoveSearchBar()
        {
            searchField?.RemoveFromHierarchy();
            searchField = null;
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

            bool isRoot = string.IsNullOrEmpty(node.Path);

            // Skip filtering logic for root: it's always rendered (no header).
            if (!isRoot && !FolderHasMatch(node, byFolder))
                return;

            // If NOT root, render the folder header (folder name + caret).
            // If root, just skip the header and directly render its contents.
            if (!isRoot)
            {
                listContainer.Add(BuildFolderHeader(node.Path, node.DisplayName, node.Depth));
                if (IsFolderCollapsed(node.Path))
                    return;  // children are hidden
            }

            // Items at this level
            if (byFolder.TryGetValue(node.Path, out var items))
            {
                foreach (var it in items)
                    listContainer.Add(BuildItemRow(it, node.Depth + (isRoot ? 0 : 1)));
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

            // Check if this item is being renamed inline
            if (RenamingItemId == id)
            {
                string currentName = GetItemName?.Invoke(item) ?? GetItemSearchText?.Invoke(item) ?? "";
                var renameField = new TextField { value = currentName };
                renameField.AddToClassList("nt-tree-item-rename");
                header.Add(renameField);
                container.Add(header);

                renameField.schedule.Execute(() =>
                {
                    renameField.Focus();
                    renameField.SelectAll();
                }).StartingIn(0);

                Action commit = () =>
                {
                    if (RenamingItemId != id) return;
                    RenamingItemId = null;
                    string newName = (renameField.value ?? "").Trim();
                    if (!string.IsNullOrEmpty(newName) && newName != currentName)
                        OnItemRenameCommit?.Invoke(item, newName);
                    Rebuild();
                };

                renameField.RegisterCallback<BlurEvent>(_ => commit());
                renameField.RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    {
                        commit();
                        e.StopPropagation();
                    }
                    else if (e.keyCode == KeyCode.Escape)
                    {
                        RenamingItemId = null;
                        Rebuild();
                        e.StopPropagation();
                    }
                });
            }
            else
            {
                // Normal header with label, swatch, etc.
                var inner = BuildItemHeader?.Invoke(item);
                if (inner != null) header.Add(inner);
                container.Add(header);

                // Click / double‑click / context menu
                header.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button == 0)
                    {
                        float now = Time.unscaledTime;
                        bool isDoubleClick = lastClickTime > 0f && now - lastClickTime < 0.35f
                                             && ReferenceEquals(lastClickedItem, item);

                        lastClickTime = now;
                        lastClickedItem = item;

                        if (isDoubleClick)
                        {
                            OnItemDoubleClicked?.Invoke(item);
                            lastClickTime = -1f;
                        }
                        else
                        {
                            OnItemClicked?.Invoke(item);
                        }

                        e.StopPropagation();
                    }
                    else if (e.button == 1)
                    {
                        OnItemContextMenu?.Invoke(item, e.position);
                        e.StopPropagation();
                    }
                });
            }

            // Inline detail (only built for the currently-selected row)
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

    public static class VisualElementExtensions
    {
        public static VisualElement GetFirstAncestorWithClass(this VisualElement element, string className)
        {
            var ve = element;
            while (ve != null)
            {
                if (ve.ClassListContains(className)) return ve;
                ve = ve.parent;
            }
            return null;
        }
    }
}
