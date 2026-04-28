using NarrativeTool.Data.Graph;

namespace NarrativeTool.Core.Runtime
{
    /// <summary>
    /// Executes the runtime behaviour of a specific node type.
    /// Returns an <see cref="ExecutionResult"/> that tells the engine what to do next.
    /// </summary>
    public interface INodeExecutor
    {
        ExecutionResult Execute(NodeData node, RuntimeContext context);
    }
}