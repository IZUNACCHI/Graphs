using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;

namespace NarrativeTool.Core.Runtime.Executors
{
    [NodeExecutorOf(typeof(StartNodeData))]
    public class StartNodeExecutor : INodeExecutor
    {
        public ExecutionResult Execute(NodeData node, RuntimeContext context)
        {
            return ExecutionResult.Continue(StartNodeData.OutputPortId);
        }
    }
}