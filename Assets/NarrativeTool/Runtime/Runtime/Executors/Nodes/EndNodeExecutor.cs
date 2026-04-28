using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;

namespace NarrativeTool.Core.Runtime.Executors
{
    [NodeExecutorOf(typeof(EndNodeData))]
    public class EndNodeExecutor : INodeExecutor
    {
        public ExecutionResult Execute(NodeData node, RuntimeContext context) => new ExecutionResult(); // null NextPortId signals end
    }
}