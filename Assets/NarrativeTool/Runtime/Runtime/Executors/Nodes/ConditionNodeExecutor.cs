using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;

namespace NarrativeTool.Core.Runtime.Executors
{
    [NodeExecutorOf(typeof(ConditionNodeData))]
    public class ConditionNodeExecutor : INodeExecutor
    {
        public ExecutionResult Execute(NodeData node, RuntimeContext context)
        {
            var cond = (ConditionNodeData)node;
            bool result = context.EvaluateCondition(cond.ConditionScript);
            string portId = result ? ConditionNodeData.TruePortId : ConditionNodeData.FalsePortId;
            return ExecutionResult.Continue(portId);
        }
    }
}
