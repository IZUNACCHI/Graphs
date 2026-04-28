using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Data.Project
{
    public sealed class FolderTreeStore<T> where T : IFolderableItem
    {
        public List<T> Items { get; } = new();
        public List<string> Folders { get; } = new();

        private Vector2 lastPointerDownPos;


        public T Find(string id)
        {
            foreach (var item in Items)
                if (item.Id == id) return item;
            return default;
        }

        public bool FolderExists(string path)
        {
            if (string.IsNullOrEmpty(path)) return true;
            return Folders.Contains(path);
        }

        public bool NameExistsInFolder(string folderPath, string name, string excludeId = null)
        {
            folderPath ??= "";
            foreach (var item in Items)
            {
                if (item.Id == excludeId) continue;
                if (item.FolderPath == folderPath && item is INamedItem named && named.Name == name)
                    return true;
            }
            return false;
        }
    }
    public static class FolderTreeStoreExtensions
    {
        // ── Enum helpers ──

        public static string FirstMemberId(this FolderTreeStore<EnumDefinition> store, string enumTypeId)
        {
            var e = store.Items.FirstOrDefault(x => x.Id == enumTypeId);
            return e?.Members?.FirstOrDefault()?.Id;
        }

        public static bool MemberNameExists(this FolderTreeStore<EnumDefinition> store, EnumDefinition enumDef, string name, string excludeId = null)
        {
            return enumDef.Members.Any(m => m.Name == name && m.Id != excludeId);
        }

        // ── Entity helpers ──

        public static bool FieldNameExists(this FolderTreeStore<EntityDefinition> store, EntityDefinition entity, string name, string excludeId = null)
        {
            return entity.Fields.Any(f => f.Name == name && f.Id != excludeId);
        }

        public static VisualElement GetFirstAncestorWithClass(this VisualElement element, string className)
        {
            var ve = element;
            while (ve != null)
            {
                if (ve.ClassListContains(className)) return ve;
                ve = ve.parent;
            }
            return null;
        }
    }
}