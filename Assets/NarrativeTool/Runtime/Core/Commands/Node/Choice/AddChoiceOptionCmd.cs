using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using System;

namespace NarrativeTool.Core.Commands
{
    public sealed class AddChoiceOptionCmd : ICommand
    {
        private readonly ChoiceNodeData node;
        private readonly ChoiceOption option;
        private readonly PortData port;
        private readonly int index;
        private readonly Action onDo, onUndo;

        public string Name => "Add choice option";

        public AddChoiceOptionCmd(ChoiceNodeData node, int index, ChoiceOption option,
                                  Action onDo, Action onUndo)
        {
            this.node = node;
            this.index = index;
            this.option = option;
            this.port = new PortData(option.PortId, option.Label,
                                     PortDirection.Output, PortCapacity.Single, "flow");
            this.onDo = onDo;
            this.onUndo = onUndo;
        }

        public void Do()
        {
            node.Options.Insert(index, option);
            node.Outputs.Insert(index, port);
            onDo?.Invoke();
        }

        public void Undo()
        {
            node.Options.RemoveAt(index);
            node.Outputs.Remove(port);
            onUndo?.Invoke();
        }

        public bool TryMerge(ICommand previous) => false;
    }
}