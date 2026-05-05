using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using NarrativeTool.Runtime;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NarrativeTool.Core.Runtime
{
    public enum RuntimeState { Idle, Running, Paused, Done }

    public class RuntimeEngine
    {
        private const int MaxStepHistory = 200;

        private RuntimeContext context;
        private NodeExecutorRegistry executorRegistry;
        private BreakpointStore breakpoints;
        private RuntimeVariableStore variableStore;
        private RuntimeEntityStore entityStore;

        // Set when execution was paused by a breakpoint, so the next Step()
        // doesn't pause on the same node forever.
        private bool skipBreakpointOnce;

        // When > 0, the engine pauses after executing this many more node
        // bodies (used by manual single-step). 0 = run normally.
        private int stepBudget = -1;  // -1 disabled, 0 = "pause now", >0 = "execute this many more"

        // Stack of state snapshots taken right before each node executed.
        // Bounded; oldest entries are dropped when MaxStepHistory exceeded.
        private readonly LinkedList<StepSnapshot> history = new();

        public RuntimeState State { get; private set; } = RuntimeState.Idle;

        public bool CanUndoStep => history.Count > 0;

        public RuntimeEngine(RuntimeContext context, NodeExecutorRegistry executorRegistry,
            BreakpointStore breakpoints = null)
        {
            this.context = context;
            this.executorRegistry = executorRegistry;
            this.breakpoints = breakpoints;
            this.variableStore = context?.Variables as RuntimeVariableStore;
            this.entityStore = context?.Entities as RuntimeEntityStore;
        }

        private sealed class StepSnapshot
        {
            public GraphData Graph;
            public NodeData Node;
            public Dictionary<string, object> Variables;
            public Dictionary<string, Dictionary<string, object>> Entities;
            public CallFrame[] CallStackBottomToTop;
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

            history.Clear();
            stepBudget = -1;
            State = RuntimeState.Running;
            PublishStateChange();
            Step();
        }

        public void Step()
        {
            if (State != RuntimeState.Running) return;

            var node = context.CurrentNode;

            // Pause on enabled breakpoint, unless we just resumed from one.
            if (!skipBreakpointOnce
                && breakpoints != null
                && breakpoints.IsActive(context.CurrentGraph.Id, node.Id))
            {
                State = RuntimeState.Paused;
                context.EventBus.Publish(new BreakpointHitEvent(context.CurrentGraph.Id, node.Id));
                PublishStateChange();
                return;
            }
            skipBreakpointOnce = false;

            // Single-step budget: 0 means "pause before executing the next node".
            if (stepBudget == 0)
            {
                stepBudget = -1;          // step consumed
                State = RuntimeState.Paused;
                skipBreakpointOnce = true; // so resume past this node doesn't re-fire its BP
                PublishStateChange();
                return;
            }
            if (stepBudget > 0) stepBudget--;

            // Snapshot state so the user can undo this step later.
            CaptureSnapshot();

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
        /// Resumes execution from a breakpoint pause. Re-runs the current node
        /// (which was the one that triggered the breakpoint) without pausing
        /// on it again.
        /// </summary>
        public void ResumeFromBreakpoint()
        {
            if (State != RuntimeState.Paused) return;
            if (context.Interaction.IsPending) return;  // not a breakpoint pause
            skipBreakpointOnce = true;
            stepBudget = -1;
            State = RuntimeState.Running;
            PublishStateChange();
            Step();
        }

        /// <summary>
        /// Manual single-step. When paused (e.g. on a breakpoint), executes
        /// exactly one node body and pauses again at the next node. Useful
        /// for walking through a graph one node at a time.
        /// </summary>
        public void StepOne()
        {
            if (State != RuntimeState.Paused) return;
            if (context.Interaction.IsPending) return;
            skipBreakpointOnce = true;
            stepBudget = 1;
            State = RuntimeState.Running;
            PublishStateChange();
            Step();
        }

        /// <summary>
        /// Rewinds one step: pops the last snapshot and restores variable /
        /// entity values, current node, current graph, and call stack. The
        /// engine is left in the Paused state so the user can step forward
        /// again. Does nothing if there is no history.
        /// </summary>
        public void UndoLastStep()
        {
            if (history.Count == 0) return;

            var snap = history.Last.Value;
            history.RemoveLast();

            // Restore values (publishes change events so UI reflects the rewind)
            variableStore?.RestoreValues(snap.Variables);
            entityStore?.RestoreValues(snap.Entities);

            // Restore graph location & call stack
            context.CurrentGraph = snap.Graph;
            context.CurrentNode = snap.Node;
            context.CallStack.Clear();
            if (snap.CallStackBottomToTop != null)
                foreach (var frame in snap.CallStackBottomToTop)
                    context.CallStack.Push(frame);

            // Land in Paused with a guard so the immediate next step doesn't
            // re-trigger a breakpoint on this node.
            skipBreakpointOnce = true;
            stepBudget = -1;
            State = RuntimeState.Paused;
            PublishStateChange();
        }

        private void CaptureSnapshot()
        {
            var snap = new StepSnapshot
            {
                Graph = context.CurrentGraph,
                Node = context.CurrentNode,
                Variables = variableStore?.SnapshotValues(),
                Entities = entityStore?.SnapshotValues(),
                CallStackBottomToTop = context.CallStack.Count == 0
                    ? null
                    : context.CallStack.Reverse().ToArray(),
            };
            history.AddLast(snap);
            while (history.Count > MaxStepHistory) history.RemoveFirst();
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
            history.Clear();
            stepBudget = -1;
            skipBreakpointOnce = false;
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