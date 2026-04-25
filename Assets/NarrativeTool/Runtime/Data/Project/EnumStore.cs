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
        public List<string> Folders { get; } = new();

        public EnumDefinition Find(string id)
        {
            foreach (var e in Enums) if (e.Id == id) return e;
            return null;
        }

        public bool FolderExists(string path)
        {
            path ??= "";
            if (string.IsNullOrEmpty(path)) return true;
            return Folders.Contains(path);
        }

        public bool NameExistsInFolder(string folderPath, string name, string excludeId = null)
        {
            folderPath ??= "";
            foreach (var e in Enums)
            {
                if (e.Id == excludeId) continue;
                if (e.FolderPath == folderPath && e.Name == name) return true;
            }
            return false;
        }

        public bool MemberNameExists(EnumDefinition enumDef, string name, string excludeId = null)
        {
            foreach (var m in enumDef.Members)
            {
                if (m.Id == excludeId) continue;
                if (m.Name == name) return true;
            }
            return false;
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
