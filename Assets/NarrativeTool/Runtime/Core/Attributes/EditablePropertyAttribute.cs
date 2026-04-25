using System;

namespace NarrativeTool.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class EditablePropertyAttribute : Attribute
    {
        public string Label { get; set; }

        public string Placeholder { get; set; }
        public string Tooltip { get; set; }
        public bool Multiline { get; set; }
        public bool Editable { get; set; } = true;
        public int Order { get; set; }
        public float Min { get; set; } = float.MinValue;
        public float Max { get; set; } = float.MaxValue;
        public Type EnumType { get; set; }  // If property is an enum, this can be used to resolve options.
    }
}