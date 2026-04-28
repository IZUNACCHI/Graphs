using NarrativeTool.Data.Graph;

namespace NarrativeTool.Runtime
{
    /// <summary>
    /// A snapshot of where a subgraph was called from, so execution can return afterwards.
    /// </summary>
    public sealed class CallFrame
    {

        public GraphData ReturnGraph { get; set; } //running graph
        public string ReturnNodeId { get; set; } //node to return to after subgraph finishes

        public string ReturnPortId { get; set; } //port to return to after subgraph finishes (for resuming from a choice)

        public CallFrame(GraphData returnGraph, string returnNodeId, string returnPortId)
        {
            ReturnGraph = returnGraph;
            ReturnNodeId = returnNodeId;
            ReturnPortId = returnPortId;
        }
    }
}