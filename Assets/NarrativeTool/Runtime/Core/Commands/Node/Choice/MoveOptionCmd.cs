using NarrativeTool.Data.Graph.Nodes;

namespace NarrativeTool.Core.Commands
{
    public class MoveOptionCmd : ICommand
    {
        private readonly ChoiceNodeData node;
        private readonly int indexA, indexB;   // always adjacent

        public string Name => "Move option";

        public MoveOptionCmd(ChoiceNodeData node, int indexA, int indexB)
        {
            this.node = node;
            this.indexA = indexA;
            this.indexB = indexB;
        }

        public void Do() => Move(indexB, indexA);
        public void Undo() => Move(indexA, indexB);

        private void Move(int from, int to)
        {
            var opt = node.Options[from];
            node.Options.RemoveAt(from);
            node.Options.Insert(to, opt);

            var portIdx = node.Outputs.FindIndex(p => p.Id == opt.PortId);
            if (portIdx >= 0)
            {
                var port = node.Outputs[portIdx];
                node.Outputs.RemoveAt(portIdx);
                node.Outputs.Insert(to, port);
            }
        }

        public bool TryMerge(ICommand previous) => false;
    }
}