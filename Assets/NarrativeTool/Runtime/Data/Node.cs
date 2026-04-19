using System.Collections.Generic;
using UnityEngine;

namespace NarrativeTool.Data
{
    /// <summary>
    /// Abstract base for every node type. Concrete subclasses add their own
    /// fields (TextNode carries a string, Start/End carry nothing extra).
    /// </summary>
    public abstract class Node
    {
        public string Id { get; }
        public string Title { get; set; }
        public NodeCategory Category { get; set; }
        public Vector2 Position { get; set; }

        public List<Port> Inputs { get; } = new();
        public List<Port> Outputs { get; } = new();

        protected Node(string id, string title, NodeCategory category, Vector2 position)
        {
            Id = id; Title = title; Category = category; Position = position;
        }

        public Port FindPort(string portId)
        {
            foreach (var p in Inputs) if (p.Id == portId) return p;
            foreach (var p in Outputs) if (p.Id == portId) return p;
            return null;
        }
    }
}