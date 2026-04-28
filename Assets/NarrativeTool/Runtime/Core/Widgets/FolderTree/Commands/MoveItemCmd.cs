using NarrativeTool.Data;
using NarrativeTool.Data.Project;
using System;

namespace NarrativeTool.Core.Commands.Generic
{
    public class MoveItemCmd<T> : ICommand where T : class, IFolderableItem
    {
        public string Name { get; }
        private readonly T item;
        private readonly FolderTreeStore<T> store;
        private readonly string oldFolder, newFolder;
        private readonly Action doPublish, undoPublish;

        public MoveItemCmd(string label, T item, FolderTreeStore<T> store,
                           string oldFolder, string newFolder,
                           Action doPublish, Action undoPublish)
        {
            Name = $"Move {label}";
            this.item = item;
            this.store = store;
            this.oldFolder = oldFolder;
            this.newFolder = newFolder;
            this.doPublish = doPublish;
            this.undoPublish = undoPublish;
        }

        public void Do()
        {
            item.FolderPath = newFolder;
            doPublish?.Invoke();
        }

        public void Undo()
        {
            item.FolderPath = oldFolder;
            undoPublish?.Invoke();
        }

        public bool TryMerge(ICommand previous) => false;
    }
}