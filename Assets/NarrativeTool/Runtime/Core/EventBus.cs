using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarrativeTool.Core
{
    /// <summary>
    /// Typed publish/subscribe bus. Events are declared as record structs
    /// (or classes — either works). Subscribers receive exactly the event type
    /// they asked for, with full IDE autocomplete on payload fields.
    ///
    /// Usage:
    ///   Services.Get&lt;EventBus&gt;().Subscribe&lt;NodeMovedEvent&gt;(e => ...);
    ///   Services.Get&lt;EventBus&gt;().Publish(new NodeMovedEvent(...));
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, List<Delegate>> subscribers = new();

        /// <summary>
        /// Subscribe to events of type T. Returns an IDisposable that unsubscribes
        /// when disposed — store it if you need to unsubscribe later (e.g. when a
        /// view is destroyed), ignore it otherwise.
        /// </summary>
        public IDisposable Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var type = typeof(T);
            if (!subscribers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                subscribers[type] = list;
            }
            list.Add(handler);
            return new Subscription(() => Unsubscribe(handler));
        }

        /// <summary>
        /// Unsubscribe a handler. Typically you use the IDisposable returned by
        /// Subscribe instead.
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler)
        {
            if (subscribers.TryGetValue(typeof(T), out var list))
                list.Remove(handler);
        }

        /// <summary>
        /// Publish an event. Every subscriber for this exact type receives it.
        /// Exceptions in one handler don't prevent others from running.
        /// </summary>
        public void Publish<T>(T evt)
        {
            if (!subscribers.TryGetValue(typeof(T), out var list)) return;
            // Copy so handlers can unsubscribe during iteration without issue
            var snapshot = list.ToArray();
            Debug.Log($"[Bus] {typeof(T).Name}: {evt}");
            foreach (var d in snapshot)
            {
                try { ((Action<T>)d)(evt); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action dispose;
            public Subscription(Action dispose) { this.dispose = dispose; }
            public void Dispose() { dispose?.Invoke(); dispose = null; }
        }
    }
}