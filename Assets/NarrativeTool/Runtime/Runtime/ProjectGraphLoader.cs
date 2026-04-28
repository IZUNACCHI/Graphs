using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Project;
using System.Linq;

namespace NarrativeTool.Core.Runtime
{
    /// <summary>
    /// Default loader that looks up a <see cref="LazyGraph"/> in the current project.
    /// </summary>
    public class ProjectGraphLoader : IGraphLoader
    {
        private readonly ProjectModel project;
        public ProjectGraphLoader(ProjectModel project) => this.project = project;

        public GraphData GetGraph(string graphId)
        {
            var lazy = project.Graphs.Items.FirstOrDefault(g => g.Id == graphId);
            return lazy?.GetGraph();
        }
    }
}