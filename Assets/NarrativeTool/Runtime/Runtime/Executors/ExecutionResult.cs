namespace NarrativeTool.Core.Runtime
{
    /// <summary>
    /// Returned by every <see cref="INodeExecutor"/> to tell the engine what to do next.
    /// </summary>
    public class ExecutionResult
    {
        /// <summary>The next port ID to follow, or null to stop/pause.</summary>
        public string NextPortId { get; set; }

        /// <summary>If not null, the engine should pause and hand control to this interaction.</summary>
        public InteractionRequest Interaction { get; set; }

        /// <summary>Convenience: creates a result that continues to a port.</summary>
        public static ExecutionResult Continue(string portId) => new ExecutionResult { NextPortId = portId };

        /// <summary>Convenience: creates a result that pauses with an interaction.</summary>
        public static ExecutionResult Pause(InteractionRequest interaction) => new ExecutionResult { Interaction = interaction };
    }
}