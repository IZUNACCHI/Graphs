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
        public string Id { get; set; }
        public string Title { get; set; }

        public string TypeId { get; set; }
        public NodeCategory Category { get; set; }
        public Vector2 Position { get; set; }

        // Settable so JSON deserialization replaces the constructor-added
        // ports cleanly (otherwise it would Add into the existing list and
        // duplicate them).
        public List<PortData> Inputs { get; set; } = new();
        public List<PortData> Outputs { get; set; } = new();

        public List<PropertyInstance> Properties { get; } = new();

        protected NodeData(string id, string title, NodeCategory category, Vector2 position)
        {
            Id = id; Title = title; Category = category; Position = position;
            var attr = GetType().GetCustomAttribute<NodeTypeAttribute>();
            if (attr != null)
                TypeId = attr.NodeTypeId;
        }

        protected NodeData()
        {
            Id = System.Guid.NewGuid().ToString("N").Substring(0, 8); // fallback
            Title = "";
            Category = NodeCategory.Flow;
            Position = Vector2.zero;
        }

        public PortData FindPort(string portId)
        {
            foreach (var p in Inputs) if (p.Id == portId) return p;
            foreach (var p in Outputs) if (p.Id == portId) return p;
            return null;
        }
    }
}