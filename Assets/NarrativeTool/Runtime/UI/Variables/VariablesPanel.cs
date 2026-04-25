using NarrativeTool.Core;
using NarrativeTool.Core.Commands;
using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Variables
{
    /// <summary>
    /// Sidebar panel that lists project variables, lets the user filter,
    /// add, delete, rename, and edit type/default values. Right-click on a
    /// variable, folder, or empty area opens a context menu via the shared
    /// <see cref="ContextMenuController"/>.
    ///
    /// All mutations go through commands on <see cref="SessionState.ProjectCommands"/>
    /// so they're undoable independently of any open graph's history.
    /// </summary>
    public sealed class VariablesPanel : VisualElement
    {
        private ProjectModel project;
        private SessionState session;
        private ContextMenuController contextMenu;
        private EventBus bus;

        // UI
        private TextField filterField;
        private VisualElement listContainer;
        private VisualElement editorContainer;

        // Local state
        private string filter = "";
        private string selectedId;
        private string renamingId;     // null when not renaming
        private readonly HashSet<string> collapsedFolders = new();

        // Per-rebuild lookup so context-menu targets can find rows by id.
        private readonly Dictionary<string, VisualElement> rowsById = new();

        private IDisposable subAdded, subRemoved, subRenamed, subTypeChanged, subDefaultChanged, subMoved;

        public VariablesPanel()
        {
            AddToClassList("nt-vars");
            focusable = true;

            // ── Header (tabs) ──
            var header = new VisualElement();
            header.AddToClassList("nt-vars-tabs");
            var tabActive = new Label("Variables");
            tabActive.AddToClassList("nt-vars-tab");
            tabActive.AddToClassList("nt-vars-tab--active");
            header.Add(tabActive);
            // TODO: Entities tab — currently disabled placeholder. Activate
            // when an entity store lands; the same panel layout will work.
            var tabEntities = new Label("Entities");
            tabEntities.AddToClassList("nt-vars-tab");
            tabEntities.AddToClassList("nt-vars-tab--disabled");
            header.Add(tabEntities);
            Add(header);

            // ── Filter row ──
            var filterRow = new VisualElement();
            filterRow.AddToClassList("nt-vars-filter-row");

            filterField = new TextField { value = "" };
            filterField.AddToClassList("nt-vars-filter");
            // SetPlaceholderText is API-version-dependent; fall back gracefully.
            filterField.RegisterValueChangedCallback(evt =>
            {
                filter = evt.newValue ?? "";
                Rebuild();
            });
            filterRow.Add(filterField);

            var addBtn = new Button(() => AddVariable("")) { text = "+" };
            addBtn.AddToClassList("nt-vars-add-btn");
            filterRow.Add(addBtn);

            Add(filterRow);

            // ── List ──
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("nt-vars-scroll");
            listContainer = scroll.contentContainer;
            listContainer.AddToClassList("nt-vars-list");
            Add(scroll);

            // ── Inline editor (shown when something is selected) ──
            editorContainer = new VisualElement();
            editorContainer.AddToClassList("nt-vars-editor");
            editorContainer.style.display = DisplayStyle.None;
            Add(editorContainer);

            // Right-click on empty list area = "Add" via context menu
            listContainer.RegisterCallback<PointerDownEvent>(OnListPointerDown);

            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        public void Bind(ProjectModel project, SessionState session, ContextMenuController contextMenu)
        {
            Unbind();

            this.project = project;
            this.session = session;
            this.contextMenu = contextMenu;
            this.bus = session.Bus;

            subAdded = bus.Subscribe<VariableAddedEvent>(_ => Rebuild());
            subRemoved = bus.Subscribe<VariableRemovedEvent>(e =>
            {
                if (selectedId == e.VariableId) selectedId = null;
                Rebuild();
            });
            subRenamed = bus.Subscribe<VariableRenamedEvent>(_ => Rebuild());
            subTypeChanged = bus.Subscribe<VariableTypeChangedEvent>(_ => Rebuild());
            subDefaultChanged = bus.Subscribe<VariableDefaultChangedEvent>(_ => RebuildEditorOnly());
            subMoved = bus.Subscribe<VariableMovedEvent>(_ => Rebuild());

            Rebuild();
        }

        public void Unbind()
        {
            subAdded?.Dispose(); subAdded = null;
            subRemoved?.Dispose(); subRemoved = null;
            subRenamed?.Dispose(); subRenamed = null;
            subTypeChanged?.Dispose(); subTypeChanged = null;
            subDefaultChanged?.Dispose(); subDefaultChanged = null;
            subMoved?.Dispose(); subMoved = null;
        }

        // ───────── Commands ─────────

        private CommandSystem Commands => session.ProjectCommands;

        public void AddVariable(string folderPath)
        {
            // Pick a unique default name within the target folder.
            string baseName = "newVariable";
            string name = baseName;
            int n = 1;
            while (project.Variables.NameExistsInFolder(folderPath, name))
                name = $"{baseName}{++n}";

            var v = new VariableDefinition(
                id: "var_" + Guid.NewGuid().ToString("N").Substring(0, 12),
                name: name,
                type: VariableType.Int,
                defaultValue: VariableStore.DefaultFor(VariableType.Int),
                folderPath: folderPath ?? "");

            Commands.Execute(new AddVariableCmd(project, bus, v));
            selectedId = v.Id;
            renamingId = v.Id;   // open straight into rename so the user can name it
            Rebuild();
        }

        public void RemoveVariable(string variableId)
        {
            Commands.Execute(new RemoveVariableCmd(project, bus, variableId));
        }

        public void BeginRename(string variableId)
        {
            renamingId = variableId;
            selectedId = variableId;
            Rebuild();
        }

        private void CommitRename(VariableDefinition v, string newName)
        {
            renamingId = null;
            newName = (newName ?? "").Trim();
            if (string.IsNullOrEmpty(newName) || newName == v.Name)
            {
                Rebuild();
                return;
            }
            if (project.Variables.NameExistsInFolder(v.FolderPath, newName, excludeId: v.Id))
            {
                Debug.LogWarning($"[Variables] Name '{newName}' already exists in folder '{v.FolderPath}'.");
                Rebuild();
                return;
            }
            Commands.Execute(new RenameVariableCmd(project, bus, v.Id, v.Name, newName));
        }

        // ───────── Rebuilds ─────────

        private void Rebuild()
        {
            listContainer.Clear();
            rowsById.Clear();
            if (project == null) return;

            // Group by folder, preserving list order within each.
            var byFolder = new Dictionary<string, List<VariableDefinition>>();
            foreach (var v in project.Variables.Variables)
            {
                if (!MatchesFilter(v)) continue;
                if (!byFolder.TryGetValue(v.FolderPath, out var list))
                    byFolder[v.FolderPath] = list = new List<VariableDefinition>();
                list.Add(v);
            }

            // Render root folders first, then named folders alphabetically.
            // (Nested folders not rendered as a tree yet — single level for v1.)
            var folders = byFolder.Keys.OrderBy(k => k.Length == 0 ? 0 : 1).ThenBy(k => k).ToList();
            foreach (var folder in folders)
            {
                if (!string.IsNullOrEmpty(folder))
                    listContainer.Add(BuildFolderHeader(folder));

                bool collapsed = collapsedFolders.Contains(folder);
                if (!collapsed)
                {
                    foreach (var v in byFolder[folder])
                    {
                        var row = BuildVariableRow(v);
                        listContainer.Add(row);
                        rowsById[v.Id] = row;
                    }
                }
            }

            RebuildEditorOnly();
        }

        private void RebuildEditorOnly()
        {
            editorContainer.Clear();
            if (project == null || string.IsNullOrEmpty(selectedId))
            {
                editorContainer.style.display = DisplayStyle.None;
                return;
            }
            var v = project.Variables.Find(selectedId);
            if (v == null)
            {
                editorContainer.style.display = DisplayStyle.None;
                return;
            }
            editorContainer.style.display = DisplayStyle.Flex;

            // Name
            var nameRow = BuildEditorRow("Name");
            var nameField = new TextField { value = v.Name };
            nameField.AddToClassList("nt-vars-input");
            nameField.RegisterCallback<BlurEvent>(_ => CommitRename(v, nameField.value));
            nameField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    CommitRename(v, nameField.value);
                    e.StopPropagation();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    nameField.SetValueWithoutNotify(v.Name);
                    Focus();
                    e.StopPropagation();
                }
            });
            nameRow.Add(nameField);
            editorContainer.Add(nameRow);

            // Type
            var typeRow = BuildEditorRow("Type");
            var typeChoices = Enum.GetNames(typeof(VariableType)).ToList();
            var typeField = new DropdownField(typeChoices, (int)v.Type);
            typeField.AddToClassList("nt-vars-input");
            typeField.RegisterValueChangedCallback(evt =>
            {
                if (!Enum.TryParse<VariableType>(evt.newValue, out var t)) return;
                if (t == v.Type) return;
                Commands.Execute(new SetVariableTypeCmd(project, bus, v.Id, v.Type, t, v.DefaultValue));
            });
            typeRow.Add(typeField);
            editorContainer.Add(typeRow);

            // Default value (type-dependent input)
            var defRow = BuildEditorRow("Default");
            defRow.Add(BuildDefaultInput(v));
            editorContainer.Add(defRow);
        }

        private VisualElement BuildDefaultInput(VariableDefinition v)
        {
            switch (v.Type)
            {
                case VariableType.Int:
                {
                    var f = new IntegerField { value = v.DefaultValue is int i ? i : 0 };
                    f.AddToClassList("nt-vars-input");
                    f.RegisterCallback<BlurEvent>(_ => CommitDefault(v, f.value));
                    return f;
                }
                case VariableType.Float:
                {
                    var f = new FloatField { value = v.DefaultValue is float fl ? fl : 0f };
                    f.AddToClassList("nt-vars-input");
                    f.RegisterCallback<BlurEvent>(_ => CommitDefault(v, f.value));
                    return f;
                }
                case VariableType.Bool:
                {
                    var f = new Toggle { value = v.DefaultValue is bool b && b };
                    f.AddToClassList("nt-vars-input");
                    f.RegisterValueChangedCallback(evt => CommitDefault(v, evt.newValue));
                    return f;
                }
                case VariableType.String:
                {
                    var f = new TextField { value = v.DefaultValue as string ?? "" };
                    f.AddToClassList("nt-vars-input");
                    f.RegisterCallback<BlurEvent>(_ => CommitDefault(v, f.value));
                    return f;
                }
                default:
                    return new Label("(unsupported type)");
            }
        }

        private void CommitDefault(VariableDefinition v, object newValue)
        {
            if (Equals(v.DefaultValue, newValue)) return;
            Commands.Execute(new SetVariableDefaultCmd(project, bus, v.Id, v.DefaultValue, newValue));
        }

        // ───────── Row builders ─────────

        private VisualElement BuildFolderHeader(string folderPath)
        {
            var row = new VisualElement();
            row.AddToClassList("nt-vars-folder");
            row.userData = folderPath;

            bool collapsed = collapsedFolders.Contains(folderPath);
            var caret = new Label(collapsed ? "▶" : "▼");
            caret.AddToClassList("nt-vars-folder-caret");
            row.Add(caret);

            var label = new Label("📁 " + folderPath);
            label.AddToClassList("nt-vars-folder-label");
            row.Add(label);

            row.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 0)
                {
                    if (collapsed) collapsedFolders.Remove(folderPath);
                    else collapsedFolders.Add(folderPath);
                    Rebuild();
                    e.StopPropagation();
                }
                else if (e.button == 1)
                {
                    contextMenu?.Open(new VariableFolderContextTarget(this, folderPath), e.position);
                    e.StopPropagation();
                }
            });
            return row;
        }

        private VisualElement BuildVariableRow(VariableDefinition v)
        {
            var row = new VisualElement();
            row.AddToClassList("nt-vars-row");
            if (v.Id == selectedId) row.AddToClassList("nt-vars-row--selected");
            if (!string.IsNullOrEmpty(v.FolderPath)) row.AddToClassList("nt-vars-row--indented");
            row.userData = v;

            var swatch = new VisualElement();
            swatch.AddToClassList("nt-vars-swatch");
            swatch.AddToClassList("nt-vars-swatch--" + v.Type.ToString().ToLower());
            row.Add(swatch);

            // Name (or inline rename field)
            if (renamingId == v.Id)
            {
                var renameField = new TextField { value = v.Name };
                renameField.AddToClassList("nt-vars-rename-input");
                row.Add(renameField);
                renameField.schedule.Execute(() => { renameField.Focus(); renameField.SelectAll(); }).StartingIn(0);
                renameField.RegisterCallback<BlurEvent>(_ => CommitRename(v, renameField.value));
                renameField.RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    {
                        CommitRename(v, renameField.value);
                        e.StopPropagation();
                    }
                    else if (e.keyCode == KeyCode.Escape)
                    {
                        renamingId = null;
                        Rebuild();
                        e.StopPropagation();
                    }
                });
            }
            else
            {
                var name = new Label(v.Name);
                name.AddToClassList("nt-vars-name");
                row.Add(name);
            }

            var typeBadge = new Label(v.Type.ToString().ToLower());
            typeBadge.AddToClassList("nt-vars-type-badge");
            row.Add(typeBadge);

            row.RegisterCallback<PointerDownEvent>(e =>
            {
                if (renamingId == v.Id) return;
                if (e.button == 0)
                {
                    selectedId = v.Id;
                    Rebuild();
                    e.StopPropagation();
                }
                else if (e.button == 1)
                {
                    selectedId = v.Id;
                    Rebuild();
                    contextMenu?.Open(new VariableContextTarget(this, v), e.position);
                    e.StopPropagation();
                }
            });
            return row;
        }

        private static VisualElement BuildEditorRow(string labelText)
        {
            var row = new VisualElement();
            row.AddToClassList("nt-vars-editor-row");
            var lbl = new Label(labelText);
            lbl.AddToClassList("nt-vars-editor-label");
            row.Add(lbl);
            return row;
        }

        // ───────── Input ─────────

        private bool MatchesFilter(VariableDefinition v)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            return v.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                || v.FolderPath.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void OnListPointerDown(PointerDownEvent e)
        {
            // Right-click on the empty area of the list (not on a row) → "Add" menu at root.
            if (e.button != 1) return;
            if (e.target is VisualElement ve && ve != listContainer) return;
            contextMenu?.Open(new VariableFolderContextTarget(this, ""), e.position);
            e.StopPropagation();
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
            else if (e.keyCode == KeyCode.F2 && !string.IsNullOrEmpty(selectedId))
            {
                BeginRename(selectedId);
                e.StopPropagation();
            }
            else if ((e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
                     && !string.IsNullOrEmpty(selectedId)
                     && string.IsNullOrEmpty(renamingId))
            {
                RemoveVariable(selectedId);
                e.StopPropagation();
            }
        }
    }

    // ───────── Context-menu targets ─────────

    public sealed class VariableContextTarget
    {
        public VariablesPanel Panel { get; }
        public VariableDefinition Variable { get; }
        public VariableContextTarget(VariablesPanel panel, VariableDefinition variable)
        { Panel = panel; Variable = variable; }
    }

    public sealed class VariableFolderContextTarget
    {
        public VariablesPanel Panel { get; }
        public string FolderPath { get; }
        public VariableFolderContextTarget(VariablesPanel panel, string folderPath)
        { Panel = panel; FolderPath = folderPath ?? ""; }
    }
}
