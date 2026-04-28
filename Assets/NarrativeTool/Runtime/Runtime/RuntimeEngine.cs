using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using System.Linq;
using UnityEngine;

namespace NarrativeTool.Core.Runtime
{
    public enum RuntimeState { Idle, Running, Paused, Done }

    public class RuntimeEngine
    {
        private RuntimeContext context;
        private NodeExecutorRegistry executorRegistry;

        public RuntimeState State { get; private set; } = RuntimeState.Idle;

        public RuntimeEngine(RuntimeContext context, NodeExecutorRegistry executorRegistry)
        {
            this.context = context;
            this.executorRegistry = executorRegistry;
        }

        /// <summary>
        /// Start execution from the given graph's Start node.
        /// If <paramref name="graphId"/> is null, resumes from the current state
        /// (used after an interaction is resolved).
        /// </summary>
        public void Start(string graphId)
        {
            if (State != RuntimeState.Idle && State != RuntimeState.Done)
            {
                Debug.LogWarning("[RuntimeEngine] Already running. Call Stop() first.");
                return;
            }

            if (!string.IsNullOrEmpty(graphId))
            {
                var graph = context.GraphLoader.GetGraph(graphId);
                if (graph == null)
                {
                    Debug.LogError($"[RuntimeEngine] Graph '{graphId}' not found.");
                    State = RuntimeState.Done;
                    return;
                }
                context.CurrentGraph = graph;
                var startNode = graph.Nodes.FirstOrDefault(n => n is StartNodeData);
                if (startNode == null)
                {
                    Debug.LogError($"[RuntimeEngine] No Start node in graph '{graphId}'.");
                    State = RuntimeState.Done;
                    return;
                }
                context.CurrentNode = startNode;
            }

            State = RuntimeState.Running;
            PublishStateChange();
            Step();
        }

        public void Step()
        {
            if (State != RuntimeState.Running) return;

            var node = context.CurrentNode;
            context.EventBus.Publish(new NodeEnteredEvent(context.CurrentGraph.Id, node.Id));

            var executor = executorRegistry.Get(node.TypeId);
            if (executor == null)
            {
                Debug.LogError($"[RuntimeEngine] No executor for TypeId '{node.TypeId}'. Stopping.");
                State = RuntimeState.Done;
                PublishStateChange();
                return;
            }

            ExecutionResult result;
            try
            {
                result = executor.Execute(node, context);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                State = RuntimeState.Done;
                PublishStateChange();
                return;
            }

            // Handle interaction request
            if (result.Interaction != null)
            {
                result.Interaction.Configure(context.Interaction, context.EventBus, node.Id);
                State = RuntimeState.Paused;
                PublishStateChange();
                return;
            }

            // Continue to next node
            ContinueExecution(result.NextPortId);
        }

        /// <summary>
        /// Called after an interaction has been resolved.
        /// The <see cref="InteractionContext.UserData"/> should contain the port ID
        /// to follow (set by the executor's callback).
        /// </summary>
        public void ContinueAfterInteraction()
        {
            if (State != RuntimeState.Paused) return;
            if (context.Interaction.IsPending)
            {
                Debug.LogWarning("[RuntimeEngine] Cannot continue while interaction is still pending.");
                return;
            }

            string nextPortId = context.Interaction.UserData as string;
            context.Interaction.UserData = null;
            State = RuntimeState.Running;
            PublishStateChange();
            ContinueExecution(nextPortId);
        }

        private void ContinueExecution(string nextPortId)
        {
            Debug.Log($"[RuntimeEngine] ContinueExecution from port '{nextPortId}' on node '{context.CurrentNode.Id}'.");
            if (string.IsNullOrEmpty(nextPortId))
            {
                Debug.Log("[RuntimeEngine] No port given, treating as end of graph.");
                HandleEndOfGraph();
                return;
            }

            var nextNode = FollowEdge(context.CurrentNode, nextPortId);
            if (nextNode == null)
            {
                Debug.LogWarning($"[RuntimeEngine] No edge found from port '{nextPortId}' on node '{context.CurrentNode.Id}'. Ending graph.");
                HandleEndOfGraph();
                return;
            }

            Debug.Log($"[RuntimeEngine] Moving to node '{nextNode.Id}' ({nextNode.GetType().Name}).");
            context.CurrentNode = nextNode;
            Step();
        }

        /// <summary>
        /// Called when a graph has no more nodes to execute.
        /// Pops the call stack; if a parent graph exists, resumes there.
        /// Otherwise the entire run is finished.
        /// </summary>
        private void HandleEndOfGraph()
        {
            if (context.CallStack.Count > 0)
            {
                var frame = context.CallStack.Pop();
                Debug.Log($"[RuntimeEngine] Popping call stack to graph '{frame.ReturnGraph.Id}', node '{frame.ReturnNodeId}'.");
                context.CurrentGraph = frame.ReturnGraph;
                var nextNode = FollowEdge(
                    context.CurrentGraph.FindNode(frame.ReturnNodeId),
                    frame.ReturnPortId);
                if (nextNode == null)
                {
                    Debug.LogWarning($"[RuntimeEngine] No edge from return node '{frame.ReturnNodeId}' port '{frame.ReturnPortId}'. Ending graph again.");
                    HandleEndOfGraph();
                    return;
                }
                context.CurrentNode = nextNode;
                Step();
            }
            else
            {
                Debug.Log("[RuntimeEngine] Graph execution finished.");
                State = RuntimeState.Done;
                PublishStateChange();
            }
        }

        public void Stop()
        {
            State = RuntimeState.Idle;
            context.Interaction.Clear();
            PublishStateChange();
        }

        private NodeData FollowEdge(NodeData fromNode, string fromPortId)
        {
            if (string.IsNullOrEmpty(fromPortId)) return null;
            var edge = context.CurrentGraph.Edges.FirstOrDefault(e =>
                e.FromNodeId == fromNode.Id && e.FromPortId == fromPortId);
            if (edge == null) return null;
            return context.CurrentGraph.Nodes.FirstOrDefault(n => n.Id == edge.ToNodeId);
        }

        private void PublishStateChange()
        {
            context.EventBus.Publish(new RuntimeStateChanged(State));
        }

        public RuntimeContext GetContext() => context;
    }
}