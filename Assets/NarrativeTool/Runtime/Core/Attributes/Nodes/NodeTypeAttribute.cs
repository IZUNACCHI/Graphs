using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class NodeTypeAttribute : Attribute
{
    public string NodeTypeId { get; }
    public string DisplayName { get; }
    public string Category { get; set; } = "General";
    public string Color { get; set; } = "#4a9a6a";
    public string Description { get; set; } = "";
    public string DocEntryId { get; set; }

    public NodeTypeAttribute(string typeId, string displayName)
    {
        NodeTypeId = typeId;
        DisplayName = displayName;
    }
}