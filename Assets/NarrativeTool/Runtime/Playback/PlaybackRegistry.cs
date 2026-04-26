using NarrativeTool.Data.Graph;
using System;
using System.Collections.Generic;

namespace NarrativeTool.Playback
{
    /// <summary>
    /// Lookup from concrete NodeData subclass to its <see cref="INodeRuntime"/>.
    /// Lives on Services so handlers register at bootstrap once and any
    /// playback session can resolve them.
    /// </summary>
    public sealed class PlaybackRegistry
    {
        private readonly Dictionary<Type, INodeRuntime> handlers = new();

        public void Register<T>(INodeRuntime handler) where T : NodeData
            => handlers[typeof(T)] = handler;

        public INodeRuntime For(NodeData node)
            => handlers.TryGetValue(node.GetType(), out var h) ? h : null;
    }
}
