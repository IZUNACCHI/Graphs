// NarrativeTool.Core/NodeRegistry.cs
using NarrativeTool.Canvas;
using NarrativeTool.Canvas.Views;
using NarrativeTool.Core;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Central registry for node types, allowing dynamic registration and lookup of node descriptors.
/// This supports both built-in node types (via reflection) and user-defined types (via code).
/// </summary>
public sealed class NodeRegistry
{
    // Keyed by TypeId for fast lookup
    private readonly Dictionary<string, NodeTypeDescriptor> descriptors = new();
    

    public void Register(NodeTypeDescriptor desc)
    {
        if (desc == null) throw new ArgumentNullException(nameof(desc));
        if (string.IsNullOrWhiteSpace(desc.TypeId))
            throw new ArgumentException("TypeId is required.");
        if (descriptors.ContainsKey(desc.TypeId))
            throw new InvalidOperationException($"Node type '{desc.TypeId}' already registered.");
        descriptors[desc.TypeId] = desc;
        Debug.Log($"[NodeRegistry] Registered {desc.TypeId}");
    }

    public void Unregister(string typeId) => descriptors.Remove(typeId);

    public NodeTypeDescriptor TryGet(string typeId) =>
        descriptors.TryGetValue(typeId, out var d) ? d : null;

    public IReadOnlyList<NodeTypeDescriptor> GetAll() => descriptors.Values.ToList();

    public NodeData CreateData(string typeId, Vector2 position)
    {
        var desc = TryGet(typeId);
        return desc?.DataFactory?.Invoke(typeId + "_" + Guid.NewGuid().ToString("N").Substring(0, 8), position);
    }

    public NodeView CreateView(NodeData node, GraphView canvas)
    {
        if (node == null || string.IsNullOrEmpty(node.TypeId))
            return null;   // let caller fall back to generic view

        var desc = TryGet(node.TypeId);
        return desc?.ViewFactory?.Invoke(node, canvas);
    }

    public void Clear() => descriptors.Clear();

    // Built in registration via reflection
    public void RegisterBuiltInTypes()
    {
        // Gather all NodeView subclasses that have [NodeViewOf]
        var viewMap = new Dictionary<Type, Type>(); // nodeDataType -> viewType
        foreach (var assembly in GetAssemblies())
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsSubclassOf(typeof(NodeView)) || type.IsAbstract) continue;
                var attr = type.GetCustomAttribute<NodeViewOfAttribute>();
                if (attr != null)
                    viewMap[attr.NodeDataType] = type;
            }
        }

        Debug.Log($"[NodeRegistry] viewMap entries: {viewMap.Count}");
        foreach (var kv in viewMap)
            Debug.Log($"  {kv.Key.Name} -> {kv.Value.Name}");

        // Scan for NodeData subclasses with [NodeType]
        var nodeDataType = typeof(NodeData);
        foreach (var assembly in GetAssemblies())
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsSubclassOf(nodeDataType) || type.IsAbstract) continue;
                var attr = type.GetCustomAttribute<NodeTypeAttribute>();
                if (attr == null) continue;

                // Resolve view type: specific view from attribute, or generic NodeView
                Type viewType = viewMap.TryGetValue(type, out var vt) ? vt : typeof(NodeView);

                var desc = new NodeTypeDescriptor
                {
                    TypeId = attr.NodeTypeId,
                    DisplayName = attr.DisplayName,
                    Category = attr.Category,
                    Color = attr.Color,
                    Description = attr.Description,
                    DocEntryId = attr.DocEntryId,
                    DataFactory = (id, pos) =>
                    {
                        var node = (NodeData)Activator.CreateInstance(type, id, attr.DisplayName, pos);
                        node.TypeId = attr.NodeTypeId;
                        return node;
                    },
                    ViewFactory = (node, canvas) => (NodeView)Activator.CreateInstance(viewType, node, canvas),
                    DrawerFactory = null   // to be extended with [InspectorDrawerOf] later
                };
                var tempNode = desc.DataFactory("__temp__", Vector2.zero);
                foreach (var p in tempNode.Inputs)
                    desc.Ports.Add(new PortDefinition
                    {
                        PortId = p.Id,
                        Direction = PortDirection.Input,
                        TypeTag = p.TypeTag
                    });
                foreach (var p in tempNode.Outputs)
                    desc.Ports.Add(new PortDefinition
                    {
                        PortId = p.Id,
                        Direction = PortDirection.Output,
                        TypeTag = p.TypeTag
                    });

                Register(desc);
            }
        }
    }

    // Given a port direction and type tag, find all node types that have at least one compatible port
    public IEnumerable<NodeTypeDescriptor> GetCompatibleNodes(PortDirection fromDirection, string typeTag)
    {
        PortDirection desired = fromDirection == PortDirection.Output
                                ? PortDirection.Input
                                : PortDirection.Output;

        return descriptors.Values
            .Where(desc => desc.Ports.Any(p => p.Direction == desired
                                               && p.TypeTag == typeTag));
    }

    // Helper to filter assemblies
    private static IEnumerable<Assembly> GetAssemblies()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.FullName.StartsWith("System") &&
                        !a.FullName.StartsWith("UnityEngine") &&
                        !a.FullName.StartsWith("UnityEditor") &&
                        !a.FullName.StartsWith("Unity.") &&
                        !a.FullName.StartsWith("mscorlib"));
    }
}
