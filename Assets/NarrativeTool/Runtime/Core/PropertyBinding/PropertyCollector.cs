using System;
using System.Collections.Generic;
using System.Reflection;
using NarrativeTool.Core.Attributes;
using NarrativeTool.Data.Graph;

namespace NarrativeTool.Core.Binding
{
    public static class PropertyCollector
    {
        // ── Path 1: attribute-based (built in C# properties) ────────

        /// <summary>
        /// Collects (Meta, Accessor) pairs for all [EditableProperty] C# properties
        /// on a NodeData subclass.
        /// </summary>
        public static List<(PropertyDefinition Meta, PropertyAccessor Accessor)>
            CollectFromNode(NodeData node)
        {
            var results = new List<(PropertyDefinition, PropertyAccessor)>();
            var props = node.GetType()
                              .GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in props)
            {
                var attr = prop.GetCustomAttribute<EditablePropertyAttribute>();
                if (attr == null) continue;

                var meta = BuildMeta(prop, attr);
                if (meta == null) continue; // unsupported type

                results.Add((meta, PropertyAccessor.FromReflection(node, prop)));
            }

            return results;
        }

        // ── Path 2: PropertyValue bag (dynamic / custom properties) ─

        /// <summary>
        /// Collects (Meta, Accessor) pairs for a list of PropertyValues matched
        /// against their PropertyMetadata descriptors by Id.
        /// Missing metadata entries are skipped with a warning.
        /// </summary>
        public static List<(PropertyDefinition Meta, PropertyAccessor Accessor)>
            CollectFromPropertyValues(IList<PropertyInstance> values,
                                      IList<PropertyDefinition> metadataList)
        {
            if (values == null || metadataList == null)
                return new List<(PropertyDefinition, PropertyAccessor)>();

            // Index by Id for O(1) lookup
            var metaMap = new Dictionary<string, PropertyDefinition>(metadataList.Count);
            foreach (var m in metadataList)
                metaMap[m.Id] = m;

            var results = new List<(PropertyDefinition, PropertyAccessor)>(values.Count);

            foreach (var pv in values)
            {
                if (!metaMap.TryGetValue(pv.DefinitionId, out var meta))
                {
                    UnityEngine.Debug.LogWarning(
                        $"[PropertyCollector] No metadata found for DefinitionId '{pv.DefinitionId}'. Skipped.");
                    continue;
                }

                results.Add((meta, PropertyAccessor.FromPropertyValue(pv, meta)));
            }

            return results;
        }

        // ── Helpers ─────────────────────────────────────────────────

        private static PropertyDefinition BuildMeta(PropertyInfo prop, EditablePropertyAttribute attr)
        {
            var meta = new PropertyDefinition
            {
                Id = prop.Name,
                Label = attr.Label ?? prop.Name,
                Placeholder = attr.Placeholder,
                Tooltip = attr.Tooltip,
                Editable = attr.Editable,
                Multiline = attr.Multiline,
                Min = attr.Min,
                Max = attr.Max,
            };

            if (prop.PropertyType == typeof(string)) meta.Type = PropertyType.String;
            else if (prop.PropertyType == typeof(int)) meta.Type = PropertyType.Int;
            else if (prop.PropertyType == typeof(float)) meta.Type = PropertyType.Float;
            else if (prop.PropertyType == typeof(bool)) meta.Type = PropertyType.Bool;
            else if (prop.PropertyType.IsEnum)
            {
                meta.Type = PropertyType.Enum;
                meta.EnumOptions = Enum.GetNames(prop.PropertyType);
            }
            else return null;

            return meta;
        }
    }
}