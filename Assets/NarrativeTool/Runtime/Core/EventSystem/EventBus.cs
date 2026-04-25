using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarrativeTool.Core.EventSystem
{
    /// <summary>
    /// Typed publish/subscribe bus. Events are declared as record structs or
    /// classes; subscribers receive exactly the event type they asked for.
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, List<Delegate>> subscribers = new();

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

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (subscribers.TryGetValue(typeof(T), out var list))
                list.Remove(handler);
        }

        public void Publish<T>(T evt)
        {
            if (!subscribers.TryGetValue(typeof(T), out var list)) return;
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