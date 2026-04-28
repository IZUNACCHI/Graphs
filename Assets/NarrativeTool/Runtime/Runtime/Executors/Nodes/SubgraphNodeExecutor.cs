using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using NarrativeTool.Runtime;
using System.Linq;
using UnityEngine;

namespace NarrativeTool.Core.Runtime.Executors
{
    [NodeExecutorOf(typeof(SubgraphNodeData))]
    public class SubgraphNodeExecutor : INodeExecutor
    {
        public ExecutionResult Execute(NodeData node, RuntimeContext context)
        {
            var sub = (SubgraphNodeData)node;

            if (string.IsNullOrEmpty(sub.ReferencedGraphId))
            {
                Debug.LogError($"[SubgraphExecutor] Node '{node.Id}' has no referenced graph.");
                return new ExecutionResult(); // end of graph (graceful)
            }

            // Load the subgraph
            var subGraph = context.GraphLoader.GetGraph(sub.ReferencedGraphId);
            if (subGraph == null)
            {
                Debug.LogError($"[SubgraphExecutor] Graph '{sub.ReferencedGraphId}' not found.");
                return new ExecutionResult();
            }

            var startNode = subGraph.Nodes.FirstOrDefault(n => n is StartNodeData);
            if (startNode == null)
            {
                Debug.LogError($"[SubgraphExecutor] No Start node in subgraph '{sub.ReferencedGraphId}'.");
                return new ExecutionResult();
            }

            // Push call frame to return here after the subgraph finishes
            context.CallStack.Push(new CallFrame(
                context.CurrentGraph,      // return graph
                sub.Id,                    // return node (this SubgraphNode)
                SubgraphNodeData.OutputPortId));

            // Switch to subgraph
            context.CurrentGraph = subGraph;
            context.CurrentNode = startNode;

            // Publish node entered for the start node? The engine will do it on the next Step.
            // The engine will also publish NodeEnteredEvent for the subgraph's start node.
            // We just return the start node's output port so the engine continues from there.
            return ExecutionResult.Continue(StartNodeData.OutputPortId);
        }
    }
}