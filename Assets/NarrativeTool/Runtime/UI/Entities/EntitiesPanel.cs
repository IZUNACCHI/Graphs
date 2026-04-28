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

namespace NarrativeTool.UI.Entities
{
    public sealed class EntitiesPanel : VisualElement
    {
        private ProjectModel project;
        private SessionState session;
        private ContextMenuController contextMenu;
        private EventBus bus;

        private TextField filterField;
        private FolderTreeView entityTree;
        private FolderTreeView enumTree;

        private string selectedEntityId;
        private string selectedEnumId;
        private string focusNameForEntityId;
        private string focusNameForEnumId;

        private readonly List<IDisposable> subs = new();

        public EntitiesPanel()
        {
            AddToClassList("nt-vars");
            focusable = true;

            // Filter row
            var filterRow = new VisualElement();
            filterRow.AddToClassList("nt-vars-filter-row");
            filterField = new TextField { value = "" };
            filterField.AddToClassList("nt-vars-filter");
            filterField.RegisterValueChangedCallback(evt =>
            {
                var f = evt.newValue ?? "";
                if (entityTree != null) { entityTree.Filter = f; entityTree.Rebuild(); }
                if (enumTree != null) { enumTree.Filter = f; enumTree.Rebuild(); }
            });
            filterRow.Add(filterField);
            Add(filterRow);

            // Entities section
            var entitiesHeader = BuildSectionHeader("Entities", () => AddEntity(""));
            Add(entitiesHeader);
            entityTree = new FolderTreeView
            {
                ShowSearchBar = false,
                RootDisplayName = null,
                BuildItemHeader = it => BuildEntityHeader((EntityDefinition)it),
                BuildItemDetail = it => BuildEntityEditor((EntityDefinition)it),
            };
            Add(entityTree);

            // Enums section
            var enumsHeader = BuildSectionHeader("Enums", () => AddEnum(""));
            enumsHeader.AddToClassList("nt-vars-section--second");
            Add(enumsHeader);
            enumTree = new FolderTreeView
            {
                ShowSearchBar = false,
                RootDisplayName = null,
                BuildItemHeader = it => BuildEnumHeader((EnumDefinition)it),
                BuildItemDetail = it => BuildEnumEditor((EnumDefinition)it),
            };
            Add(enumTree);

            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        public void Bind(ProjectModel project, SessionState session, ContextMenuController contextMenu)
        {
            Unbind();
            this.project = project;
            this.session = session;
            this.contextMenu = contextMenu;
            this.bus = session.Bus;

            // ── Wire entity tree ──
            WireEntityTree();

            // ── Wire enum tree ──
            WireEnumTree();

            // ── Event subscriptions ──
            subs.Add(bus.Subscribe<EntityAddedEvent>(_ => RebuildAll()));
            subs.Add(bus.Subscribe<EntityRemovedEvent>(e =>
            {
                if (selectedEntityId == e.EntityId) selectedEntityId = null;
                RebuildAll();
            }));
            subs.Add(bus.Subscribe<EntityRenamedEvent>(_ => RebuildAll()));
            subs.Add(bus.Subscribe<EntityFieldChangedEvent>(_ => RebuildAll()));
            subs.Add(bus.Subscribe<EntityFolderAddedEvent>(_ => RebuildAll()));
            subs.Add(bus.Subscribe<EntityFolderRemovedEvent>(_ => RebuildAll()));
            subs.Add(bus.Subscribe<EntityFolderRenamedEvent>(_ => RebuildAll()));

            subs.Add(bus.Subscribe<EnumAddedEvent>(_ => RebuildAll()));
            subs.Add(bus.Subscribe<EnumRemovedEvent>(e =>
            {
                if (selectedEnumId == e.EnumId) selectedEnumId = null;
                RebuildAll();
            }));
            subs.Add(bus.Subscribe<EnumRenamedEvent>(_ => RebuildAll()));
            subs.Add(bus.Subscribe<EnumMemberChangedEvent>(_ => RebuildAll()));
            subs.Add(bus.Subscribe<EnumFolderAddedEvent>(_ => RebuildAll()));
            subs.Add(bus.Subscribe<EnumFolderRemovedEvent>(_ => RebuildAll()));
            subs.Add(bus.Subscribe<EnumFolderRenamedEvent>(_ => RebuildAll()));

            RebuildAll();
        }

        public void Unbind()
        {
            foreach (var s in subs) s?.Dispose();
            subs.Clear();

            // Dismiss any open modals
            foreach (var child in Children().OfType<ModalConfirm>().ToList())
                child.RemoveFromHierarchy();

            // Clear entity tree
            if (entityTree != null)
            {
                entityTree.GetFolders = null;
                entityTree.GetItems = null;
                entityTree.GetItemFolder = null;
                entityTree.GetItemId = null;
                entityTree.GetItemSearchText = null;
                entityTree.OnItemClicked = null;
                entityTree.OnItemDoubleClicked = null;
                entityTree.OnItemContextMenu = null;
                entityTree.OnFolderContextMenu = null;
                entityTree.OnEmptyContextMenu = null;
                entityTree.OnFolderRenameCommit = null;
                entityTree.OnItemMoved = null;
                entityTree.OnFolderMoved = null;
                entityTree.AllowDragDrop = false;
                entityTree.Rebuild();
            }

            // Clear enum tree
            if (enumTree != null)
            {
                enumTree.GetFolders = null;
                enumTree.GetItems = null;
                enumTree.GetItemFolder = null;
                enumTree.GetItemId = null;
                enumTree.GetItemSearchText = null;
                enumTree.OnItemClicked = null;
                enumTree.OnItemDoubleClicked = null;
                enumTree.OnItemContextMenu = null;
                enumTree.OnFolderContextMenu = null;
                enumTree.OnEmptyContextMenu = null;
                enumTree.OnFolderRenameCommit = null;
                enumTree.OnItemMoved = null;
                enumTree.OnFolderMoved = null;
                enumTree.AllowDragDrop = false;
                enumTree.Rebuild();
            }
        }

        private void WireEntityTree()
        {
            entityTree.GetFolders = () => (IReadOnlyList<string>)(project?.Entities.Folders) ?? Array.Empty<string>();
            entityTree.GetItems = () => project?.Entities.Items.Cast<object>() ?? Enumerable.Empty<object>();
            entityTree.GetItemFolder = it => ((EntityDefinition)it).FolderPath;
            entityTree.GetItemId = it => ((EntityDefinition)it).Id;
            entityTree.GetItemSearchText = it => { var e = (EntityDefinition)it; return e.Name + " " + e.FolderPath; };

            entityTree.OnItemClicked = it => SelectEntity(((EntityDefinition)it).Id, toggle: true);
            entityTree.OnItemContextMenu = (it, pos) =>
            {
                var e = (EntityDefinition)it;
                SelectEntity(e.Id, toggle: false);
                contextMenu?.Open(new EntityContextTarget(this, e), pos);
            };
            entityTree.OnFolderContextMenu = (folder, pos) =>
                contextMenu?.Open(new EntityFolderContextTarget(this, folder), pos);
            entityTree.OnEmptyContextMenu = (parent, pos) =>
                contextMenu?.Open(new EntityFolderContextTarget(this, parent), pos);
            entityTree.OnFolderRenameCommit = (oldPath, newPath) =>
            {
                if (project.Entities.Folders.Contains(newPath))
                { Debug.LogWarning($"[Entities] Folder '{newPath}' already exists."); RebuildAll(); return; }
                Commands.Execute(new RenameFolderCmd<EntityDefinition>(
                    "Entity",
                    project.Entities,
                    oldPath, newPath,
                    onRename: (o, n) => bus.Publish(new EntityFolderRenamedEvent(project.Id, o, n)),
                    onItemPathChanged: (item, oldItemPath, newItemPath) => { }
                ));
            };

            // Drag‑and‑drop
            entityTree.AllowDragDrop = true;
            entityTree.OnItemMoved = (item, newFolder) =>
            {
                var entity = (EntityDefinition)item;
                if (entity.FolderPath == newFolder) return;
                Commands.Execute(new MoveItemCmd<EntityDefinition>(
                    "Entity", entity, project.Entities,
                    entity.FolderPath, newFolder,
                    doPublish: () => { }, undoPublish: () => { }
                ));
                RebuildAll();
            };
            entityTree.OnFolderMoved = (folderPath, newParent) =>
            {
                string folderName = folderPath.Split('/').Last();
                string newPath = string.IsNullOrEmpty(newParent) ? folderName : newParent + "/" + folderName;
                Commands.Execute(new RenameFolderCmd<EntityDefinition>(
                    "Entity", project.Entities,
                    folderPath, newPath,
                    onRename: (o, n) => { },
                    onItemPathChanged: (item, oldItemPath, newItemPath) => { }
                ));
                RebuildAll();
            };
        }

        private void WireEnumTree()
        {
            enumTree.GetFolders = () => (IReadOnlyList<string>)(project?.Enums.Folders) ?? Array.Empty<string>();
            enumTree.GetItems = () => project?.Enums.Items.Cast<object>() ?? Enumerable.Empty<object>();
            enumTree.GetItemFolder = it => ((EnumDefinition)it).FolderPath;
            enumTree.GetItemId = it => ((EnumDefinition)it).Id;
            enumTree.GetItemSearchText = it => { var e = (EnumDefinition)it; return e.Name + " " + e.FolderPath; };

            enumTree.OnItemClicked = it => SelectEnum(((EnumDefinition)it).Id, toggle: true);
            enumTree.OnItemContextMenu = (it, pos) =>
            {
                var e = (EnumDefinition)it;
                SelectEnum(e.Id, toggle: false);
                contextMenu?.Open(new EnumContextTarget(this, e), pos);
            };
            enumTree.OnFolderContextMenu = (folder, pos) =>
                contextMenu?.Open(new EnumFolderContextTarget(this, folder), pos);
            enumTree.OnEmptyContextMenu = (parent, pos) =>
                contextMenu?.Open(new EnumFolderContextTarget(this, parent), pos);
            enumTree.OnFolderRenameCommit = (oldPath, newPath) =>
            {
                if (project.Enums.Folders.Contains(newPath))
                { Debug.LogWarning($"[Enums] Folder '{newPath}' already exists."); RebuildAll(); return; }
                Commands.Execute(new RenameFolderCmd<EnumDefinition>(
                    "Enum",
                    project.Enums,
                    oldPath, newPath,
                    onRename: (o, n) => bus.Publish(new EnumFolderRenamedEvent(project.Id, o, n)),
                    onItemPathChanged: (item, oldItemPath, newItemPath) => { }
                ));
            };

            // Drag‑and‑drop
            enumTree.AllowDragDrop = true;
            enumTree.OnItemMoved = (item, newFolder) =>
            {
                var enumDef = (EnumDefinition)item;
                if (enumDef.FolderPath == newFolder) return;
                Commands.Execute(new MoveItemCmd<EnumDefinition>(
                    "Enum", enumDef, project.Enums,
                    enumDef.FolderPath, newFolder,
                    doPublish: () => { }, undoPublish: () => { }
                ));
                RebuildAll();
            };
            enumTree.OnFolderMoved = (folderPath, newParent) =>
            {
                string folderName = folderPath.Split('/').Last();
                string newPath = string.IsNullOrEmpty(newParent) ? folderName : newParent + "/" + folderName;
                Commands.Execute(new RenameFolderCmd<EnumDefinition>(
                    "Enum", project.Enums,
                    folderPath, newPath,
                    onRename: (o, n) => { },
                    onItemPathChanged: (item, oldItemPath, newItemPath) => { }
                ));
                RebuildAll();
            };
        }

        private CommandSystem Commands => session.ProjectCommands;

        private void RebuildAll()
        {
            if (entityTree != null) { entityTree.SelectedItemId = selectedEntityId; entityTree.Rebuild(); }
            if (enumTree != null) { enumTree.SelectedItemId = selectedEnumId; enumTree.Rebuild(); }
        }

        // ───────── Tree wiring ─────────

        private FolderTreeView BuildEntityTree()
        {
            var tree = new FolderTreeView
            {
               
                GetFolders = () => project?.Entities.Folders ?? (IReadOnlyList<string>)Array.Empty<string>(),
                GetItems = () => project?.Entities.Items.Cast<object>() ?? Enumerable.Empty<object>(),
                GetItemFolder = it => ((EntityDefinition)it).FolderPath,
                GetItemId = it => ((EntityDefinition)it).Id,
                GetItemSearchText = it => { var e = (EntityDefinition)it; return e.Name + " " + e.FolderPath; },
                BuildItemHeader = it => BuildEntityHeader((EntityDefinition)it),
                BuildItemDetail = it => BuildEntityEditor((EntityDefinition)it),

                OnItemClicked = it => SelectEntity(((EntityDefinition)it).Id, toggle: true),
                OnItemContextMenu = (it, pos) =>
                {
                    var e = (EntityDefinition)it;
                    SelectEntity(e.Id, toggle: false);
                    contextMenu?.Open(new EntityContextTarget(this, e), pos);
                },
                OnFolderContextMenu = (folder, pos) =>
                    contextMenu?.Open(new EntityFolderContextTarget(this, folder), pos),
                OnEmptyContextMenu = (parent, pos) =>
                    contextMenu?.Open(new EntityFolderContextTarget(this, parent), pos),
                OnFolderRenameCommit = (oldPath, newPath) =>
                {
                    if (project.Entities.Folders.Contains(newPath))
                    { Debug.LogWarning($"[Entities] Folder '{newPath}' already exists."); RebuildAll(); return; }
                    Commands.Execute(new RenameFolderCmd<EntityDefinition>(
                        "Entity",
                        project.Entities,
                        oldPath, newPath,
                        onRename: (o, n) => bus.Publish(new EntityFolderRenamedEvent(project.Id, o, n)),
                        onItemPathChanged: (item, oldItemPath, newItemPath) => { }
                    ));
                },
            };

            // ─── Drag‑and‑drop (added after initializer) ───
            tree.AllowDragDrop = true;

            tree.OnItemMoved = (item, newFolder) =>
            {
                var entity = (EntityDefinition)item;
                if (entity.FolderPath == newFolder) return;
                Commands.Execute(new MoveItemCmd<EntityDefinition>(
                    "Entity", entity, project.Entities,
                    entity.FolderPath, newFolder,
                    doPublish: () => { }, undoPublish: () => { }
                ));
                RebuildAll();
            };

            tree.OnFolderMoved = (folderPath, newParent) =>
            {
                string folderName = folderPath.Split('/').Last();
                string newPath = string.IsNullOrEmpty(newParent) ? folderName : newParent + "/" + folderName;
                Commands.Execute(new RenameFolderCmd<EntityDefinition>(
                    "Entity", project.Entities,
                    folderPath, newPath,
                    onRename: (o, n) => { },
                    onItemPathChanged: (item, oldItemPath, newItemPath) => { }
                ));
                RebuildAll();
            };

            return tree;
        }

        private FolderTreeView BuildEnumTree()
        {
            var tree = new FolderTreeView
            {
                GetFolders = () => project?.Enums.Folders ?? (IReadOnlyList<string>)Array.Empty<string>(),
                GetItems = () => project?.Enums.Items.Cast<object>() ?? Enumerable.Empty<object>(),
                GetItemFolder = it => ((EnumDefinition)it).FolderPath,
                GetItemId = it => ((EnumDefinition)it).Id,
                GetItemSearchText = it => { var e = (EnumDefinition)it; return e.Name + " " + e.FolderPath; },
                BuildItemHeader = it => BuildEnumHeader((EnumDefinition)it),
                BuildItemDetail = it => BuildEnumEditor((EnumDefinition)it),
                OnItemClicked = it => SelectEnum(((EnumDefinition)it).Id, toggle: true),
                OnItemContextMenu = (it, pos) =>
                {
                    var e = (EnumDefinition)it;
                    SelectEnum(e.Id, toggle: false);
                    contextMenu?.Open(new EnumContextTarget(this, e), pos);
                },
                OnFolderContextMenu = (folder, pos) =>
                    contextMenu?.Open(new EnumFolderContextTarget(this, folder), pos),
                OnEmptyContextMenu = (parent, pos) =>
                    contextMenu?.Open(new EnumFolderContextTarget(this, parent), pos),
                OnFolderRenameCommit = (oldPath, newPath) =>
                {
                    if (project.Enums.Folders.Contains(newPath))
                    { Debug.LogWarning($"[Enums] Folder '{newPath}' already exists."); RebuildAll(); return; }
                    Commands.Execute(new RenameFolderCmd<EnumDefinition>(
                        "Enum",
                        project.Enums,
                        oldPath, newPath,
                        onRename: (o, n) => bus.Publish(new EnumFolderRenamedEvent(project.Id, o, n)),
                        onItemPathChanged: (item, oldItemPath, newItemPath) => { }
                    ));
                },
            };

            // ── Drag‑and‑drop ──
            tree.AllowDragDrop = true;

            tree.OnItemMoved = (item, newFolder) =>
            {
                var enumDef = (EnumDefinition)item;
                if (enumDef.FolderPath == newFolder) return;
                Commands.Execute(new MoveItemCmd<EnumDefinition>(
                    "Enum", enumDef, project.Enums,
                    enumDef.FolderPath, newFolder,
                    doPublish: () => { }, undoPublish: () => { }
                ));
                RebuildAll();
            };

            tree.OnFolderMoved = (folderPath, newParent) =>
            {
                string folderName = folderPath.Split('/').Last();
                string newPath = string.IsNullOrEmpty(newParent) ? folderName : newParent + "/" + folderName;
                Commands.Execute(new RenameFolderCmd<EnumDefinition>(
                    "Enum", project.Enums,
                    folderPath, newPath,
                    onRename: (o, n) => { },
                    onItemPathChanged: (item, oldItemPath, newItemPath) => { }
                ));
                RebuildAll();
            };

            return tree;
        }

        // ───────── Entity commands ─────────

        public void AddEntity(string folderPath)
        {
            string baseName = "NewEntity";
            string name = baseName;
            int n = 1;
            while (project.Entities.NameExistsInFolder(folderPath, name)) name = $"{baseName}{++n}";
            var e = new EntityDefinition("ent_" + Guid.NewGuid().ToString("N").Substring(0, 12), name, folderPath ?? "");
            Commands.Execute(new AddItemCmd<EntityDefinition>(
                "Entity",
                project.Entities,
                e,
                doPublish: () => bus.Publish(new EntityAddedEvent(project.Id, e.Id)),
                undoPublish: () => bus.Publish(new EntityRemovedEvent(project.Id, e.Id))
            ));
            selectedEntityId = e.Id;
            focusNameForEntityId = e.Id;
            RebuildAll();
        }

        public void RemoveEntity(string entityId)
        {
            var entity = project.Entities.Items.FirstOrDefault(e => e.Id == entityId);
            if (entity == null) return;

            var msg = $"Delete entity \"{entity.Name}\"?";
            Add(new ModalConfirm(msg, "Delete", () =>
            {
                Commands.Execute(new RemoveItemCmd<EntityDefinition>(
                    "Entity",
                    project.Entities,
                    entityId,
                    doPublish: () => bus.Publish(new EntityRemovedEvent(project.Id, entityId)),
                    undoPublish: () => bus.Publish(new EntityAddedEvent(project.Id, entityId))
                ));
                if (selectedEntityId == entityId) selectedEntityId = null;
                RebuildAll();
            }));
        }

        public void BeginRenameEntity(string entityId)
        {
            selectedEntityId = entityId;
            focusNameForEntityId = entityId;
            RebuildAll();
        }

        private void CommitEntityRename(EntityDefinition e, string newName)
        {
            newName = (newName ?? "").Trim();
            if (string.IsNullOrEmpty(newName) || newName == e.Name) return;
            if (project.Entities.NameExistsInFolder(e.FolderPath, newName, excludeId: e.Id))
            { Debug.LogWarning($"[Entities] Name '{newName}' already exists in folder '{e.FolderPath}'."); return; }
            Commands.Execute(new RenameItemCmd<EntityDefinition>(
                "Entity",
                e,
                e.Name,
                newName,
                doPublish: () => bus.Publish(new EntityRenamedEvent(project.Id, e.Id, e.Name, newName)),
                undoPublish: () => bus.Publish(new EntityRenamedEvent(project.Id, e.Id, newName, e.Name))
            ));
        }

        // ───────── Entity folder commands ─────────

        public void AddEntityFolder(string parent)
        {
            string parentPrefix = string.IsNullOrEmpty(parent) ? "" : parent + "/";
            string baseName = "newFolder";
            string name = baseName;
            int n = 1;
            while (project.Entities.Folders.Contains(parentPrefix + name)) name = $"{baseName}{++n}";
            string full = parentPrefix + name;
            Commands.Execute(new AddFolderCmd<EntityDefinition>(
                "Entity",
                project.Entities,
                full,
                doPublish: () => bus.Publish(new EntityFolderAddedEvent(project.Id, full)),
                undoPublish: () => bus.Publish(new EntityFolderRemovedEvent(project.Id, full))
            ));
            if (!string.IsNullOrEmpty(parent)) entityTree.SetFolderCollapsed(parent, false);
            entityTree.RenamingFolderPath = full;
            entityTree.Rebuild();
        }

        public void RemoveEntityFolder(string folderPath)
        {
            var msg = $"Delete folder \"{folderPath}\" and all its contents?";
            Add(new ModalConfirm(msg, "Delete", () =>
            {
                Commands.Execute(new RemoveFolderCmd<EntityDefinition>(
                    "Entity",
                    project.Entities,
                    folderPath,
                    onItemRemoved: e => bus.Publish(new EntityRemovedEvent(project.Id, e.Id)),
                    onFolderRemoved: f => bus.Publish(new EntityFolderRemovedEvent(project.Id, f)),
                    onItemRestored: e => bus.Publish(new EntityAddedEvent(project.Id, e.Id)),
                    onFolderRestored: f => bus.Publish(new EntityFolderAddedEvent(project.Id, f))
                ));
                if (selectedEntityId != null && project.Entities.Items.All(x => x.Id != selectedEntityId))
                    selectedEntityId = null;
                RebuildAll();
            }));
        }

        public void BeginRenameEntityFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            entityTree.RenamingFolderPath = folderPath;
            entityTree.SetFolderCollapsed(folderPath, false);
            entityTree.Rebuild();
        }

        // ───────── Enum commands ─────────

        public void AddEnum(string folderPath)
        {
            string baseName = "NewEnum";
            string name = baseName;
            int n = 1;
            while (project.Enums.NameExistsInFolder(folderPath, name)) name = $"{baseName}{++n}";
            var e = new EnumDefinition("enum_" + Guid.NewGuid().ToString("N").Substring(0, 12), name, folderPath ?? "");
            e.Members.Add(new EnumMember("m_" + Guid.NewGuid().ToString("N").Substring(0, 8), "Value1"));
            Commands.Execute(new AddItemCmd<EnumDefinition>(
                "Enum",
                project.Enums,
                e,
                doPublish: () => bus.Publish(new EnumAddedEvent(project.Id, e.Id)),
                undoPublish: () => bus.Publish(new EnumRemovedEvent(project.Id, e.Id))
            ));
            selectedEnumId = e.Id;
            focusNameForEnumId = e.Id;
            RebuildAll();
        }

        public void RemoveEnum(string enumId)
        {
            var enumDef = project.Enums.Items.FirstOrDefault(e => e.Id == enumId);
            if (enumDef == null) return;

            var msg = $"Delete enum \"{enumDef.Name}\"?";
            Add(new ModalConfirm(msg, "Delete", () =>
            {
                Commands.Execute(new RemoveItemCmd<EnumDefinition>(
                    "Enum",
                    project.Enums,
                    enumId,
                    doPublish: () => bus.Publish(new EnumRemovedEvent(project.Id, enumId)),
                    undoPublish: () => bus.Publish(new EnumAddedEvent(project.Id, enumId))
                ));
                if (selectedEnumId == enumId) selectedEnumId = null;
                RebuildAll();
            }));
        }

        public void BeginRenameEnum(string enumId)
        {
            selectedEnumId = enumId;
            focusNameForEnumId = enumId;
            RebuildAll();
        }

        private void CommitEnumRename(EnumDefinition e, string newName)
        {
            newName = (newName ?? "").Trim();
            if (string.IsNullOrEmpty(newName) || newName == e.Name) return;
            if (project.Enums.NameExistsInFolder(e.FolderPath, newName, excludeId: e.Id))
            { Debug.LogWarning($"[Enums] Name '{newName}' already exists in folder '{e.FolderPath}'."); return; }
            Commands.Execute(new RenameItemCmd<EnumDefinition>(
                "Enum",
                e,
                e.Name,
                newName,
                doPublish: () => bus.Publish(new EnumRenamedEvent(project.Id, e.Id, e.Name, newName)),
                undoPublish: () => bus.Publish(new EnumRenamedEvent(project.Id, e.Id, newName, e.Name))
            ));
        }

        // ───────── Enum folder commands ─────────

        public void AddEnumFolder(string parent)
        {
            string parentPrefix = string.IsNullOrEmpty(parent) ? "" : parent + "/";
            string baseName = "newFolder";
            string name = baseName;
            int n = 1;
            while (project.Enums.Folders.Contains(parentPrefix + name)) name = $"{baseName}{++n}";
            string full = parentPrefix + name;
            Commands.Execute(new AddFolderCmd<EnumDefinition>(
                "Enum",
                project.Enums,
                full,
                doPublish: () => bus.Publish(new EnumFolderAddedEvent(project.Id, full)),
                undoPublish: () => bus.Publish(new EnumFolderRemovedEvent(project.Id, full))
            ));
            if (!string.IsNullOrEmpty(parent)) enumTree.SetFolderCollapsed(parent, false);
            enumTree.RenamingFolderPath = full;
            enumTree.Rebuild();
        }

        public void RemoveEnumFolder(string folderPath)
        {
            var msg = $"Delete folder \"{folderPath}\" and all its contents?";
            Add(new ModalConfirm(msg, "Delete", () =>
            {
                Commands.Execute(new RemoveFolderCmd<EnumDefinition>(
                    "Enum",
                    project.Enums,
                    folderPath,
                    onItemRemoved: e => bus.Publish(new EnumRemovedEvent(project.Id, e.Id)),
                    onFolderRemoved: f => bus.Publish(new EnumFolderRemovedEvent(project.Id, f)),
                    onItemRestored: e => bus.Publish(new EnumAddedEvent(project.Id, e.Id)),
                    onFolderRestored: f => bus.Publish(new EnumFolderAddedEvent(project.Id, f))
                ));
                if (selectedEnumId != null && project.Enums.Items.All(x => x.Id != selectedEnumId))
                    selectedEnumId = null;
                RebuildAll();
            }));
        }

        public void BeginRenameEnumFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            enumTree.RenamingFolderPath = folderPath;
            enumTree.SetFolderCollapsed(folderPath, false);
            enumTree.Rebuild();
        }

        // ───────── Selection helpers ─────────

        private void SelectEntity(string id, bool toggle)
        {
            if (toggle && selectedEntityId == id) selectedEntityId = null;
            else selectedEntityId = id;
            RebuildAll();
        }

        private void SelectEnum(string id, bool toggle)
        {
            if (toggle && selectedEnumId == id) selectedEnumId = null;
            else selectedEnumId = id;
            RebuildAll();
        }

        // ───────── Row builders ─────────

        private VisualElement BuildSectionHeader(string title, Action onAdd)
        {
            var header = new VisualElement();
            header.AddToClassList("nt-vars-section");
            var lbl = new Label(title);
            lbl.AddToClassList("nt-vars-section-label");
            header.Add(lbl);
            var add = new Button(onAdd) { text = "+" };
            add.AddToClassList("nt-vars-add-btn");
            header.Add(add);
            return header;
        }

        private VisualElement BuildEntityHeader(EntityDefinition e)
        {
            var row = new VisualElement();
            row.AddToClassList("nt-vars-row");

            var swatch = new VisualElement();
            swatch.AddToClassList("nt-vars-swatch");
            swatch.AddToClassList("nt-vars-swatch--entity");
            row.Add(swatch);

            var name = new Label(e.Name);
            name.AddToClassList("nt-vars-name");
            row.Add(name);

            var badge = new Label($"{e.Fields.Count} field{(e.Fields.Count == 1 ? "" : "s")}");
            badge.AddToClassList("nt-vars-type-badge");
            row.Add(badge);
            return row;
        }

        private VisualElement BuildEnumHeader(EnumDefinition e)
        {
            var row = new VisualElement();
            row.AddToClassList("nt-vars-row");

            var swatch = new VisualElement();
            swatch.AddToClassList("nt-vars-swatch");
            swatch.AddToClassList("nt-vars-swatch--enum");
            row.Add(swatch);

            var name = new Label(e.Name);
            name.AddToClassList("nt-vars-name");
            row.Add(name);

            var badge = new Label($"{e.Members.Count} value{(e.Members.Count == 1 ? "" : "s")}");
            badge.AddToClassList("nt-vars-type-badge");
            row.Add(badge);
            return row;
        }

        // ───────── Entity inline editor (unchanged from original) ─────────

        private VisualElement BuildEntityEditor(EntityDefinition e)
        {
            var editor = new VisualElement();
            editor.AddToClassList("nt-vars-editor");

            var nameRow = BuildEditorRow("Name");
            var nameField = new TextField { value = e.Name };
            nameField.AddToClassList("nt-vars-input");
            nameField.RegisterCallback<BlurEvent>(_ => CommitEntityRename(e, nameField.value));
            nameField.RegisterCallback<KeyDownEvent>(ev =>
            {
                if (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter)
                { CommitEntityRename(e, nameField.value); ev.StopPropagation(); }
                else if (ev.keyCode == KeyCode.Escape)
                { nameField.SetValueWithoutNotify(e.Name); Focus(); ev.StopPropagation(); }
            });
            nameRow.Add(nameField);
            editor.Add(nameRow);

            if (focusNameForEntityId == e.Id)
            {
                focusNameForEntityId = null;
                nameField.schedule.Execute(() => { nameField.Focus(); nameField.SelectAll(); }).StartingIn(0);
            }

            var fieldsHdr = new Label("Fields");
            fieldsHdr.AddToClassList("nt-vars-editor-label");
            fieldsHdr.style.marginTop = 6;
            editor.Add(fieldsHdr);

            foreach (var f in e.Fields)
                editor.Add(BuildFieldRow(e, f));

            var addFieldBtn = new Button(() => AddField(e)) { text = "+ Add field" };
            addFieldBtn.AddToClassList("nt-vars-input");
            editor.Add(addFieldBtn);

            return editor;
        }

        private VisualElement BuildFieldRow(EntityDefinition e, EntityField f)
        {
            var box = new VisualElement();
            box.AddToClassList("nt-vars-field-box");

            var topRow = new VisualElement();
            topRow.AddToClassList("nt-vars-field-toprow");
            var fname = new TextField { value = f.Name };
            fname.AddToClassList("nt-vars-input");
            fname.RegisterCallback<BlurEvent>(_ => CommitFieldRename(e, f, fname.value));
            fname.RegisterCallback<KeyDownEvent>(ev =>
            {
                if (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter)
                { CommitFieldRename(e, f, fname.value); ev.StopPropagation(); }
            });
            topRow.Add(fname);
            var rm = new Button(() =>
            {
                var msg = $"Delete field \"{f.Name}\"?";
                Add(new ModalConfirm(msg, "Delete", () =>
                {
                    Commands.Execute(new RemoveEntityFieldCmd(project, bus, e.Id, f.Id));
                }));
            })
            { text = "X" };
            rm.AddToClassList("nt-option-remove-btn");
            topRow.Add(rm);
            box.Add(topRow);

            var typeChoices = Enum.GetNames(typeof(VariableType)).ToList();
            var typeDD = new DropdownField(typeChoices, (int)f.Type);
            typeDD.AddToClassList("nt-vars-input");
            typeDD.RegisterValueChangedCallback(evt =>
            {
                if (!Enum.TryParse<VariableType>(evt.newValue, out var t)) return;
                if (t == f.Type) return;
                Commands.Execute(new SetEntityFieldTypeCmd(project, bus, e.Id, f.Id,
                    f.Type, t, f.DefaultValue, f.EnumTypeId));
            });
            box.Add(typeDD);

            if (f.Type == VariableType.Enum)
                box.Add(BuildFieldEnumPicker(e, f));

            box.Add(BuildFieldDefaultInput(e, f));
            return box;
        }

        private VisualElement BuildFieldEnumPicker(EntityDefinition e, EntityField f)
        {
            var enums = project.Enums.Items.ToList();
            if (enums.Count == 0)
            {
                var msg = new Label("(no enums defined)");
                msg.AddToClassList("nt-vars-input"); return msg;
            }
            var names = enums.Select(en => en.Name).ToList();
            int currentIdx = enums.FindIndex(en => en.Id == f.EnumTypeId);
            var dd = new DropdownField(names, Mathf.Max(0, currentIdx));
            dd.AddToClassList("nt-vars-input");
            dd.RegisterValueChangedCallback(evt =>
            {
                int idx = names.IndexOf(evt.newValue);
                if (idx < 0) return;
                string newId = enums[idx].Id;
                if (newId == f.EnumTypeId) return;
                Commands.Execute(new SetEntityFieldEnumTypeCmd(project, bus, e.Id, f.Id,
                    f.EnumTypeId, newId, f.DefaultValue));
            });
            return dd;
        }

        private VisualElement BuildFieldDefaultInput(EntityDefinition e, EntityField f)
        {
            switch (f.Type)
            {
                case VariableType.Int:
                    {
                        var fld = new IntegerField { value = f.DefaultValue is int i ? i : 0 };
                        fld.AddToClassList("nt-vars-input");
                        fld.RegisterCallback<BlurEvent>(_ => CommitFieldDefault(e, f, fld.value));
                        return fld;
                    }
                case VariableType.Float:
                    {
                        var fld = new FloatField { value = f.DefaultValue is float fv ? fv : 0f };
                        fld.AddToClassList("nt-vars-input");
                        fld.RegisterCallback<BlurEvent>(_ => CommitFieldDefault(e, f, fld.value));
                        return fld;
                    }
                case VariableType.Bool:
                    {
                        var fld = new Toggle { value = f.DefaultValue is bool b && b };
                        fld.AddToClassList("nt-vars-input");
                        fld.RegisterValueChangedCallback(evt => CommitFieldDefault(e, f, evt.newValue));
                        return fld;
                    }
                case VariableType.String:
                    {
                        var fld = new TextField { value = f.DefaultValue as string ?? "" };
                        fld.AddToClassList("nt-vars-input");
                        fld.RegisterCallback<BlurEvent>(_ => CommitFieldDefault(e, f, fld.value));
                        return fld;
                    }
                case VariableType.Enum:
                    {
                        var en = project.Enums.Items.FirstOrDefault(en2 => en2.Id == f.EnumTypeId);
                        if (en == null || en.Members.Count == 0)
                        {
                            var msg = new Label(en == null ? "(pick an enum first)" : "(enum has no members)");
                            msg.AddToClassList("nt-vars-input"); return msg;
                        }
                        var memberNames = en.Members.Select(m => m.Name).ToList();
                        int idx = en.Members.FindIndex(m => m.Id == (f.DefaultValue as string));
                        var dd = new DropdownField(memberNames, Mathf.Max(0, idx));
                        dd.AddToClassList("nt-vars-input");
                        dd.RegisterValueChangedCallback(evt =>
                        {
                            int sel = memberNames.IndexOf(evt.newValue);
                            if (sel < 0) return;
                            CommitFieldDefault(e, f, en.Members[sel].Id);
                        });
                        return dd;
                    }
                default:
                    return new Label("(unsupported)");
            }
        }

        private void AddField(EntityDefinition e)
        {
            string baseName = "field";
            string name = baseName;
            int n = 1;
            while (project.Entities.FieldNameExists(e, name)) name = $"{baseName}{++n}";
            var f = new EntityField("f_" + Guid.NewGuid().ToString("N").Substring(0, 12),
                                    name, VariableType.Int, VariableStore.DefaultFor(VariableType.Int));
            Commands.Execute(new AddEntityFieldCmd(project, bus, e.Id, f));
        }

        private void CommitFieldRename(EntityDefinition e, EntityField f, string newName)
        {
            newName = (newName ?? "").Trim();
            if (string.IsNullOrEmpty(newName) || newName == f.Name) return;
            if (project.Entities.FieldNameExists(e, newName, excludeId: f.Id))
            { Debug.LogWarning($"[Entities] Field name '{newName}' already exists on '{e.Name}'."); return; }
            Commands.Execute(new RenameEntityFieldCmd(project, bus, e.Id, f.Id, f.Name, newName));
        }

        private void CommitFieldDefault(EntityDefinition e, EntityField f, object value)
        {
            if (Equals(f.DefaultValue, value)) return;
            Commands.Execute(new SetEntityFieldDefaultCmd(project, bus, e.Id, f.Id, f.DefaultValue, value));
        }

        // ───────── Enum inline editor (unchanged) ─────────

        private VisualElement BuildEnumEditor(EnumDefinition e)
        {
            var editor = new VisualElement();
            editor.AddToClassList("nt-vars-editor");

            var nameRow = BuildEditorRow("Name");
            var nameField = new TextField { value = e.Name };
            nameField.AddToClassList("nt-vars-input");
            nameField.RegisterCallback<BlurEvent>(_ => CommitEnumRename(e, nameField.value));
            nameField.RegisterCallback<KeyDownEvent>(ev =>
            {
                if (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter)
                { CommitEnumRename(e, nameField.value); ev.StopPropagation(); }
                else if (ev.keyCode == KeyCode.Escape)
                { nameField.SetValueWithoutNotify(e.Name); Focus(); ev.StopPropagation(); }
            });
            nameRow.Add(nameField);
            editor.Add(nameRow);

            if (focusNameForEnumId == e.Id)
            {
                focusNameForEnumId = null;
                nameField.schedule.Execute(() => { nameField.Focus(); nameField.SelectAll(); }).StartingIn(0);
            }

            var membersHdr = new Label("Members");
            membersHdr.AddToClassList("nt-vars-editor-label");
            membersHdr.style.marginTop = 6;
            editor.Add(membersHdr);

            foreach (var m in e.Members)
                editor.Add(BuildMemberRow(e, m));

            var addMemberBtn = new Button(() => AddMember(e)) { text = "+ Add member" };
            addMemberBtn.AddToClassList("nt-vars-input");
            editor.Add(addMemberBtn);

            return editor;
        }

        private VisualElement BuildMemberRow(EnumDefinition e, EnumMember m)
        {
            var row = new VisualElement();
            row.AddToClassList("nt-vars-field-toprow");
            var nameField = new TextField { value = m.Name };
            nameField.AddToClassList("nt-vars-input");
            nameField.RegisterCallback<BlurEvent>(_ => CommitMemberRename(e, m, nameField.value));
            nameField.RegisterCallback<KeyDownEvent>(ev =>
            {
                if (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter)
                { CommitMemberRename(e, m, nameField.value); ev.StopPropagation(); }
            });
            row.Add(nameField);
            var rm = new Button(() =>
            {
                var msg = $"Delete member \"{m.Name}\"?";
                Add(new ModalConfirm(msg, "Delete", () =>
                {
                    Commands.Execute(new RemoveEnumMemberCmd(project, bus, e.Id, m.Id));
                }));
            })
            { text = "X" };
            rm.AddToClassList("nt-option-remove-btn");
            row.Add(rm);
            return row;
        }

        private void AddMember(EnumDefinition e)
        {
            string baseName = "Value";
            string name = baseName + (e.Members.Count + 1);
            int n = 1;
            while (project.Enums.MemberNameExists(e, name)) name = $"{baseName}{e.Members.Count + 1 + n++}";
            var m = new EnumMember("m_" + Guid.NewGuid().ToString("N").Substring(0, 8), name);
            Commands.Execute(new AddEnumMemberCmd(project, bus, e.Id, m));
        }

        private void CommitMemberRename(EnumDefinition e, EnumMember m, string newName)
        {
            newName = (newName ?? "").Trim();
            if (string.IsNullOrEmpty(newName) || newName == m.Name) return;
            if (project.Enums.MemberNameExists(e, newName, excludeId: m.Id))
            { Debug.LogWarning($"[Enums] Member '{newName}' already exists on '{e.Name}'."); return; }
            Commands.Execute(new RenameEnumMemberCmd(project, bus, e.Id, m.Id, m.Name, newName));
        }

        private static VisualElement BuildEditorRow(string label)
        {
            var row = new VisualElement();
            row.AddToClassList("nt-vars-editor-row");
            var lbl = new Label(label);
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
        }
    }

    // ───────── Context menu targets ─────────

    public sealed class EntityContextTarget
    {
        public EntitiesPanel Panel { get; }
        public EntityDefinition Entity { get; }
        public EntityContextTarget(EntitiesPanel p, EntityDefinition e) { Panel = p; Entity = e; }
    }

    public sealed class EntityFolderContextTarget
    {
        public EntitiesPanel Panel { get; }
        public string FolderPath { get; }
        public EntityFolderContextTarget(EntitiesPanel p, string f) { Panel = p; FolderPath = f ?? ""; }
    }

    public sealed class EnumContextTarget
    {
        public EntitiesPanel Panel { get; }
        public EnumDefinition Enum { get; }
        public EnumContextTarget(EntitiesPanel p, EnumDefinition e) { Panel = p; Enum = e; }
    }

    public sealed class EnumFolderContextTarget
    {
        public EntitiesPanel Panel { get; }
        public string FolderPath { get; }
        public EnumFolderContextTarget(EntitiesPanel p, string f) { Panel = p; FolderPath = f ?? ""; }
    }
}