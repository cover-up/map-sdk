using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// The map-scene component policy, shared so the two enforcement points can't
    /// drift: the runtime loader (<see cref="MapLoader"/> strips offenders from a
    /// loaded scene) and the editor tools (Validate Map / Export Workshop Map
    /// refuse to ship a scene that contains them). See Docs/Steam.md §8 map
    /// security.
    ///
    /// Why an allowlist at all: a Unity AssetBundle carries no code — a map's
    /// MonoBehaviours rebind by GUID to classes already compiled into THIS build.
    /// So a crafted Workshop map can't inject new code, but without this it could
    /// instantiate one of our own gameplay/networking/dev components (or a shipped
    /// plugin's) with attacker-chosen serialized fields and let its lifecycle
    /// methods run. The allowlist admits only the Map SDK authoring components and
    /// stock Unity engine components; everything else is rejected.
    /// </summary>
    public static class MapSceneGuard
    {
        // The assembly the Map SDK authoring components compile into
        // (Packages/com.coverup.mapsdk → CoverUp.MapSdk). MapConfig is one of them,
        // so its assembly names the whole SDK set without a hardcoded type list — a
        // new SDK component is trusted automatically.
        private static readonly Assembly MapSdkAssembly = typeof(MapConfig).Assembly;

        /// <summary>True if <paramref name="t"/> is legitimate in a map scene: a
        /// Map SDK authoring component, or a stock Unity engine component (assembly
        /// name "Unity…" — UnityEngine.*Module, Unity.RenderPipelines.*,
        /// Unity.TextMeshPro, …). Our own game code (CoverUp.* other than the SDK)
        /// and any shipped third-party plugin component return false.</summary>
        public static bool IsAllowedMapComponent(Type t)
        {
            if (t == null) return false;
            if (t.Assembly == MapSdkAssembly) return true;
            string asm = t.Assembly.GetName().Name;
            return asm != null && asm.StartsWith("Unity", StringComparison.Ordinal);
        }

        /// <summary>Editor/validator view of <see cref="IsAllowedMapComponent"/>:
        /// the disallowed components present in <paramref name="scene"/>, as
        /// human-readable lines ("Full.Type.Name ×count", plus a missing/foreign
        /// script tally), sorted. An empty list means the scene is clean. A missing
        /// script (null component) counts as disallowed: at runtime it rebinds to
        /// nothing and is inert, so shipping one silently breaks the map.</summary>
        public static List<string> DisallowedComponents(Scene scene)
        {
            var counts = new Dictionary<string, int>();
            int missing = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) { missing++; continue; }
                    Type t = mb.GetType();
                    if (IsAllowedMapComponent(t)) continue;
                    string key = t.FullName ?? t.Name;
                    counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
                }
            }
            var lines = new List<string>();
            foreach (KeyValuePair<string, int> kv in counts)
                lines.Add(kv.Value > 1 ? $"{kv.Key} ×{kv.Value}" : kv.Key);
            lines.Sort(StringComparer.Ordinal);
            if (missing > 0)
                lines.Add(missing > 1 ? $"<missing or unresolved script> ×{missing}" : "<missing or unresolved script>");
            return lines;
        }
    }
}
