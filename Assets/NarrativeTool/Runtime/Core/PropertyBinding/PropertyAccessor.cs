using System;
using System.Reflection;
using NarrativeTool.Data.Graph;
using UnityEngine;

namespace NarrativeTool.Core.Binding
{
    /// <summary>
    /// Two paths, same interface:
    ///   FromReflection    — built-in C# properties on NodeData subclasses.
    ///   FromPropertyValue — dynamic properties stored as JSON in PropertyValue.
    /// PropertyFieldFactory sees only Getter/Setter and is unchanged.
    /// </summary>
    public sealed class PropertyAccessor
    {
        public Func<object> Getter { get; }
        public Action<object> Setter { get; }

        private PropertyAccessor(Func<object> getter, Action<object> setter)
        {
            Getter = getter ?? throw new ArgumentNullException(nameof(getter));
            Setter = setter ?? throw new ArgumentNullException(nameof(setter));
        }

        // ── Factory: reflection-based ────────────────────────────────

        public static PropertyAccessor FromReflection(object target, PropertyInfo prop)
        {
            var targetType = prop.PropertyType;
            return new PropertyAccessor(
                getter: () => prop.GetValue(target),
                setter: val =>
                {
                    object converted;
                    if (targetType.IsEnum && val is string s)
                        converted = Enum.Parse(targetType, s);
                    else if (val != null && targetType != val.GetType())
                        converted = Convert.ChangeType(val, targetType);
                    else
                        converted = val;
                    prop.SetValue(target, converted);
                });
        }

        // ── Factory: PropertyValue bag ───────────────────────────────

        /// <summary>
        /// PropertyMetadata describes the type; PropertyValue holds the data.
        /// Falls back to PropertyMetadata.Default when SerializedValue is empty.
        /// </summary>
        public static PropertyAccessor FromPropertyValue(PropertyInstance pv, PropertyDefinition meta)
        {
            if (pv == null) throw new ArgumentNullException(nameof(pv));
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            return new PropertyAccessor(
                getter: () => Deserialize(
                    string.IsNullOrEmpty(pv.SerializedValue) ? meta.Default : pv.SerializedValue,
                    meta.Type, meta.EnumOptions),
                setter: val => pv.SerializedValue = Serialize(val, meta.Type));
        }

        // ── Serialization ────────────────────────────────────────────

        private static object Deserialize(string raw, PropertyType type, string[] enumOptions)
        {
            if (string.IsNullOrEmpty(raw)) return GetDefault(type, enumOptions);
            try
            {
                switch (type)
                {
                    case PropertyType.String: return raw;
                    case PropertyType.Int: return int.Parse(raw);
                    case PropertyType.Float:
                        return float.Parse(raw,
                                                 System.Globalization.CultureInfo.InvariantCulture);
                    case PropertyType.Bool: return bool.Parse(raw);
                    case PropertyType.Enum: return raw;
                    default: return raw;
                }
            }
            catch
            {
                Debug.LogWarning($"[PropertyAccessor] Failed to deserialize '{raw}' as {type}. Using default.");
                return GetDefault(type, enumOptions);
            }
        }

        private static string Serialize(object val, PropertyType type)
        {
            if (val == null) return "";
            if (type == PropertyType.Float)
                return ((float)val).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return val.ToString();
        }

        private static object GetDefault(PropertyType type, string[] enumOptions)
        {
            switch (type)
            {
                case PropertyType.String: return "";
                case PropertyType.Int: return 0;
                case PropertyType.Float: return 0f;
                case PropertyType.Bool: return false;
                case PropertyType.Enum: return enumOptions?.Length > 0 ? enumOptions[0] : "";
                default: return null;
            }
        }
    }
}