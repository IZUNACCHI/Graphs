using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NarrativeTool.Data.Graph
{
    /// <summary>
    /// Abstract base for every node type. Concrete subclasses add their own
    /// fields (TextNode carries a string, Start/End carry nothing extra).
    /// </summary>
    public abstract class NodeData
    {
        public string Id { get; }
        public string Title { get; set; }

        public string TypeId { get; set; }
        public NodeCategory Category { get; set; }
        public Vector2 Position { get; set; }

        public List<PortData> Inputs { get; } = new();
        public List<PortData> Outputs { get; } = new();

        public List<PropertyInstance> Properties { get; } = new();

        protected NodeData(string id, string title, NodeCategory category, Vector2 position)
        {
            Id = id; Title = title; Category = category; Position = position;
            var attr = GetType().GetCustomAttribute<NodeTypeAttribute>();
            if (attr != null)
                TypeId = attr.NodeTypeId;
        }

        public PortData FindPort(string portId)
        {
            foreach (var p in Inputs) if (p.Id == portId) return p;
            foreach (var p in Outputs) if (p.Id == portId) return p;
            return null;
        }
    }
}