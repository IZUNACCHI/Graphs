using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace NarrativeTool.UI.Docking
{
    /// <summary>
    /// JSON load/save of the user's dock layout. Stored globally (per-user) under
    /// <c>{persistentDataPath}/NarrativeTool/layout.json</c>. The center zone is
    /// not serialised in Phase 2 because it currently hosts <c>GraphTabManager</c>
    /// as custom content; Phase 2c will fold it into the dock tree.
    /// </summary>
    public static class DockLayoutSerializer
    {
        public const int CurrentVersion = 1;

        public static string DefaultPath
            => Path.Combine(Application.persistentDataPath, "NarrativeTool", "layout.json");

        public static void Save(DockRoot root, string path = null)
        {
            if (root == null) return;
            path ??= DefaultPath;
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var doc = new LayoutDoc
                {
                    Version = CurrentVersion,
                    Left   = SerializeZone(root.Left),
                    Right  = SerializeZone(root.Right),
                    Bottom = SerializeZone(root.Bottom),
                };
                File.WriteAllText(path, JsonConvert.SerializeObject(doc, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DockLayoutSerializer] Save failed: {ex.Message}");
            }
        }

        public static bool Load(DockRoot root, string path = null)
        {
            if (root == null) return false;
            path ??= DefaultPath;
            if (!File.Exists(path)) return false;
            try
            {
                var doc = JsonConvert.DeserializeObject<LayoutDoc>(File.ReadAllText(path));
                if (doc == null || doc.Version != CurrentVersion) return false;

                ApplyZone(root.Left,   doc.Left);
                ApplyZone(root.Right,  doc.Right);
                ApplyZone(root.Bottom, doc.Bottom);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DockLayoutSerializer] Load failed: {ex.Message}");
                return false;
            }
        }

        // ───────────────────────── Document model ─────────────────────────

        private sealed class LayoutDoc
        {
            public int Version { get; set; } = CurrentVersion;
            public ZoneDoc Left   { get; set; }
            public ZoneDoc Right  { get; set; }
            public ZoneDoc Bottom { get; set; }
        }
        private sealed class ZoneDoc
        {
            public NodeDoc Tree { get; set; }
        }
        private sealed class NodeDoc
        {
            public string Type { get; set; }                 // "area" | "split"
            // area
            public List<string> Panels { get; set; }
            public string Active { get; set; }
            // split
            public string Orientation { get; set; }          // "H" | "V"
            public NodeDoc First { get; set; }
            public NodeDoc Second { get; set; }
        }

        // ───────────────────────── Serialise ─────────────────────────

        private static ZoneDoc SerializeZone(DockZone zone)
        {
            if (zone == null || zone.Root == null) return null;
            return new ZoneDoc { Tree = SerializeNode(zone.Root) };
        }

        private static NodeDoc SerializeNode(DockNode n)
        {
            switch (n)
            {
                case DockArea a:
                    string active = null;
                    if (a.TabView.activeTab?.userData is IDockablePanel pp) active = pp.Id;
                    return new NodeDoc
                    {
                        Type = "area",
                        Panels = a.Panels.Select(p => p.Id).ToList(),
                        Active = active,
                    };
                case DockSplit s:
                    return new NodeDoc
                    {
                        Type = "split",
                        Orientation = s.Orientation == DockOrientation.Horizontal ? "H" : "V",
                        First  = SerializeNode(s.First),
                        Second = SerializeNode(s.Second),
                    };
                default: return null;
            }
        }

        // ───────────────────────── Apply ─────────────────────────

        private static void ApplyZone(DockZone zone, ZoneDoc doc)
        {
            if (zone == null) return;
            if (doc?.Tree == null) return;

            // Detach current panels back into the registry pool? They are still
            // referenced by the registry's factories (which return the same panel
            // objects), so we don't dispose anything — just rebuild the tree.
            var node = BuildNode(doc.Tree);
            if (node != null) zone.SetRoot(node);
        }

        private static DockNode BuildNode(NodeDoc doc)
        {
            if (doc == null) return null;
            switch (doc.Type)
            {
                case "area":
                {
                    var area = new DockArea();
                    if (doc.Panels != null)
                        foreach (var pid in doc.Panels)
                        {
                            var d = DockRegistry.Find(pid);
                            if (d?.Factory == null) continue; // unknown id → drop silently
                            area.AddPanel(d.Factory());
                        }
                    if (!string.IsNullOrEmpty(doc.Active)) area.SelectPanel(doc.Active);
                    if (area.IsEmpty) return null;
                    return area;
                }
                case "split":
                {
                    var first  = BuildNode(doc.First);
                    var second = BuildNode(doc.Second);
                    if (first == null && second == null) return null;
                    if (first == null) return second;
                    if (second == null) return first;
                    var orient = doc.Orientation == "V" ? DockOrientation.Vertical
                                                        : DockOrientation.Horizontal;
                    return new DockSplit(orient, first, second);
                }
                default: return null;
            }
        }
    }
}
