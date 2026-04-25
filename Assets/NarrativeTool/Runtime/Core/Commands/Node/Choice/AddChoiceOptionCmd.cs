// AddChoiceOptionCmd.cs
using NarrativeTool.Data.Graph.Nodes;
using System;

namespace NarrativeTool.Core.Commands
{
    public sealed class AddChoiceOptionCmd : ICommand
    {
        private readonly ChoiceNodeData node;
        private readonly ChoiceOption option;
        private readonly int index;
        private readonly Action onDo, onUndo;

        public string Name => "Add choice option";

        public AddChoiceOptionCmd(ChoiceNodeData node, int index, ChoiceOption option,
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
            node.Options.Insert(index, option);
            // add output port (handled by view)
            onDo?.Invoke();
        }

        public void Undo()
        {
            node.Options.RemoveAt(index);
            onUndo?.Invoke();
        }

        public bool TryMerge(ICommand previous) => false;
    }
}