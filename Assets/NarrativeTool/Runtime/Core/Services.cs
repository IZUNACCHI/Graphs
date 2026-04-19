using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarrativeTool.Core
{
    /// <summary>
    /// Central access point for app-wide singleton services (EventBus, CommandSystem, etc).
    /// Services are registered at bootstrap time and accessed via Get&lt;T&gt;().
    /// Using the singleton pattern (not a DI container) for simplicity.
    /// </summary>
    public static class Services
    {
        private static readonly Dictionary<Type, object> map = new();

        /// <summary>
        /// Register a service instance. Typically called from AppBootstrap.
        /// Overwrites any previous registration for the same type (useful for tests).
        /// </summary>
        public static void Register<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            map[typeof(T)] = instance;
            Debug.Log($"[Services] Registered {typeof(T).Name}");
        }

        /// <summary>
        /// Retrieve a registered service. Throws if not registered — failing loud
        /// is better than silent nulls.
        /// </summary>
        public static T Get<T>() where T : class
        {
            if (!map.TryGetValue(typeof(T), out var obj))
                throw new InvalidOperationException(
                    $"Service {typeof(T).Name} is not registered. " +
                    $"Did you forget to call Services.Register<{typeof(T).Name}>() in bootstrap?");
            return (T)obj;
        }

        /// <summary>
        /// Try to retrieve a service without throwing. Returns null if not registered.
        /// </summary>
        public static T TryGet<T>() where T : class
        {
            map.TryGetValue(typeof(T), out var obj);
            return obj as T;
        }

        /// <summary>
        /// Clear all registered services. Call on app shutdown or scene reload.
        /// </summary>
        public static void Clear()
        {
            map.Clear();
            Debug.Log("[Services] Cleared all services");
        }
    }
}