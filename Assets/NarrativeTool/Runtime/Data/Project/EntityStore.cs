using System.Collections.Generic;

namespace NarrativeTool.Data.Project
{
    public sealed class EntityStore
    {
        public List<EntityDefinition> Entities { get; } = new();
        public List<string> Folders { get; } = new();

        public EntityDefinition Find(string id)
        {
            foreach (var e in Entities) if (e.Id == id) return e;
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
            foreach (var e in Entities)
            {
                if (e.Id == excludeId) continue;
                if (e.FolderPath == folderPath && e.Name == name) return true;
            }
            return false;
        }

        public bool FieldNameExists(EntityDefinition entity, string name, string excludeId = null)
        {
            foreach (var f in entity.Fields)
            {
                if (f.Id == excludeId) continue;
                if (f.Name == name) return true;
            }
            return false;
        }
    }
}
