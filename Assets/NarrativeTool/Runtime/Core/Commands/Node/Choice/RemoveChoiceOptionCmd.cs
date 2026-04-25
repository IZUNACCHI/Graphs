// AddChoiceOptionCmd.cs
using NarrativeTool.Data.Graph.Nodes;
using System;

namespace NarrativeTool.Core.Commands
{
    // RemoveChoiceOptionCmd.cs
    public sealed class RemoveChoiceOptionCmd : ICommand
    {
        private readonly ChoiceNodeData node;
        private readonly ChoiceOption option;
        private readonly int index;
        private readonly Action onDo, onUndo;

        public string Name => "Remove choice option";

        public RemoveChoiceOptionCmd(ChoiceNodeData node, int index, ChoiceOption option,
                                     Action onDo, Action onUndo)
        {
            this.node = node;
            this.index = index;
            this.option = option;
            this.onDo = onDo;
            this.onUndo = onUndo;
        }

        public void Do()
        {
            node.Options.RemoveAt(index);
            onDo?.Invoke();
        }

        public void Undo()
        {
            node.Options.Insert(index, option);
            onUndo?.Invoke();
        }

        public bool TryMerge(ICommand previous) => false;
    }
}