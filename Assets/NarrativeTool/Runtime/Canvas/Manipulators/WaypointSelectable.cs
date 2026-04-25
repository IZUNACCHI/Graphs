using System.Collections.Generic;
using NarrativeTool.Canvas.Views;
using NarrativeTool.Core;
using NarrativeTool.Core.Selection;

namespace NarrativeTool.Canvas.Manipulators
{
    /// <summary>
    /// Lightweight wrapper that adapts an (EdgeView, waypoint index) pair as
    /// an ISelectable, so waypoints can live in the SelectionService without
    /// owning their own VisualElement.
    ///
    /// Cached per (EdgeView, index) — requesting the same pair returns the
    /// same instance, which is necessary for HashSet<ISelectable> membership
    /// to work correctly across hover, right-click, and drag.
    ///
    /// When the EdgeView's waypoint list changes (add/remove), the cache
    /// entries for shifted indices become stale. Callers (GraphCanvas) are
    /// responsible for invalidating the cache whenever waypoints mutate.
    /// </summary>
    public sealed class WaypointSelectable : ISelectable
    {
        public EdgeView EdgeView { get; }
        public int Index { get; }

        private WaypointSelectable(EdgeView edgeView, int index)
        {
            EdgeView = edgeView; Index = index;
        }

        // Cache so (edgeView, index) always produces the same instance.
        private static readonly Dictionary<(EdgeView, int), WaypointSelectable> cache = new();

        public static WaypointSelectable Get(EdgeView edgeView, int index)
        {
            var key = (edgeView, index);
            if (!cache.TryGetValue(key, out var wp))
            {
                wp = new WaypointSelectable(edgeView, index);
                cache[key] = wp;
            }
            return wp;
        }

        /// <summary>
        /// Drop all cached entries for an EdgeView. Call whenever the edge's
        /// waypoint list mutates — stale indices would refer to the wrong
        /// waypoint or out-of-range.
        /// </summary>
        public static void InvalidateEdge(EdgeView edgeView)
        {
            var toRemove = new List<(EdgeView, int)>();
            foreach (var key in cache.Keys)
                if (key.Item1 == edgeView) toRemove.Add(key);
            foreach (var key in toRemove) cache.Remove(key);
        }

        /// <summary>Drop the entire cache (e.g. on canvas rebind).</summary>
        public static void ClearAll() => cache.Clear();

        // ISelectable
        public void OnSelected() => EdgeView.SetWaypointSelected(Index, true);
        public void OnDeselected() => EdgeView.SetWaypointSelected(Index, false);
    }
}