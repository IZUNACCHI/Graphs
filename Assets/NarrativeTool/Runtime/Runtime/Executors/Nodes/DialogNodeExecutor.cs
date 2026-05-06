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
            // Interpolate variables before publishing
            string text = context.Interpolate(dlg.Dialogue);
            context.EventBus.Publish(new DialogueLineEvent(dlg.Id, dlg.Speaker, text, dlg.StageDirections));

            // Request a continue interaction
            var interaction = new ContinueInteraction("Click to continue", DialogNodeData.OutputPortId);
            return ExecutionResult.Pause(interaction);
        }
    }
}