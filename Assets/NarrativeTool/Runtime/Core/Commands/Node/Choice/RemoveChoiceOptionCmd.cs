using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using System;

namespace NarrativeTool.Core.Commands
{
    public sealed class RemoveChoiceOptionCmd : ICommand
    {
        private readonly ChoiceNodeData node;
        private readonly ChoiceOption option;
        private readonly PortData port;
        private readonly int index;
        private readonly Action onDo, onUndo;

        public string Name => "Remove choice option";

        public RemoveChoiceOptionCmd(ChoiceNodeData node, int index, ChoiceOption option,
                                     Action onDo, Action onUndo)
        {
            this.node = node;
            this.index = index;
            this.option = option;
            this.port = node.FindPort(option.PortId);
            this.onDo = onDo;
            this.onUndo = onUndo;
        }

        public void Do()
        {
            node.Options.RemoveAt(index);
            if (port != null) node.Outputs.Remove(port);
            onDo?.Invoke();
        }

        public void Undo()
        {
            node.Options.Insert(index, option);
            if (port != null) node.Outputs.Insert(index, port);
            onUndo?.Invoke();
        }

        public bool TryMerge(ICommand previous) => false;
    }
}