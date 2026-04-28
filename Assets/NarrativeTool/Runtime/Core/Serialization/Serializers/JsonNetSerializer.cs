using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Project;
using NarrativeTool.Systems.Serialization;
using Newtonsoft.Json;

namespace NarrativeTool.Data.Serialization
{
    public sealed class JsonNetSerializer : ISerializer
    {
        public string Format => "json";

        private readonly JsonSerializerSettings settings;

        public JsonNetSerializer()
        {
            settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                Converters = { new Vector2JsonConverter() }
            };
        }

        public string SerializeProject(ProjectModel project)
        {
            return JsonConvert.SerializeObject(project, settings);
        }

        public ProjectModel DeserializeProject(string data)
        {
            return JsonConvert.DeserializeObject<ProjectModel>(data, settings);
        }

        public string SerializeGraph(GraphData graph)
        {
            return JsonConvert.SerializeObject(graph, settings);
        }

        public GraphData DeserializeGraph(string data)
        {
            return JsonConvert.DeserializeObject<GraphData>(data, settings);
        }
    }
}