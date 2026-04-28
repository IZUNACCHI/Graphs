using NarrativeTool.Core.Commands;
using NarrativeTool.Core.Commands.Generic;
using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Project;
using NarrativeTool.UI.FolderTree;
using NarrativeTool.UI.Widgets;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Variables
{
    public sealed class VariablesPanel : VisualElement
    {
        private ProjectModel project;
        private SessionState session;
        private ContextMenuController contextMenu;
        private EventBus bus;

        private TextField filterField;
        private FolderTreeView tree;

        private string selectedId;
        private string focusNameForId;

        private readonly List<IDisposable> subs = new();

        public VariablesPanel()
        {
            AddToClassList("nt-vars");
            focusable = true;

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

            var addFolderBtn = new Button(() => AddFolder("")) { text = "📁" };
            addFolderBtn.AddToClassList("nt-vars-add-btn");
            addFolderBtn.tooltip = "New folder";
            filterRow.Add(addFolderBtn);

            Add(filterRow);

            // ── Tree (bare, no data callbacks yet) ──
            tree = new FolderTreeView
            {
                ShowSearchBar = false,
                RootDisplayName = null,
                BuildItemHeader = item => BuildVariableHeader((VariableDefinition)item),
                BuildItemDetail = item => BuildVariableEditor((VariableDefinition)item),
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

            // ── Wire the tree with data, events, and drag‑drop ──
            WireTree();

            // ── Event subscriptions ──
            subs.Add(bus.Subscribe<VariableAddedEvent>(_ => Rebuild()));
            subs.Add(bus.Subscribe<VariableRemovedEvent>(e =>
            {
                if (selectedId == e.VariableId) selectedId = null;
                Rebuild();
            }));
            subs.Add(bus.Subscribe<VariableRenamedEvent>(_ => Rebuild()));
            subs.Add(bus.Subscribe<VariableTypeChangedEvent>(_ => Rebuild()));
            subs.Add(bus.Subscribe<VariableDefaultChangedEvent>(_ => Rebuild()));
            subs.Add(bus.Subscribe<VariableMovedEvent>(_ => Rebuild()));
            subs.Add(bus.Subscribe<VariableFolderAddedEvent>(_ => Rebuild()));
            subs.Add(bus.Subscribe<VariableFolderRemovedEvent>(_ => Rebuild()));
            subs.Add(bus.Subscribe<VariableFolderRenamedEvent>(_ => Rebuild()));
            subs.Add(bus.Subscribe<VariableEnumTypeChangedEvent>(_ => Rebuild()));
            subs.Add(bus.Subscribe<EnumAddedEvent>(_ => Rebuild()));
            subs.Add(bus.Subscribe<EnumRemovedEvent>(_ => Rebuild()));
            subs.Add(bus.Subscribe<EnumRenamedEvent>(_ => Rebuild()));
            subs.Add(bus.Subscribe<EnumMemberChangedEvent>(_ => Rebuild()));

            Rebuild();
        }

        public void Unbind()
        {
            // Dispose subscriptions
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
                tree.OnItemClicked = null;
                tree.OnItemDoubleClicked = null;
                tree.OnItemContextMenu = null;
                tree.OnFolderContextMenu = null;
                tree.OnEmptyContextMenu = null;
                tree.OnFolderRenameCommit = null;
                tree.OnItemMoved = null;
                tree.OnFolderMoved = null;
                tree.AllowDragDrop = false;
                tree.Rebuild();
            }
        }

        // ─── Wire data, events, drag‑drop to the tree ───
        private void WireTree()
        {
            tree.GetFolders = () => project.Variables.Folders;
            tree.GetItems = () => project?.Variables.Items.Cast<object>();
            tree.GetItemFolder = item => ((VariableDefinition)item).FolderPath;
            tree.GetItemId = item => ((VariableDefinition)item).Id;
            tree.GetItemSearchText = item =>
            {
                var v = (VariableDefinition)item;
                return v.Name + " " + v.FolderPath;
            };

            tree.OnItemClicked = item => SelectVariable(((VariableDefinition)item).Id, toggle: true);
            tree.OnItemContextMenu = (item, pos) =>
            {
                var v = (VariableDefinition)item;
                SelectVariable(v.Id, toggle: false);
                contextMenu?.Open(new VariableContextTarget(this, v), pos);
            };
            tree.OnFolderContextMenu = (folder, pos) =>
                contextMenu?.Open(new VariableFolderContextTarget(this, folder), pos);
            tree.OnEmptyContextMenu = (parent, pos) =>
                contextMenu?.Open(new VariableFolderContextTarget(this, parent), pos);
            tree.OnFolderRenameCommit = (oldPath, newPath) =>
            {
                CommitFolderRename(oldPath, newPath);
            };

            // Drag‑and‑drop
            tree.AllowDragDrop = true;

            tree.OnItemMoved = (item, newFolder) =>
            {
                var v = (VariableDefinition)item;
                if (v.FolderPath == newFolder) return;
                Commands.Execute(new MoveItemCmd<VariableDefinition>(
                    "Variable", v, project.Variables,
                    v.FolderPath, newFolder,
                    doPublish: () => { }, undoPublish: () => { }
                ));
                Rebuild();
            };

            tree.OnFolderMoved = (folderPath, newParent) =>
            {
                string folderName = folderPath.Split('/').Last();
                string newPath = string.IsNullOrEmpty(newParent) ? folderName : newParent + "/" + folderName;
                Commands.Execute(new RenameFolderCmd<VariableDefinition>(
                    "Variable", project.Variables,
                    folderPath, newPath,
                    onRename: (o, n) => { },
                    onItemPathChanged: (item, oldItemPath, newItemPath) => { }
                ));
                Rebuild();
            };
        }

        private CommandSystem Commands => session.ProjectCommands;

        private void Rebuild()
        {
            if (tree == null) return;
            tree.SelectedItemId = selectedId;
            tree.Rebuild();
        }

        // ───────── Variable commands (unchanged) ─────────
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

            Commands.Execute(new AddItemCmd<VariableDefinition>(
                "Variable",
                project.Variables,
                v,
                doPublish: () => bus.Publish(new VariableAddedEvent(project.Id, v.Id)),
                undoPublish: () => bus.Publish(new VariableRemovedEvent(project.Id, v.Id))
            ));
            selectedId = v.Id;
            focusNameForId = v.Id;
            Rebuild();
        }

        public void RemoveVariable(string variableId)
        {
            Commands.Execute(new RemoveItemCmd<VariableDefinition>(
                "Variable",
                project.Variables,
                variableId,
                doPublish: () => bus.Publish(new VariableRemovedEvent(project.Id, variableId)),
                undoPublish: () => bus.Publish(new VariableAddedEvent(project.Id, variableId))
            ));
        }

        public void BeginRenameVariable(string variableId)
        {
            selectedId = variableId;
            focusNameForId = variableId;
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
            Commands.Execute(new RenameItemCmd<VariableDefinition>(
                "Variable",
                v,
                v.Name,
                newName,
                doPublish: () => bus.Publish(new VariableRenamedEvent(project.Id, v.Id, v.Name, newName)),
                undoPublish: () => bus.Publish(new VariableRenamedEvent(project.Id, v.Id, newName, v.Name))
            ));
        }

        private void CommitDefault(VariableDefinition v, object newValue)
        {
            if (Equals(v.DefaultValue, newValue)) return;
            Commands.Execute(new SetVariableDefaultCmd(project, bus, v.Id, v.DefaultValue, newValue));
        }

        // ───────── Folder commands ─────────
        public void AddFolder(string parent)
        {
            string parentPrefix = string.IsNullOrEmpty(parent) ? "" : parent + "/";
            string baseName = "newFolder";
            string name = baseName;
            int n = 1;
            while (project.Variables.Folders.Contains(parentPrefix + name))
                name = $"{baseName}{++n}";
            string fullPath = parentPrefix + name;

            Commands.Execute(new AddFolderCmd<VariableDefinition>(
                "Variable",
                project.Variables,
                fullPath,
                doPublish: () => bus.Publish(new VariableFolderAddedEvent(project.Id, fullPath)),
                undoPublish: () => bus.Publish(new VariableFolderRemovedEvent(project.Id, fullPath))
            ));
            if (!string.IsNullOrEmpty(parent)) tree.SetFolderCollapsed(parent, false);
            BeginRenameFolder(fullPath);
        }

        public void RemoveFolder(string folderPath)
        {
            Commands.Execute(new RemoveFolderCmd<VariableDefinition>(
                "Variable",
                project.Variables,
                folderPath,
                onItemRemoved: v => bus.Publish(new VariableRemovedEvent(project.Id, v.Id)),
                onFolderRemoved: f => bus.Publish(new VariableFolderRemovedEvent(project.Id, f)),
                onItemRestored: v => bus.Publish(new VariableAddedEvent(project.Id, v.Id)),
                onFolderRestored: f => bus.Publish(new VariableFolderAddedEvent(project.Id, f))
            ));
        }

        public void BeginRenameFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            tree.RenamingFolderPath = folderPath;
            tree.SetFolderCollapsed(folderPath, false);
            tree.Rebuild();
        }

        private void CommitFolderRename(string oldPath, string newPath)
        {
            if (project.Variables.Folders.Contains(newPath))
            {
                Debug.LogWarning($"[Variables] Folder '{newPath}' already exists.");
                tree.Rebuild();
                return;
            }
            Commands.Execute(new RenameFolderCmd<VariableDefinition>(
                "Variable",
                project.Variables,
                oldPath,
                newPath,
                onRename: (o, n) => bus.Publish(new VariableFolderRenamedEvent(project.Id, o, n)),
                onItemPathChanged: (item, oldItemPath, newItemPath) => { }
            ));
        }

        // ───────── Selection / inline edit ─────────
        private void SelectVariable(string id, bool toggle)
        {
            if (toggle && selectedId == id) selectedId = null;
            else selectedId = id;
            Rebuild();
        }

        // ───────── Row builders (unchanged) ─────────
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

            if (focusNameForId == v.Id)
            {
                focusNameForId = null;
                nameField.schedule.Execute(() =>
                {
                    nameField.Focus();
                    nameField.SelectAll();
                }).StartingIn(0);
            }

            var typeRow = BuildEditorRow("Type");
            var typeChoices = Enum.GetNames(typeof(VariableType)).ToList();
            var typeField = new DropdownField(typeChoices, (int)v.Type);
            typeField.AddToClassList("nt-vars-input");
            typeField.RegisterValueChangedCallback(evt =>
            {
                if (!Enum.TryParse<VariableType>(evt.newValue, out var t)) return;
                if (t == v.Type) return;
                Commands.Execute(new SetVariableTypeCmd(project, bus, v.Id, v.Type, t,
                                                        v.DefaultValue, v.EnumTypeId));
            });
            typeRow.Add(typeField);
            editor.Add(typeRow);

            if (v.Type == VariableType.Enum)
            {
                var enumRow = BuildEditorRow("Enum");
                enumRow.Add(BuildEnumTypePicker(v));
                editor.Add(enumRow);
            }

            var defRow = BuildEditorRow("Default");
            defRow.Add(BuildDefaultInput(v));
            editor.Add(defRow);

            return editor;
        }

        private VisualElement BuildEnumTypePicker(VariableDefinition v)
        {
            var enums = project.Enums.Items;
            if (enums.Count == 0)
            {
                var msg = new Label("(no enums defined)");
                msg.AddToClassList("nt-vars-input");
                return msg;
            }
            var names = enums.Select(e => e.Name).ToList();
            int currentIdx = enums.FindIndex(e => e.Id == v.EnumTypeId);
            var dd = new DropdownField(names, Mathf.Max(0, currentIdx));
            dd.AddToClassList("nt-vars-input");
            dd.RegisterValueChangedCallback(evt =>
            {
                int idx = names.IndexOf(evt.newValue);
                if (idx < 0) return;
                string newId = enums[idx].Id;
                if (newId == v.EnumTypeId) return;
                Commands.Execute(new SetVariableEnumTypeCmd(project, bus, v.Id,
                                                            v.EnumTypeId, newId, v.DefaultValue));
            });
            return dd;
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
                case VariableType.Enum:
                    {
                        var enumDef = project.Enums.Find(v.EnumTypeId);
                        if (enumDef == null || enumDef.Members.Count == 0)
                        {
                            var msg = new Label(enumDef == null
                                ? "(pick an enum first)"
                                : "(enum has no members)");
                            msg.AddToClassList("nt-vars-input");
                            return msg;
                        }
                        var memberNames = enumDef.Members.Select(m => m.Name).ToList();
                        int idx = enumDef.Members.FindIndex(m => m.Id == (v.DefaultValue as string));
                        var dd = new DropdownField(memberNames, Mathf.Max(0, idx));
                        dd.AddToClassList("nt-vars-input");
                        dd.RegisterValueChangedCallback(evt =>
                        {
                            int sel = memberNames.IndexOf(evt.newValue);
                            if (sel < 0) return;
                            CommitDefault(v, enumDef.Members[sel].Id);
                        });
                        return dd;
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

    // ───────── Context-menu targets (unchanged) ─────────
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