using System.Collections.Generic;

namespace NarrativeTool.Data.Project
{
    /// <summary>
    /// A user-defined enum type usable by variables (and, later, scripting).
    /// As with variables, <see cref="Id"/> is the stable reference future
    /// scripts will bind against; <see cref="Name"/> is the display label.
    /// </summary>
    public sealed class EnumDefinition : IFolderableItem, INamedItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string FolderPath { get; set; } = "";
        public List<EnumMember> Members { get; } = new();

        public EnumDefinition(string id, string name, string folderPath = "")
        {
            Id = id; Name = name; FolderPath = folderPath ?? "";
        }

        public EnumDefinition()
        {
            Id = System.Guid.NewGuid().ToString("N").Substring(0, 8);
            Name = "New Enum";
            FolderPath = "";
        }

        public EnumMember FindMember(string memberId)
        {
            foreach (var m in Members) if (m.Id == memberId) return m;
            return null;
        }
    }

    /// <summary>
    /// One value of an enum type. Member <see cref="Id"/> is what gets stored
    /// in <see cref="VariableDefinition.DefaultValue"/> when the parent
    /// variable is enum-typed; <see cref="Name"/> is the display text.
    /// </summary>
    public sealed class EnumMember
    {
        public string Id { get; private set; }
        public string Name { get; set; }

        public EnumMember(string id, string name)
        {
            Id = id; Name = name;
        }

        public EnumMember() { }
    }
}
