using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using System.Linq;
using UnityEngine;

namespace NarrativeTool.Core.Runtime.Executors
{
    [NodeExecutorOf(typeof(JumperInNodeData))]
    public class JumperInExecutor : INodeExecutor
    {
        public ExecutionResult Execute(NodeData node, RuntimeContext context)
        {
            var jumperIn = (JumperInNodeData)node;
            if (string.IsNullOrEmpty(jumperIn.TargetOutNodeId))
            {
                Debug.LogError($"[JumperIn] Node '{node.Id}' has no target Out jumper.");
                return new ExecutionResult();
            }

            // Find the Out node in the same graph (for cross graph, we'd need graph scoped lookup)
            var outNode = context.CurrentGraph.Nodes
                .OfType<JumperOutNodeData>()
                .FirstOrDefault(n => n.Id == jumperIn.TargetOutNodeId);

            if (outNode == null)
            {
                Debug.LogError($"[JumperIn] Target Out node '{jumperIn.TargetOutNodeId}' not found.");
                return new ExecutionResult();
            }

            // Teleport: continue from the Out node's output port
            return ExecutionResult.Continue(JumperOutNodeData.OutputPortId);
        }
    }
}