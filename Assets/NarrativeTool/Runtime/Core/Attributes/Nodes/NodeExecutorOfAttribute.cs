using System;

namespace NarrativeTool.Core.Runtime
{
    /// <summary>
    /// Marks an <see cref="INodeExecutor"/> class as handling the given <see cref="NodeData"/> type.
    /// The registry uses this to map <c>TypeId</c> to executor.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class NodeExecutorOfAttribute : Attribute
    {
        public Type NodeDataType { get; }
        public NodeExecutorOfAttribute(Type nodeDataType) => NodeDataType = nodeDataType;
    }
}