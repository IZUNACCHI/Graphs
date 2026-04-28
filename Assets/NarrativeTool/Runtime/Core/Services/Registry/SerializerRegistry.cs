using NarrativeTool.Systems.Serialization;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarrativeTool.Data.Serialization
{
    public static class SerializerRegistry
    {
        private static readonly Dictionary<string, ISerializer> serializers = new();
        private static ISerializer current;

        public static void Register(ISerializer serializer)
        {
            serializers[serializer.Format] = serializer
                ?? throw new ArgumentNullException(nameof(serializer));
            Debug.Log($"[Serializer] Registered '{serializer.Format}'");
        }

        public static void SetCurrent(string format)
        {
            current = serializers.TryGetValue(format, out var s)
                ? s
                : throw new InvalidOperationException($"No serializer registered for format '{format}'");
        }

        public static ISerializer Current => current
            ?? throw new InvalidOperationException("No serializer set as current");
    }
}