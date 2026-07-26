using System.Collections.Generic;
using System.Text;
using CoverUp.Core;
using CoverUp.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.EditorTools
{
    /// <summary>
    /// Authoring aids for map size variants (small/medium/large). Preview Size
    /// toggles which variant root is active in the open scene so doors and bounds
    /// can be authored one size at a time; Validate Map checks the _CoverUpMap
    /// contract before a scene ships (or a Workshop creator publishes).
    /// See the map-size-variants design.
    /// </summary>
    public static class MapSizeTools
    {
        // ------------------------------------------------------------- preview

        [MenuItem("Cover Up!/Maps/Preview Size/Base only")]
        private static void PreviewBase() => Preview(null);
        [MenuItem("Cover Up!/Maps/Preview Size/Small")]
        private static void PreviewSmall() => Preview(MapSize.Small);
        [MenuItem("Cover Up!/Maps/Preview Size/Medium")]
        private static void PreviewMedium() => Preview(MapSize.Medium);
        [MenuItem("Cover Up!/Maps/Preview Size/Large")]
        private static void PreviewLarge() => Preview(MapSize.Large);

        // null = base only (all size roots hidden). Records undo and dirties the
        // scene so the previewed state persists (runtime ApplySize overrides it).
        private static void Preview(MapSize? size)
        {
            MapSizeVariants v = OpenSceneVariants();
            if (v == null) return;

            foreach (MapSize s in AllSizes)
            {
                GameObject root = v.Root(s);
                if (root == null) continue;
                bool active = size.HasValue && s == size.Value;
                if (root.activeSelf == active) continue;
                Undo.RecordObject(root, "Preview Map Size");
                root.SetActive(active);
                EditorUtility.SetDirty(root);
            }

            // Keep solo/editor play in step with the preview: a concrete size
            // becomes the soloWalkSize so Walk Through shows the same thing.
            // Base-only is an edit-time view, so it leaves soloWalkSize alone.
            if (size.HasValue)
            {
                var so = new SerializedObject(v);
                so.FindProperty("soloWalkSize").enumValueIndex = (int)size.Value;
                so.ApplyModifiedProperties();
            }
            EditorSceneManager.MarkSceneDirty(v.gameObject.scene);
        }

        // ------------------------------------------------------------ validate

        [MenuItem("Cover Up!/Maps/Validate Map")]
        private static void ValidateMap()
        {
            Scene scene = SceneManager.GetActiveScene();
            var (errors, warnings, kind) = Validate(scene);
            Report(scene, kind, errors, warnings);
        }

        /// <summary>Run the _CoverUpMap contract checks on a scene and return the
        /// findings without any UI — shared by the Validate Map menu and the
        /// Workshop exporter (which aborts on any error). <c>kind</c> is the
        /// one-line verdict header (map type + doll scale).</summary>
        public static (List<string> errors, List<string> warnings, string kind) Validate(Scene scene)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // Map-security allowlist (Docs/Steam.md §8): a map scene may contain
            // only Map SDK authoring components and stock Unity components. Any of
            // our own game components, a shipped plugin's, or a missing/foreign
            // script makes the map ineligible to export or publish — the runtime
            // loader strips them anyway, so shipping one silently breaks the map.
            // Shared policy with MapLoader via MapSceneGuard; refuse it here.
            foreach (string bad in MapSceneGuard.DisallowedComponents(scene))
                errors.Add($"Disallowed component in the map scene: {bad}. Maps may use only Map SDK "
                    + "components and standard Unity components (Docs/Steam.md §8).");

            // MapConfig is the single player-scale source; a gameplay map without
            // one runs at GameScale.Default (allowed, but usually a mistake).
            // Report the resulting doll height and flag near-guard-rail values.
            MapConfig config = FindInScene<MapConfig>(scene);
            if (config == null)
            {
                warnings.Add($"No MapConfig — the map runs at the default player scale " +
                    $"(dolls ≈ {GameScale.ApproxHeightMeters(GameScale.Default):0.00} m). Add one to the _CoverUpMap root.");
            }
            else if (config.PlayerScale <= 0.1f || config.PlayerScale >= 1.9f)
            {
                warnings.Add($"Player scale {config.PlayerScale:0.###} (dolls ≈ " +
                    $"{GameScale.ApproxHeightMeters(config.PlayerScale):0.00} m) sits near the guard rails — intended?");
            }
            string scaleNote = config != null
                ? $", dolls ≈ {GameScale.ApproxHeightMeters(config.PlayerScale):0.00} m"
                : ", default scale";

            MapSizeVariants variants = MapSizeVariants.FindInScene(scene);
            MapSpawnDisc spawn = MapSpawnDisc.FindInScene(scene);
            var bounds = FindAllInScene<MapBoundsVolume>(scene);

            if (variants == null)
            {
                // One-size map: legal. Bounds may live anywhere; just sanity-check
                // the essentials so a plain map still gets a verdict.
                if (spawn == null) errors.Add("No MapSpawnDisc — players have nowhere to land.");
                if (bounds.Count == 0)
                    warnings.Add("No MapBoundsVolume — players are not kept inside the map.");
                return (errors, warnings, "one-size map (no MapSizeVariants)" + scaleNote);
            }

            // Sized map: enforce the container contract.
            var roots = new List<Transform>();
            foreach (MapSize s in AllSizes)
            {
                GameObject r = variants.Root(s);
                if (r != null) roots.Add(r.transform);
            }
            if (roots.Count == 0)
                errors.Add("MapSizeVariants has no size roots assigned — build at least one (Small/Medium/Large).");

            // Every bounds volume must live inside a size root — a volume in Base
            // (or loose) leaks the largest extent into every size (union clamp).
            foreach (MapBoundsVolume b in bounds)
            {
                if (!InsideAnyRoot(b.transform, roots))
                    errors.Add($"MapBoundsVolume '{Path(b.transform)}' is not inside a size root — " +
                               "bounds must live under Small/Medium/Large, never in Base.");
            }

            // Spawn stays shared in Base so every size has it.
            if (spawn == null)
                errors.Add("No MapSpawnDisc — players have nowhere to land.");
            else if (InsideAnyRoot(spawn.transform, roots))
                errors.Add($"MapSpawnDisc '{Path(spawn.transform)}' is inside a size root — " +
                           "the spawn belongs in Base so it exists at every size.");

            // A size root with no bounds of its own is almost always an authoring
            // slip (that size would fall back to whatever volumes happen to be
            // active — i.e. none, no containment).
            foreach (MapSize s in AllSizes)
            {
                GameObject r = variants.Root(s);
                if (r == null) continue;
                if (r.GetComponentsInChildren<MapBoundsVolume>(true).Length == 0)
                    warnings.Add($"Size '{s}' has no MapBoundsVolume — players won't be contained at that size.");
            }

            string built = (variants.BuiltMask & 1) != 0 ? "S" : "-";
            built += (variants.BuiltMask & 2) != 0 ? "M" : "-";
            built += (variants.BuiltMask & 4) != 0 ? "L" : "-";
            return (errors, warnings, $"sized map (built: {built}){scaleNote}");
        }

        // --------------------------------------------------------------- utils

        private static readonly MapSize[] AllSizes = { MapSize.Small, MapSize.Medium, MapSize.Large };

        private static MapSizeVariants OpenSceneVariants()
        {
            MapSizeVariants v = MapSizeVariants.FindInScene(SceneManager.GetActiveScene());
            if (v == null)
                EditorUtility.DisplayDialog("Preview Size",
                    "The open scene has no MapSizeVariants component. Add one to the _CoverUpMap root first.", "OK");
            return v;
        }

        private static bool InsideAnyRoot(Transform t, List<Transform> roots)
        {
            foreach (Transform root in roots)
                if (t.IsChildOf(root)) return true;
            return false;
        }

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (T c in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.gameObject.scene == scene) return c;
            return null;
        }

        private static List<T> FindAllInScene<T>(Scene scene) where T : Component
        {
            var list = new List<T>();
            foreach (T c in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.gameObject.scene == scene) list.Add(c);
            return list;
        }

        private static void Report(Scene scene, string kind, List<string> errors, List<string> warnings)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Validate Map — {scene.name} ({kind})");
            sb.AppendLine();
            if (errors.Count == 0 && warnings.Count == 0)
                sb.AppendLine("✓ No problems found.");
            foreach (string e in errors) sb.AppendLine("✗ " + e);
            foreach (string w in warnings) sb.AppendLine("• " + w);

            string text = sb.ToString();
            if (errors.Count > 0) Debug.LogError(text);
            else if (warnings.Count > 0) Debug.LogWarning(text);
            else Debug.Log(text);

            EditorUtility.DisplayDialog("Validate Map",
                text + (errors.Count > 0 ? "\nSee the Console for the full list." : ""), "OK");
        }
    }
}
