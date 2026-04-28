using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace NarrativeTool.Data.Serialization
{
    /// <summary>
    /// Round-trips Vector2 as {"x":..,"y":..}. Without this, Newtonsoft
    /// follows every public property on Vector2 (magnitude, normalized,
    /// sqrMagnitude, ...), bloating the JSON and breaking deserialization.
    /// </summary>
    public sealed class Vector2JsonConverter : JsonConverter<Vector2>
    {
        public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x"); writer.WriteValue(value.x);
            writer.WritePropertyName("y"); writer.WriteValue(value.y);
            writer.WriteEndObject();
        }

        public override Vector2 ReadJson(JsonReader reader, Type objectType,
                                         Vector2 existingValue, bool hasExistingValue,
                                         JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return Vector2.zero;
            var obj = JObject.Load(reader);
            return new Vector2(
                obj.Value<float?>("x") ?? 0f,
                obj.Value<float?>("y") ?? 0f);
        }
    }
}
