using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class NodeViewOfAttribute : Attribute
{
    public Type NodeDataType { get; }
    public NodeViewOfAttribute(Type nodeDataType)
    {
        NodeDataType = nodeDataType;
    }
}