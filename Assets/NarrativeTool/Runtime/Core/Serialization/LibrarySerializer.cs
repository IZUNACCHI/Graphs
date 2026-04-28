using NarrativeTool.Data.Project;
using Newtonsoft.Json;
using System;
using System.IO;
using UnityEngine;

namespace NarrativeTool.Data.Serialization
{
    /// <summary>
    /// Persists <see cref="ProjectLibrary"/> (the start-screen list) to a
    /// single JSON file under persistentDataPath. Saved on every mutation
    /// (pin toggle / register opened) and loaded once at app start.
    /// </summary>
    public static class LibrarySerializer
    {
        public static string DefaultPath
            => Path.Combine(Application.persistentDataPath, "library.json");

        private sealed class LibraryDoc
        {
            public int SchemaVersion { get; set; } = 1;
            public System.Collections.Generic.List<ProjectLibraryEntry> Entries { get; set; }
                = new();
        }

        public static void Save(ProjectLibrary library, string path = null)
        {
            path ??= DefaultPath;
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var doc = new LibraryDoc { Entries = new(library.Entries) };
                var json = JsonConvert.SerializeObject(doc, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LibrarySerializer] Save failed: {ex.Message}");
            }
        }

        public static bool Load(ProjectLibrary library, string path = null)
        {
            path ??= DefaultPath;
            if (!File.Exists(path)) return false;
            try
            {
                var json = File.ReadAllText(path);
                var doc = JsonConvert.DeserializeObject<LibraryDoc>(json);
                if (doc?.Entries == null) return false;
                library.Entries.Clear();
                library.Entries.AddRange(doc.Entries);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LibrarySerializer] Load failed: {ex.Message}");
                return false;
            }
        }
    }
}
