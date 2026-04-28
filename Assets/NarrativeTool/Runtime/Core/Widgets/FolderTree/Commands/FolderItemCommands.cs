// Core/Commands/Generic/FolderItemCommands.cs
using NarrativeTool.Data;
using NarrativeTool.Data.Project;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NarrativeTool.Core.Commands.Generic
{
    // ── Add item ──
    public class AddItemCmd<T> : ICommand where T : class, IFolderableItem
    {
        public string Name { get; }
        private readonly FolderTreeStore<T> store;
        private readonly T item;
        private readonly Action doPublish, undoPublish;

        public AddItemCmd(string label, FolderTreeStore<T> store, T item,
                          Action doPublish, Action undoPublish)
        {
            Name = $"Add {label}";
            this.store = store;
            this.item = item;
            this.doPublish = doPublish;
            this.undoPublish = undoPublish;
        }

        public void Do()
        {
            store.Items.Add(item);
            doPublish?.Invoke();
        }

        public void Undo()
        {
            store.Items.Remove(item);
            undoPublish?.Invoke();
        }

        public bool TryMerge(ICommand previous) => false;
    }

    // ── Remove item ──
    public class RemoveItemCmd<T> : ICommand where T : class, IFolderableItem
    {
        public string Name { get; }
        private readonly FolderTreeStore<T> store;
        private readonly string itemId;
        private T removedItem;
        private readonly Action doPublish, undoPublish;

        public RemoveItemCmd(string label, FolderTreeStore<T> store, string itemId,
                             Action doPublish, Action undoPublish)
        {
            Name = $"Remove {label}";
            this.store = store;
            this.itemId = itemId;
            this.doPublish = doPublish;
            this.undoPublish = undoPublish;
        }

        public void Do()
        {
            removedItem = store.Find(itemId);
            if (removedItem != null)
            {
                store.Items.Remove(removedItem);
                doPublish?.Invoke();
            }
        }

        public void Undo()
        {
            if (removedItem != null)
            {
                store.Items.Add(removedItem);
                undoPublish?.Invoke();
            }
        }

        public bool TryMerge(ICommand previous) => false;
    }

    // ── Rename item (using INamedItem) ──
    public class RenameItemCmd<T> : ICommand where T : class, IFolderableItem, INamedItem
    {
        public string Name { get; }
        private readonly T item;
        private readonly string oldName, newName;
        private readonly Action doPublish, undoPublish;

        public RenameItemCmd(string label, T item, string oldName, string newName,
                             Action doPublish, Action undoPublish)
        {
            Name = $"Rename {label}";
            this.item = item;
            this.oldName = oldName;
            this.newName = newName;
            this.doPublish = doPublish;
            this.undoPublish = undoPublish;
        }

        public void Do()
        {
            item.Name = newName;
            doPublish?.Invoke();
        }

        public void Undo()
        {
            item.Name = oldName;
            undoPublish?.Invoke();
        }

        public bool TryMerge(ICommand previous) => false;
    }

    // ── Add folder ──
    public class AddFolderCmd<T> : ICommand where T : class, IFolderableItem
    {
        public string Name { get; }
        private readonly FolderTreeStore<T> store;
        private readonly string folderPath;
        private readonly Action doPublish, undoPublish;

        public AddFolderCmd(string label, FolderTreeStore<T> store, string folderPath,
                            Action doPublish, Action undoPublish)
        {
            Name = $"Add {label} Folder";
            this.store = store;
            this.folderPath = folderPath;
            this.doPublish = doPublish;
            this.undoPublish = undoPublish;
        }

        public void Do()
        {
            if (!store.Folders.Contains(folderPath))
            {
                store.Folders.Add(folderPath);
                doPublish?.Invoke();
            }
        }

        public void Undo()
        {
            store.Folders.Remove(folderPath);
            undoPublish?.Invoke();
        }

        public bool TryMerge(ICommand previous) => false;
    }

    // ── Remove folder (cascade) ──
    public class RemoveFolderCmd<T> : ICommand where T : class, IFolderableItem
    {
        public string Name { get; }
        private readonly FolderTreeStore<T> store;
        private readonly string folderPath;
        private readonly Action<T> onItemRemoved;      // e.g., publish per item
        private readonly Action<string> onFolderRemoved;
        private readonly Action<T> onItemRestored;
        private readonly Action<string> onFolderRestored;

        private List<T> removedItems;
        private List<string> removedFolders;

        public RemoveFolderCmd(string label, FolderTreeStore<T> store, string folderPath,
                               Action<T> onItemRemoved, Action<string> onFolderRemoved,
                               Action<T> onItemRestored, Action<string> onFolderRestored)
        {
            Name = $"Remove {label} Folder";
            this.store = store;
            this.folderPath = folderPath;
            this.onItemRemoved = onItemRemoved;
            this.onFolderRemoved = onFolderRemoved;
            this.onItemRestored = onItemRestored;
            this.onFolderRestored = onFolderRestored;
        }

        public void Do()
        {
            string prefix = folderPath + "/";
            removedItems = store.Items
                .Where(i => i.FolderPath == folderPath || (i.FolderPath?.StartsWith(prefix) ?? false))
                .ToList();
            removedFolders = store.Folders
                .Where(f => f != null && (f == folderPath || f.StartsWith(prefix)))
                .OrderByDescending(f => f.Length)
                .ToList();

            foreach (var item in removedItems)
            {
                store.Items.Remove(item);
                onItemRemoved?.Invoke(item);
            }
            foreach (var folder in removedFolders)
            {
                store.Folders.Remove(folder);
                onFolderRemoved?.Invoke(folder);
            }
        }

        public void Undo()
        {
            // Restore folders first (shallow first)
            foreach (var folder in removedFolders.OrderBy(f => f.Length))
            {
                if (!store.Folders.Contains(folder))
                {
                    store.Folders.Add(folder);
                    onFolderRestored?.Invoke(folder);
                }
            }
            foreach (var item in removedItems)
            {
                store.Items.Add(item);
                onItemRestored?.Invoke(item);
            }
        }

        public bool TryMerge(ICommand previous) => false;
    }

    // ── Rename folder ──
    public class RenameFolderCmd<T> : ICommand where T : class, IFolderableItem
    {
        public string Name { get; }
        private readonly FolderTreeStore<T> store;
        private readonly string oldPath, newPath;
        private readonly Action<string, string> onRename; // (old, new)
        private readonly Action<T, string, string> onItemPathChanged; // (item, old, new)

        private List<(T item, string oldFolder)> changedItems;
        private List<(string oldChild, string newChild)> renamedFolders;

        public RenameFolderCmd(string label, FolderTreeStore<T> store, string oldPath, string newPath,
                               Action<string, string> onRename,
                               Action<T, string, string> onItemPathChanged)
        {
            Name = $"Rename {label} Folder";
            this.store = store;
            this.oldPath = oldPath;
            this.newPath = newPath;
            this.onRename = onRename;
            this.onItemPathChanged = onItemPathChanged;
        }

        public void Do()
        {
            store.Folders.Remove(oldPath);
            store.Folders.Add(newPath);
            onRename?.Invoke(oldPath, newPath);

            string oldPrefix = oldPath + "/";
            renamedFolders = new List<(string, string)>();
            foreach (var f in store.Folders.Where(f => f.StartsWith(oldPrefix)).ToList())
            {
                string newChild = newPath + f.Substring(oldPath.Length);
                store.Folders.Remove(f);
                store.Folders.Add(newChild);
                renamedFolders.Add((f, newChild));
                onRename?.Invoke(f, newChild);
            }

            changedItems = new List<(T, string)>();
            foreach (var item in store.Items.Where(i => i.FolderPath == oldPath ||
                                                        (i.FolderPath?.StartsWith(oldPrefix) ?? false)).ToList())
            {
                string oldItemPath = item.FolderPath;
                if (item.FolderPath == oldPath)
                    item.FolderPath = newPath;
                else
                    item.FolderPath = newPath + item.FolderPath.Substring(oldPath.Length);
                changedItems.Add((item, oldItemPath));
                onItemPathChanged?.Invoke(item, oldItemPath, item.FolderPath);
            }
        }

        public void Undo()
        {
            // Reverse item folder changes
            foreach (var (item, oldFolder) in changedItems)
                item.FolderPath = oldFolder;

            // Reverse child folder renames
            foreach (var (oldChild, newChild) in renamedFolders)
            {
                store.Folders.Remove(newChild);
                store.Folders.Add(oldChild);
                onRename?.Invoke(newChild, oldChild);
            }

            store.Folders.Remove(newPath);
            store.Folders.Add(oldPath);
            onRename?.Invoke(newPath, oldPath);
        }

        public bool TryMerge(ICommand previous) => false;
    }
}