using NarrativeTool.Data.Project;
using NarrativeTool.Data.Graph;

namespace NarrativeTool.Systems.Serialization
{
    public interface ISerializer
    {
        //Format identifier, e.g. "json", "yaml", "xml". Used for display and file extension handling.
        string Format { get; }

        string SerializeProject(ProjectModel project);
        ProjectModel DeserializeProject(string data);

        string SerializeGraph(GraphData graph);
        GraphData DeserializeGraph(string data);
    }
}
