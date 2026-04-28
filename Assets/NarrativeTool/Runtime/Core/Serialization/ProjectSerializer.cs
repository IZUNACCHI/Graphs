using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Project;
using Newtonsoft.Json;
using System;
using System.IO;
using UnityEngine;

namespace NarrativeTool.Data.Serialization
{
    /// <summary>
    /// class responsible for saving and loading projects, using the currently registered serializer in SerializerRegistry. 
    /// Also provides convenience methods for graph serialization and path generation based on project names and user-supplied locations.
    /// </summary>
    public static class ProjectSerializer
    {
        
        public static void Save(ProjectModel project, string path)
        {
            var data = SerializerRegistry.Current.SerializeProject(project);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, data);
            Debug.Log($"[ProjectSerializer] Saved as {SerializerRegistry.Current.Format} to {path}");
        }

        public static ProjectModel Load(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[ProjectSerializer] File not found: {path}");
                return null;
            }
            var data = File.ReadAllText(path);
            return SerializerRegistry.Current.DeserializeProject(data);
        }

        // Convenience graph methods
        public static void SaveGraph(GraphData graph, string path)
        {
            var data = SerializerRegistry.Current.SerializeGraph(graph);
            File.WriteAllText(path, data);
        }

        public static GraphData LoadGraph(string path)
        {
            if (!File.Exists(path)) return null;
            var graph = File.ReadAllText(path);
            return SerializerRegistry.Current.DeserializeGraph(graph);
        }

        /// <summary>
        /// Default storage path for a new project: persistentDataPath /
        /// PlotInAPot / &lt;sanitized name&gt; / project.nproj. Uses the wizard's
        /// user-supplied save location.
        /// </summary>
        public static string DefaultPathFor(string projectName)
        {
            var safe = SanitizeFileName(projectName);
            if (string.IsNullOrEmpty(safe)) safe = "untitled";
            return Path.Combine(Application.persistentDataPath, "Projects", safe, safe + ".nproj");
        }

        /// <summary>
        /// Storage path using the user-supplied save location from the wizard.
        /// </summary>
        public static string UserPathFor(string projectName, string userSuppliedPath)
        {
            var safe = SanitizeFileName(projectName);
            if (string.IsNullOrEmpty(safe)) safe = "untitled";
            
            // Use the user-supplied path if provided, otherwise fall back to default
            //TODO: Re enable this once Filebrowser is done.
            /*
            if (!string.IsNullOrEmpty(userSuppliedPath))
            {
                // Ensure the path ends with a directory separator
                var finalPath = userSuppliedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return Path.Combine(finalPath, safe, safe + ".nproj");
            }*/
            
            return DefaultPathFor(projectName);
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
