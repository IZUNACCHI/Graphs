using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;

namespace NarrativeTool.Core.Runtime.Executors
{
    [NodeExecutorOf(typeof(ScriptNodeData))]
    public class ScriptNodeExecutor : INodeExecutor
    {
        public ExecutionResult Execute(NodeData node, RuntimeContext context)
        {
            var scriptNode = (ScriptNodeData)node;
            if (!string.IsNullOrEmpty(scriptNode.Script))
                context.Scripting.Evaluate(scriptNode.Script, out _);

            return ExecutionResult.Continue(ScriptNodeData.OutputPortId);
        }
    }
}