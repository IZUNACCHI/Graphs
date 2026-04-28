using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;

namespace NarrativeTool.Core.Runtime.Executors
{
    [NodeExecutorOf(typeof(JumperOutNodeData))]
    public class JumperOutExecutor : INodeExecutor
    {
        public ExecutionResult Execute(NodeData node, RuntimeContext context)
        {
            // When execution arrives at an Out jumper (via teleport), just continue
            return ExecutionResult.Continue(JumperOutNodeData.OutputPortId);
        }
    }
}