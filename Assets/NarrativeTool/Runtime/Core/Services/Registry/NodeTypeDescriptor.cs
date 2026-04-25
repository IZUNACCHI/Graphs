using NarrativeTool.Canvas;
using NarrativeTool.Canvas.Views;
using NarrativeTool.Core;
using NarrativeTool.Data.Graph;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class NodeTypeDescriptor
{
    public string TypeId { get; set; }
    public string DisplayName { get; set; }
    public string Category { get; set; }
    public string Color { get; set; }
    public string Description { get; set; }
    public string DocEntryId { get; set; }

    public List<PortDefinition> Ports { get; set; } = new();

    public Func<string, Vector2, NodeData> DataFactory { get; set; }
    public Func<NodeData, GraphView, NodeView> ViewFactory { get; set; }
    public Func<IInspectorDrawer> DrawerFactory { get; set; }
}