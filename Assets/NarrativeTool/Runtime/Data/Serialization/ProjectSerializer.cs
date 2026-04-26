using NarrativeTool.Data.Project;
using Newtonsoft.Json;
using System;
using System.IO;
using UnityEngine;

namespace NarrativeTool.Data.Serialization
{
    /// <summary>
    /// JSON save/load for <see cref="ProjectModel"/>. Polymorphic
    /// node/property data is handled via <c>TypeNameHandling.Auto</c>
    /// (Newtonsoft embeds a <c>$type</c> discriminator on each NodeData
    /// subclass). Vector2 round-trips via <see cref="Vector2JsonConverter"/>.
    ///
    /// File format: a single .nproj JSON file per project. SchemaVersion
    /// lives on ProjectModel for forward compatibility — when the on-disk
    /// version differs from the runtime version, this is where a migration
    /// pass would hook in (TODO once a v2 schema lands).
    /// </summary>
    public static class ProjectSerializer
    {
        // Lazily built so Application/Resources access happens off the
        // static-initializer hot path.
        private static JsonSerializerSettings settings;
        public static JsonSerializerSettings Settings => settings ??= BuildSettings();

        private static JsonSerializerSettings BuildSettings() => new()
        {
            // Auto: embed $type only when declared type is more abstract
            // than runtime type. Needed for List<NodeData> polymorphism.
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new Vector2JsonConverter() },
        };

        public static void Save(ProjectModel project, string path)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonConvert.SerializeObject(project, Settings);
            File.WriteAllText(path, json);
            Debug.Log($"[ProjectSerializer] Saved project '{project.Name}' to {path}");
        }

        public static ProjectModel Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogWarning($"[ProjectSerializer] No file at {path}");
                return null;
            }
            try
            {
                var json = File.ReadAllText(path);
                var project = JsonConvert.DeserializeObject<ProjectModel>(json, Settings);
                Debug.Log($"[ProjectSerializer] Loaded project '{project?.Name}' from {path}");
                return project;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProjectSerializer] Failed to load {path}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Default storage path for a new project: persistentDataPath /
        /// Projects / &lt;sanitized name&gt; / project.nproj. The wizard's
        /// user-supplied save location is decorative for now (TODO: real
        /// folder picker).
        /// </summary>
        public static string DefaultPathFor(string projectName)
        {
            var safe = SanitizeFileName(projectName);
            if (string.IsNullOrEmpty(safe)) safe = "untitled";
            return Path.Combine(Application.persistentDataPath, "Projects", safe, safe + ".nproj");
        }

        private static string SanitizeFileName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var invalid = Path.GetInvalidFileNameChars();
            var chars = raw.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (Array.IndexOf(invalid, chars[i]) >= 0 || chars[i] == ' ') chars[i] = '_';
            return new string(chars);
        }
    }
}
