using NarrativeTool.Core.EventSystem;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;

namespace NarrativeTool.Core.Runtime.Executors
{
    [NodeExecutorOf(typeof(DialogNodeData))]
    public class DialogNodeExecutor : INodeExecutor
    {
        public ExecutionResult Execute(NodeData node, RuntimeContext context)
        {
            var dlg = (DialogNodeData)node;
            // Publish the dialogue line immediately
            context.EventBus.Publish(new DialogueLineEvent(dlg.Id, dlg.Speaker, dlg.Dialogue, dlg.StageDirections));

            // Request a continue interaction
            var interaction = new ContinueInteraction("Click to continue", DialogNodeData.OutputPortId);
            return ExecutionResult.Pause(interaction);
        }
    }
}