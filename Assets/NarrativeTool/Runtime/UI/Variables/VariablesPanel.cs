using NarrativeTool.Core.Commands;
using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;
using NarrativeTool.UI.FolderTree;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Variables
{
    /// <summary>
    /// Sidebar panel listing project variables grouped by folder, with inline
    /// editing of the selected item. All mutations go through commands on
    /// <see cref="SessionState.ProjectCommands"/>.
    ///
    /// Folder rendering is delegated to <see cref="FolderTreeView"/> — a
    /// reusable widget that the future Graphs panel will share.
    /// </summary>
    public sealed class VariablesPanel : VisualElement
    {
        private ProjectModel project;
        private SessionState session;
        private ContextMenuController contextMenu;
        private EventBus bus;

        // UI
        private TextField filterField;
        private FolderTreeView tree;

        // Local state
        private string selectedId;
        // When set, the inline name field of the selected variable should
        // grab focus on next rebuild — used after "Rename" is chosen so the
        // user lands directly in the name input.
        private string focusNameForId;

        private IDisposable subAdded, subRemoved, subRenamed, subTypeChanged, subDefaultChanged, subMoved;
        private IDisposable subFolderAdded, subFolderRemoved, subFolderRenamed;

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
            // TODO: Entities tab — placeholder until an entity store lands.
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
            filterField.RegisterValueChangedCallback(evt =>
            {
                if (tree != null)
                {
                    tree.Filter = evt.newValue ?? "";
                    tree.Rebuild();
                }
            });
            filterRow.Add(filterField);

            var addBtn = new Button(() => AddVariable("")) { text = "+" };
            addBtn.AddToClassList("nt-vars-add-btn");
            filterRow.Add(addBtn);

            Add(filterRow);

            // ── Tree ──
            tree = new FolderTreeView
            {
                GetFolders = () => project?.Variables.Folders ?? (IReadOnlyList<string>)Array.Empty<string>(),
                GetItems = () => project?.Variables.Variables.Cast<object>() ?? Enumerable.Empty<object>(),
                GetItemFolder = item => ((VariableDefinition)item).FolderPath,
                GetItemId = item => ((VariableDefinition)item).Id,
                GetItemSearchText = item =>
                {
                    var v = (VariableDefinition)item;
                    return v.Name + " " + v.FolderPath;
                },
                BuildItemHeader = item => BuildVariableHeader((VariableDefinition)item),
                BuildItemDetail = item => BuildVariableEditor((VariableDefinition)item),
                OnItemClicked = item => SelectVariable(((VariableDefinition)item).Id, toggle: true),
                OnItemContextMenu = (item, pos) =>
                {
                    var v = (VariableDefinition)item;
                    SelectVariable(v.Id, toggle: false);
                    contextMenu?.Open(new VariableContextTarget(this, v), pos);
                },
                OnFolderContextMenu = (folder, pos) =>
                    contextMenu?.Open(new VariableFolderContextTarget(this, folder), pos),
                OnEmptyContextMenu = (parent, pos) =>
                    contextMenu?.Open(new VariableFolderContextTarget(this, parent), pos),
                OnFolderRenameCommit = (oldPath, newPath) =>
                {
                    if (project.Variables.FolderExists(newPath))
                    {
                        Debug.LogWarning($"[Variables] Folder '{newPath}' already exists.");
                        Rebuild();
                        return;
                    }
                    Commands.Execute(new RenameVariableFolderCmd(project, bus, oldPath, newPath));
                },
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

            subAdded = bus.Subscribe<VariableAddedEvent>(_ => Rebuild());
            subRemoved = bus.Subscribe<VariableRemovedEvent>(e =>
            {
                if (selectedId == e.VariableId) selectedId = null;
                Rebuild();
            });
            subRenamed = bus.Subscribe<VariableRenamedEvent>(_ => Rebuild());
            subTypeChanged = bus.Subscribe<VariableTypeChangedEvent>(_ => Rebuild());
            subDefaultChanged = bus.Subscribe<VariableDefaultChangedEvent>(_ => Rebuild());
            subMoved = bus.Subscribe<VariableMovedEvent>(_ => Rebuild());
            subFolderAdded = bus.Subscribe<VariableFolderAddedEvent>(_ => Rebuild());
            subFolderRemoved = bus.Subscribe<VariableFolderRemovedEvent>(_ => Rebuild());
            subFolderRenamed = bus.Subscribe<VariableFolderRenamedEvent>(_ => Rebuild());

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
            subFolderAdded?.Dispose(); subFolderAdded = null;
            subFolderRemoved?.Dispose(); subFolderRemoved = null;
            subFolderRenamed?.Dispose(); subFolderRenamed = null;
        }

        private CommandSystem Commands => session.ProjectCommands;

        private void Rebuild()
        {
            if (tree == null) return;
            tree.SelectedItemId = selectedId;
            tree.Rebuild();
        }

        // ───────── Variable commands ─────────

        public void AddVariable(string folderPath)
        {
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
            focusNameForId = v.Id;   // jump straight into the name field
            Rebuild();
        }

        public void RemoveVariable(string variableId)
        {
            Commands.Execute(new RemoveVariableCmd(project, bus, variableId));
        }

        public void BeginRenameVariable(string variableId)
        {
            selectedId = variableId;
            focusNameForId = variableId;
            Rebuild();
        }

        // ───────── Folder commands ─────────

        public void AddFolder()
        {
            string baseName = "newFolder";
            string name = baseName;
            int n = 1;
            while (project.Variables.FolderExists(name)) name = $"{baseName}{++n}";
            Commands.Execute(new AddVariableFolderCmd(project, bus, name));
            BeginRenameFolder(name);   // drop the user straight into rename
        }

        public void RemoveFolder(string folderPath)
        {
            // Cascade delete: remove all variables in the folder, then remove
            // the folder itself, all in one transaction so undo restores both.
            using var tx = Commands.BeginTransaction($"Remove folder \"{folderPath}\"");
            var inFolder = project.Variables.Variables
                .Where(v => v.FolderPath == folderPath)
                .Select(v => v.Id)
                .ToList();
            foreach (var id in inFolder)
                Commands.Execute(new RemoveVariableCmd(project, bus, id));
            Commands.Execute(new RemoveVariableFolderCmd(project, bus, folderPath));
        }

        public void BeginRenameFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            tree.RenamingFolderPath = folderPath;
            // Make sure the folder is expanded so the user can see what's inside.
            tree.SetFolderCollapsed(folderPath, false);
            tree.Rebuild();
        }

        // ───────── Selection / inline edit ─────────

        private void SelectVariable(string id, bool toggle)
        {
            if (toggle && selectedId == id)
            {
                selectedId = null;
            }
            else
            {
                selectedId = id;
            }
            Rebuild();
        }

        private void CommitRename(VariableDefinition v, string newName)
        {
            newName = (newName ?? "").Trim();
            if (string.IsNullOrEmpty(newName) || newName == v.Name) return;
            if (project.Variables.NameExistsInFolder(v.FolderPath, newName, excludeId: v.Id))
            {
                Debug.LogWarning($"[Variables] Name '{newName}' already exists in folder '{v.FolderPath}'.");
                return;
            }
            Commands.Execute(new RenameVariableCmd(project, bus, v.Id, v.Name, newName));
        }

        private void CommitDefault(VariableDefinition v, object newValue)
        {
            if (Equals(v.DefaultValue, newValue)) return;
            Commands.Execute(new SetVariableDefaultCmd(project, bus, v.Id, v.DefaultValue, newValue));
        }

        // ───────── Row builders ─────────

        private VisualElement BuildVariableHeader(VariableDefinition v)
        {
            var row = new VisualElement();
            row.AddToClassList("nt-vars-row");

            var swatch = new VisualElement();
            swatch.AddToClassList("nt-vars-swatch");
            swatch.AddToClassList("nt-vars-swatch--" + v.Type.ToString().ToLower());
            row.Add(swatch);

            var name = new Label(v.Name);
            name.AddToClassList("nt-vars-name");
            row.Add(name);

            var typeBadge = new Label(v.Type.ToString().ToLower());
            typeBadge.AddToClassList("nt-vars-type-badge");
            row.Add(typeBadge);
            return row;
        }

        private VisualElement BuildVariableEditor(VariableDefinition v)
        {
            var editor = new VisualElement();
            editor.AddToClassList("nt-vars-editor");

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
            editor.Add(nameRow);

            // If this is the variable that just asked for focus, grab it next frame.
            if (focusNameForId == v.Id)
            {
                focusNameForId = null;
                nameField.schedule.Execute(() =>
                {
                    nameField.Focus();
                    nameField.SelectAll();
                }).StartingIn(0);
            }

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
            editor.Add(typeRow);

            // Default
            var defRow = BuildEditorRow("Default");
            defRow.Add(BuildDefaultInput(v));
            editor.Add(defRow);

            return editor;
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
                BeginRenameVariable(selectedId);
                e.StopPropagation();
            }
            else if ((e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
                     && !string.IsNullOrEmpty(selectedId))
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
        public string FolderPath { get; }     // "" for root / empty area
        public VariableFolderContextTarget(VariablesPanel panel, string folderPath)
        { Panel = panel; FolderPath = folderPath ?? ""; }
    }
}
