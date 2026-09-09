using System.Collections.Generic;
using System.IO;
using CoverUp.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CoverUp.EditorTools
{
    /// <summary>Which extent of an object a real-world size refers to, in the object's
    /// OWN axes: height is its up, whatever way it is lying in the scene.</summary>
    public enum RealSizeAxis : byte
    {
        Height = 0,
        Width = 1,
        Depth = 2,
        Longest = 3,
    }

    /// <summary>
    /// Give an imported asset its true real-world size.
    ///
    /// The world is 1:1 (one Unity unit is one metre, see <c>GameScale</c>), but nothing
    /// on the asset side enforces that. An FBX carries a unit tag and Unity honours it, so
    /// an FBX modelled at real size arrives at real size; a glTF has no unit tag at all,
    /// the importer takes the numbers as metres, and a photoscan simply dumps whatever the
    /// scanner used. Measured across one map's statues (2026-09-09): the files declared
    /// heights from 0.7 m to 2.7 km for figures that are all roughly life-size. No file
    /// can say what the thing really measures, so the number has to come from the mapper,
    /// once. This does the three things they would otherwise do by hand: measure what the
    /// object currently is, do the division, and remember the answer.
    ///
    /// Measurement is in the root's own axes, so a statue that arrived lying on its side
    /// still reports its upright height rather than the height of the pile it makes on
    /// the floor. It counts everything rendered under the root, plinth included; fit a
    /// child to size the figure alone. The fit is always UNIFORM: a scan's proportions
    /// are the one thing about its size that IS right.
    ///
    /// UI lives in <see cref="MapRealSizeWindow"/>; Validate Map uses
    /// <see cref="TryWorldBounds"/> to flag imports that plainly never got this.
    /// </summary>
    public static class MapRealSize
    {
        /// <summary>Suffix of the prefab variant <see cref="TrySaveAsPrefab"/> writes beside
        /// the source asset, so every later drag-in is already the right size.</summary>
        public const string VariantSuffix = "_RealSize";

        /// <summary>Where a variant goes when the object has no source asset to sit beside
        /// (a group built in the scene). Matches the example builder's neutral folder.</summary>
        private const string FallbackFolder = "Assets/Maps/Prefabs";

        /// <summary>Does this renderer count toward an object's size? Particles, trails
        /// and lines have no authored size, and anything tagged EditorOnly never ships.</summary>
        public static bool Counts(Renderer r)
        {
            if (r == null) return false;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) return false;
            for (Transform t = r.transform; t != null; t = t.parent)
                if (t.CompareTag(MapReferenceDoll.EditorOnlyTag)) return false;
            return true;
        }

        /// <summary>Size of everything rendered under <paramref name="root"/>, in metres,
        /// along the root's own axes: x is width, y is height, z is depth. False when
        /// nothing under it renders.</summary>
        public static bool TryMeasure(Transform root, out Vector3 size)
        {
            size = Vector3.zero;
            if (root == null) return false;

            Quaternion toRoot = Quaternion.Inverse(root.rotation);
            Vector3 origin = root.position;
            var local = new Bounds();
            bool any = false;
            var corners = new Vector3[8];

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!Counts(r) || !TryCorners(r, corners)) continue;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = toRoot * (corners[i] - origin);
                    if (!any) { local = new Bounds(p, Vector3.zero); any = true; }
                    else local.Encapsulate(p);
                }
            }

            if (!any) return false;
            size = local.size;
            return true;
        }

        /// <summary>The one number a size refers to.</summary>
        public static float Extent(Vector3 size, RealSizeAxis axis)
        {
            switch (axis)
            {
                case RealSizeAxis.Width: return size.x;
                case RealSizeAxis.Depth: return size.z;
                case RealSizeAxis.Longest: return Mathf.Max(size.x, size.y, size.z);
                default: return size.y;
            }
        }

        /// <summary>Scale <paramref name="root"/> uniformly so its extent along
        /// <paramref name="axis"/> becomes <paramref name="metres"/>. Undoable. Returns the
        /// factor applied, or 0 when there was nothing to measure or nothing to do.</summary>
        public static float Fit(Transform root, RealSizeAxis axis, float metres)
        {
            if (root == null || metres <= 0f || !TryMeasure(root, out Vector3 size)) return 0f;
            float current = Extent(size, axis);
            if (current <= 1e-5f) return 0f;

            float k = metres / current;
            if (Mathf.Approximately(k, 1f)) return 0f;

            Undo.RecordObject(root, "Fit Real Size");
            root.localScale *= k;
            if (PrefabUtility.IsPartOfPrefabInstance(root))
                PrefabUtility.RecordPrefabInstancePropertyModifications(root);
            if (root.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            return k;
        }

        /// <summary>
        /// Keep a fitted object's size for every future drag-in. A scene object that is an
        /// instance of a model or prefab becomes a <b>prefab variant</b> beside its source,
        /// named <c>&lt;source&gt;_RealSize</c>, and the scene object is connected to it. That
        /// works the same for FBX and glTF: the glTF importer has no scale setting, so a
        /// variant is the only place a glTF's scale can live. An instance of an existing
        /// <c>_RealSize</c> variant applies its overrides to that variant instead of nesting a
        /// second one. A plain scene object becomes a regular prefab under
        /// <c>Assets/Maps/Prefabs</c>.
        /// </summary>
        public static bool TrySaveAsPrefab(GameObject root, out string path, out string problem)
        {
            path = null;
            problem = null;
            if (root == null) { problem = "Nothing selected."; return false; }
            if (!root.scene.IsValid())
            {
                problem = $"'{root.name}' is an asset, not a scene object. Drag it into the scene first.";
                return false;
            }
            if (PrefabUtility.IsPartOfPrefabInstance(root) && !PrefabUtility.IsAnyPrefabInstanceRoot(root))
            {
                problem = $"'{root.name}' is inside a prefab instance. Select the instance root to save a variant " +
                          "(the fit itself is fine on a child).";
                return false;
            }

            string source = PrefabUtility.IsAnyPrefabInstanceRoot(root)
                ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root)
                : null;

            // Already our variant: update it in place rather than stacking another.
            if (!string.IsNullOrEmpty(source) &&
                Path.GetFileNameWithoutExtension(source).EndsWith(VariantSuffix))
            {
                PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);
                path = source;
                return true;
            }

            string dir;
            string name;
            if (!string.IsNullOrEmpty(source))
            {
                dir = Path.GetDirectoryName(source)?.Replace('\\', '/');
                name = Path.GetFileNameWithoutExtension(source);
            }
            else
            {
                dir = FallbackFolder;
                name = root.name;
                EnsureFolder(dir);
            }

            path = $"{dir}/{name}{VariantSuffix}.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.UserAction, out bool ok);
            if (!ok)
            {
                problem = $"Unity could not save '{path}'. See the Console.";
                path = null;
                return false;
            }
            return true;
        }

        // World-space corners of one renderer's authored box. From the mesh, not
        // Renderer.bounds: an inactive renderer's bounds read as empty, and a mesh box
        // transformed by the object's own matrix is tight where a world AABB is loose.
        private static bool TryCorners(Renderer r, Vector3[] corners)
        {
            Bounds b;
            Matrix4x4 m;
            switch (r)
            {
                case MeshRenderer _:
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) return false;
                    b = mf.sharedMesh.bounds;
                    m = r.transform.localToWorldMatrix;
                    break;
                case SkinnedMeshRenderer smr:
                    // Bind-pose box. Props are what this measures; a posed character
                    // would need its bones, and the reference dolls cover that need.
                    if (smr.sharedMesh == null) return false;
                    b = smr.sharedMesh.bounds;
                    m = smr.transform.localToWorldMatrix;
                    break;
                default:
                    b = r.bounds;
                    m = Matrix4x4.identity;
                    break;
            }

            Vector3 c = b.center;
            Vector3 h = b.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    c.x + ((i & 1) == 0 ? -h.x : h.x),
                    c.y + ((i & 2) == 0 ? -h.y : h.y),
                    c.z + ((i & 4) == 0 ? -h.z : h.z));
                corners[i] = m.MultiplyPoint3x4(corner);
            }
            return true;
        }

        /// <summary>World-space AABB of one renderer's authored box, valid for inactive
        /// objects too. Validate Map's view of a size.</summary>
        public static bool TryWorldBounds(Renderer r, out Bounds bounds)
        {
            bounds = default;
            var corners = new Vector3[8];
            if (!TryCorners(r, corners)) return false;
            bounds = new Bounds(corners[0], Vector3.zero);
            for (int i = 1; i < 8; i++) bounds.Encapsulate(corners[i]);
            return true;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && parent != folder) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
