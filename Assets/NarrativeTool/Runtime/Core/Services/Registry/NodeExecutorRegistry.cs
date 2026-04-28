using NarrativeTool.Core.Attributes;
using NarrativeTool.Data.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace NarrativeTool.Core.Runtime
{
    /// <summary>
    /// Maintains a map from node TypeId to its <see cref="INodeExecutor"/>.
    /// Call <see cref="ScanAssemblies"/> at startup to auto register built in executors.
    /// </summary>
    public sealed class NodeExecutorRegistry
    {
        private readonly Dictionary<string, INodeExecutor> executors = new();

        public void Register(string typeId, INodeExecutor executor)
        {
            if (executors.ContainsKey(typeId))
                throw new InvalidOperationException($"Executor for '{typeId}' already registered.");
            executors[typeId] = executor;
        }

        public INodeExecutor Get(string typeId)
        {
            return executors.TryGetValue(typeId, out var e) ? e : null;
        }

        /// <summary>
        /// Scans all loaded assemblies for classes with <see cref="NodeExecutorOfAttribute"/>
        /// and registers them automatically.
        /// </summary>
        public void ScanAssemblies()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
              .Where(a => !a.FullName.StartsWith("System") &&
                        !a.FullName.StartsWith("UnityEngine") &&
                        !a.FullName.StartsWith("UnityEditor") &&
                        !a.FullName.StartsWith("Unity.") &&
                        !a.FullName.StartsWith("mscorlib")))
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (!typeof(INodeExecutor).IsAssignableFrom(type) || type.IsAbstract) continue;
                    var attr = type.GetCustomAttribute<NodeExecutorOfAttribute>();
                    if (attr == null) continue;

                    var nodeTypeAttr = attr.NodeDataType.GetCustomAttribute<NodeTypeAttribute>();
                    if (nodeTypeAttr == null)
                    {
                        Debug.LogWarning($"[NodeExecutorRegistry] {attr.NodeDataType.Name} has no [NodeType], skipped.");
                        continue;
                    }
                    var executor = (INodeExecutor)Activator.CreateInstance(type);
                    Register(nodeTypeAttr.NodeTypeId, executor);
                    Debug.Log($"[NodeExecutorRegistry] Registered executor for '{nodeTypeAttr.NodeTypeId}' : {type.Name}");
                }
            }
        }
    }
}