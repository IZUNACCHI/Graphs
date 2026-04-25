using System.Collections.Generic;

namespace NarrativeTool.Data.Project
{
    /// <summary>
    /// Project-scoped collection of <see cref="EnumDefinition"/>s. Mirrors
    /// <see cref="VariableStore"/> so a future enum-management UI can plug
    /// in alongside the variables panel.
    /// </summary>
    public sealed class EnumStore
    {
        public List<EnumDefinition> Enums { get; } = new();

        public EnumDefinition Find(string id)
        {
            foreach (var e in Enums) if (e.Id == id) return e;
            return null;
        }

        /// <summary>
        /// First member id of the given enum, or null if the enum is missing
        /// or has no members. Used as the default value when an enum-typed
        /// variable is created or its enum type is (re)assigned.
        /// </summary>
        public string FirstMemberId(string enumTypeId)
        {
            var e = Find(enumTypeId);
            if (e == null || e.Members.Count == 0) return null;
            return e.Members[0].Id;
        }
    }
}
