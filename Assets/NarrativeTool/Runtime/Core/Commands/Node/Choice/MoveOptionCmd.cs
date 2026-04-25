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

        public void Do()
        {
            var opt = node.Options[indexB];
            node.Options.RemoveAt(indexB);
            node.Options.Insert(indexA, opt);
        }

        public void Undo()
        {
            var opt = node.Options[indexA];
            node.Options.RemoveAt(indexA);
            node.Options.Insert(indexB, opt);
        }

        public bool TryMerge(ICommand previous) => false;
    }
}