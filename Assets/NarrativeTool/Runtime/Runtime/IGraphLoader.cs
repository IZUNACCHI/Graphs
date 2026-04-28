using NarrativeTool.Data.Graph;

namespace NarrativeTool.Core.Runtime
{
    public interface IGraphLoader
    {
        GraphData GetGraph(string graphId);
    }
}