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
    /// The GameObject names the <c>_CoverUpMap</c> contract reserves. One place,
    /// because three tools spell them: the example builder creates them, Validate
    /// Map enforces them, and Group Base migrates a scene into them.
    /// </summary>
    public static class MapContract
    {
        public const string Root = "_CoverUpMap";
        public const string Base = "Base";
        public const string Fixtures = "Fixtures";
        public const string Content = "Content";
        public const string Sizes = "Sizes";

        /// <summary>Authoring aids that never ship: the scale reference dolls
        /// (<see cref="MapReferenceDoll"/>), tagged EditorOnly so the build strips the
        /// whole subtree. A sibling of Base rather than a group inside it, because
        /// everything under Base is map content that DOES ship.</summary>
        public const string Reference = "Reference";

        /// <summary>True if this object is an editor-only authoring aid — a reference
        /// doll, or the group they live in. The contract rules skip these: they're
        /// stripped at export, so where they sit in the hierarchy cannot affect the
        /// shipped map, and forcing a mapper to file them tidily would be theatre.</summary>
        public static bool IsAuthoringAid(Transform t)
        {
            if (t == null) return false;
            if (t.name == Reference) return true;
            return t.GetComponentInChildren<MapReferenceDoll>(true) != null;
        }

        /// <summary>The scene's contract root, or null for a scene that predates
        /// the shape (the game's own box_* maps are flat, for instance).</summary>
        public static Transform FindRoot(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == Root) return go.transform;
            return null;
        }

        public static Transform FindChild(Transform parent, string name)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c.name == name) return c;
            }
            return null;
        }

        /// <summary>Direct children of <paramref name="parent"/> as a snapshot —
        /// safe to re-parent while iterating.</summary>
        public static List<Transform> Children(Transform parent)
        {
            var list = new List<Transform>();
            if (parent == null) return list;
            for (int i = 0; i < parent.childCount; i++) list.Add(parent.GetChild(i));
            return list;
        }
    }

    /// <summary>
    /// One-shot migration into the Base grouping contract (SDK 0.4.0):
    ///
    ///   _CoverUpMap
    ///   ├── Base
    ///   │   ├── Fixtures     spawn disc, the arena sun, bounds on a one-size map
    ///   │   └── Content      your geometry and props
    ///   └── Sizes            size-variant roots (bounds + doors), untouched
    ///
    /// The split exists so the handful of objects a map CANNOT lose are visibly
    /// apart from the thousands a mapper freely rebuilds — deleting the Content
    /// group and starting over is a normal move; deleting Fixtures is what breaks
    /// a map. Validate Map errors on a scene that isn't in this shape, and this
    /// menu item is the fix: it never deletes anything, only re-parents.
    /// </summary>
    public static class MapBaseGrouping
    {
        [MenuItem("Cover Up!/Maps/Group Base")]
        private static void GroupOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            var log = new List<string>();
            int moved = Group(scene, log);

            var sb = new StringBuilder($"Group Base — {scene.name}\n\n");
            if (log.Count == 0) sb.AppendLine("Already in the contract shape — nothing to do.");
            foreach (string line in log) sb.AppendLine("• " + line);
            if (moved > 0)
                sb.AppendLine("\nScene marked dirty — save it, then run Validate Map.");

            Debug.Log("[CoverUp] " + sb);
            EditorUtility.DisplayDialog("Group Base", sb.ToString(), "OK");
        }

        /// <summary>
        /// Headless entry, for migrating a map without opening the editor:
        /// <c>Unity -batchmode -quit -nographics -projectPath &lt;proj&gt;
        /// -executeMethod CoverUp.EditorTools.MapBaseGrouping.RunHeadless
        /// -mapScene Assets/…/my_map.unity</c>. Saves the scene, then runs
        /// Validate Map and exits non-zero if the scene still has errors.
        /// </summary>
        public static void RunHeadless()
        {
            try
            {
                string path = null;
                string[] args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-mapScene") path = args[i + 1];
                if (string.IsNullOrEmpty(path))
                    throw new System.Exception("pass -mapScene <path to the map scene>");

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var log = new List<string>();
                int moved = Group(scene, log);
                foreach (string line in log) Debug.Log("[CoverUp] Group Base: " + line);
                Debug.Log($"[CoverUp] Group Base: moved {moved} object(s) in {path}");
                EditorSceneManager.SaveScene(scene, path);

                var (errors, warnings, kind) = MapSizeTools.Validate(scene);
                Debug.Log($"[CoverUp] Validate Map — {kind}");
                foreach (string w in warnings) Debug.Log("[CoverUp] • " + w);
                foreach (string e in errors) Debug.LogError("[CoverUp] ✗ " + e);
                Debug.Log(errors.Count == 0 ? "[CoverUp] Group Base: PASS" : "[CoverUp] Group Base: FAIL");
                EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[CoverUp] Group Base failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Put <paramref name="scene"/> into the contract shape, creating the
        /// groups that are missing and re-parenting what sits in the wrong place.
        /// Returns the number of objects moved; <paramref name="log"/> collects a
        /// human-readable account. Fully undoable.
        /// </summary>
        public static int Group(Scene scene, List<string> log)
        {
            if (!scene.IsValid() || !scene.isLoaded) return 0;
            int moved = 0;

            // ---- the root ------------------------------------------------------
            Transform root = MapContract.FindRoot(scene);
            if (root == null)
            {
                var rootGo = new GameObject(MapContract.Root);
                SceneManager.MoveGameObjectToScene(rootGo, scene);
                Undo.RegisterCreatedObjectUndo(rootGo, "Group Base");
                root = rootGo.transform;
                log.Add($"Created '{MapContract.Root}'.");
            }

            // Anything still loose at the scene root belongs in the map — a flat
            // legacy scene brings everything, and even a contract-shaped scene
            // usually has the sun sitting outside (that's where NewScene puts it).
            // Snapshot first: re-parenting mutates the scene's root list.
            int adopted = 0;
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.transform == root) continue;
                Undo.SetTransformParent(go.transform, root, true, "Group Base");
                adopted++;
            }
            if (adopted > 0)
            {
                moved += adopted;
                log.Add($"Moved {adopted} loose root object(s) into '{MapContract.Root}'.");
            }

            // ---- Base / Fixtures / Content -------------------------------------
            Transform baseT = Ensure(root, MapContract.Base, log);
            Transform fixtures = Ensure(baseT, MapContract.Fixtures, log);
            Transform content = Ensure(baseT, MapContract.Content, log);

            // ---- size roots go under Sizes, never under Base --------------------
            MapSizeVariants variants = MapSizeVariants.FindInScene(scene);
            var sizeRoots = new List<Transform>();
            if (variants != null)
            {
                foreach (MapSize s in new[] { MapSize.Small, MapSize.Medium, MapSize.Large })
                {
                    GameObject r = variants.Root(s);
                    if (r != null && r.scene == scene) sizeRoots.Add(r.transform);
                }
            }
            if (sizeRoots.Count > 0)
            {
                Transform sizes = Ensure(root, MapContract.Sizes, log);
                int movedSizes = 0;
                foreach (Transform sr in sizeRoots)
                {
                    if (sr.parent == sizes) continue;
                    Undo.SetTransformParent(sr, sizes, true, "Group Base");
                    movedSizes++;
                }
                if (movedSizes > 0)
                {
                    moved += movedSizes;
                    log.Add($"Moved {movedSizes} size root(s) under '{MapContract.Sizes}'.");
                }
            }

            // ---- everything else under the root belongs in Base -----------------
            int intoBase = 0;
            foreach (Transform child in MapContract.Children(root))
            {
                if (child == baseT) continue;
                if (child.name == MapContract.Sizes) continue;
                if (sizeRoots.Contains(child)) continue;
                // Reference dolls stay put: sweeping them into Base would file an
                // editor-only aid among the objects that ship.
                if (MapContract.IsAuthoringAid(child)) continue;
                Undo.SetTransformParent(child, baseT, true, "Group Base");
                intoBase++;
            }
            if (intoBase > 0)
            {
                moved += intoBase;
                log.Add($"Moved {intoBase} object(s) from '{MapContract.Root}' into '{MapContract.Base}'.");
            }

            // ---- split Base's children between Fixtures and Content -------------
            int toFixtures = 0, toContent = 0;
            foreach (Transform child in MapContract.Children(baseT))
            {
                if (child == fixtures || child == content) continue;
                if (sizeRoots.Contains(child)) continue; // already handled above
                bool fixture = IsFixture(child);
                Undo.SetTransformParent(child, fixture ? fixtures : content, true, "Group Base");
                if (fixture) toFixtures++; else toContent++;
            }
            if (toFixtures + toContent > 0)
            {
                moved += toFixtures + toContent;
                log.Add($"Sorted {toFixtures} object(s) into '{MapContract.Fixtures}' and " +
                        $"{toContent} into '{MapContract.Content}'.");
            }

            // The contract components belong on the root itself. Moving components
            // between objects is the mapper's call (references would follow), so
            // this only says so.
            MapConfig cfg = FindInScene<MapConfig>(scene);
            if (cfg != null && cfg.transform != root)
                log.Add($"MapConfig sits on '{cfg.name}', not on the '{MapContract.Root}' root — move it yourself.");

            if (moved > 0) EditorSceneManager.MarkSceneDirty(scene);
            return moved;
        }

        // What can't be deleted without breaking the map: the spawn, the lighting,
        // and (on a one-size map, where they don't live in a size root) the bounds
        // volumes. Everything else is the mapper's own content.
        private static bool IsFixture(Transform t) =>
            t.GetComponentInChildren<MapSpawnDisc>(true) != null
            || t.GetComponentInChildren<MapBoundsVolume>(true) != null
            || t.GetComponentInChildren<Light>(true) != null;

        private static Transform Ensure(Transform parent, string name, List<string> log)
        {
            Transform existing = MapContract.FindChild(parent, name);
            if (existing != null) return existing;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Group Base");
            Undo.SetTransformParent(go.transform, parent, false, "Group Base");
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            log.Add($"Created '{parent.name}/{name}'.");
            return go.transform;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (T c in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.gameObject.scene == scene) return c;
            return null;
        }
    }
}
