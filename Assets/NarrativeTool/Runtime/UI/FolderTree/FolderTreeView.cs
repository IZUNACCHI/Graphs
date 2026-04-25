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

        public void Rebuild()
        {
            listContainer.Clear();

            var folders = GetFolders?.Invoke() ?? Array.Empty<string>();
            var items = GetItems?.Invoke()?.ToList() ?? new List<object>();

            // Group items by folder.
            var byFolder = new Dictionary<string, List<object>>();
            foreach (var item in items)
            {
                if (!MatchesFilter(item)) continue;
                var f = GetItemFolder?.Invoke(item) ?? "";
                if (!byFolder.TryGetValue(f, out var list))
                    byFolder[f] = list = new List<object>();
                list.Add(item);
            }

            // Always render the root as an explicit, undeletable folder
            // header (path = ""). Items at the root sit beneath it.
            listContainer.Add(BuildFolderHeader(""));
            if (!IsFolderCollapsed("") && byFolder.TryGetValue("", out var rootItems))
            {
                foreach (var it in rootItems) listContainer.Add(BuildItemRow(it));
            }

            // Other folders alphabetically. Render folder header even if it
            // has no matching items (so empty folders are visible /
            // right-clickable).
            foreach (var folder in folders.OrderBy(f => f))
            {
                if (string.IsNullOrEmpty(folder)) continue; // root already drawn
                bool folderMatches = string.IsNullOrEmpty(Filter)
                    || folder.IndexOf(Filter, StringComparison.OrdinalIgnoreCase) >= 0;
                bool hasMatchingItems = byFolder.TryGetValue(folder, out var children) && children.Count > 0;
                if (!folderMatches && !hasMatchingItems) continue;

                listContainer.Add(BuildFolderHeader(folder));
                if (!IsFolderCollapsed(folder) && hasMatchingItems)
                {
                    foreach (var it in children) listContainer.Add(BuildItemRow(it));
                }
            }
        }

        private bool MatchesFilter(object item)
        {
            if (string.IsNullOrEmpty(Filter)) return true;
            string text = GetItemSearchText?.Invoke(item) ?? "";
            return text.IndexOf(Filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private VisualElement BuildFolderHeader(string folderPath)
        {
            bool isRoot = string.IsNullOrEmpty(folderPath);

            var row = new VisualElement();
            row.AddToClassList("nt-tree-folder");
            if (isRoot) row.AddToClassList("nt-tree-folder--root");
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

            var label = new Label(isRoot ? "📁 " + RootDisplayName : "📁 " + folderPath);
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

        private VisualElement BuildItemRow(object item)
        {
            string id = GetItemId(item);
            bool isSelected = id == SelectedItemId;

            var container = new VisualElement();
            container.AddToClassList("nt-tree-item");
            if (isSelected) container.AddToClassList("nt-tree-item--selected");
            string folder = GetItemFolder?.Invoke(item) ?? "";
            if (!string.IsNullOrEmpty(folder)) container.AddToClassList("nt-tree-item--indented");
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
