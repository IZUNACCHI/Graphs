using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarrativeTool.Core
{
    /// <summary>
    /// Central access point for app-wide singleton services (EventBus,
    /// ContextMenuController, etc). Services are registered at bootstrap time
    /// and accessed via Get&lt;T&gt;().
    ///
    /// Per-project runtime state (command stacks, selection) does NOT live
    /// here — that's in SessionState.
    /// </summary>
    public static class Services
    {
        private static readonly Dictionary<Type, object> map = new();

        public static void Register<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            map[typeof(T)] = instance;
            Debug.Log($"[Services] Registered {typeof(T).Name}");
        }

        public static T Get<T>() where T : class
        {
            if (!map.TryGetValue(typeof(T), out var obj))
                throw new InvalidOperationException(
                    $"Service {typeof(T).Name} is not registered. " +
                    $"Did you forget to call Services.Register<{typeof(T).Name}>() in bootstrap?");
            return (T)obj;
        }

        public static T TryGet<T>() where T : class
        {
            map.TryGetValue(typeof(T), out var obj);
            return obj as T;
        }

        public static void Clear()
        {
            map.Clear();
            Debug.Log("[Services] Cleared all services");
        }
    }
}