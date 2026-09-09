using System.Collections.Generic;
using System.IO;
using CoverUp.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CoverUp.EditorTools
{
    /// <summary>Which extent of an object a real-world size refers to.</summary>
    public enum RealSizeAxis : byte
    {
        Height = 0,
        Width = 1,
        Depth = 2,
        Longest = 3,
    }

    /// <summary>
    /// Which frame a size is measured in. <see cref="World"/> is the default: height is
    /// the scene's up, the way the mapper sees the object. <see cref="Object"/> is the
    /// root's own axes, for a prop deliberately laid on its side whose upright height
    /// should still read. The first version measured in object axes only, and a Z-up
    /// photoscan stood up by a -90° root rotation then reported its nose-to-tail length
    /// as "height" (the lion of canvas-chaos, 2026-09-09).
    /// </summary>
    public enum RealSizeFrame : byte
    {
        World = 0,
        Object = 1,
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
    /// It counts everything rendered under the root, plinth included; fit a child to size
    /// the figure alone. The fit is always UNIFORM: a scan's proportions are the one
    /// thing about its size that IS right.
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

        /// <summary>Size of everything rendered under <paramref name="root"/>, in metres:
        /// x is width, y is height, z is depth, in the chosen <paramref name="frame"/>.
        /// False when nothing under it renders.</summary>
        public static bool TryMeasure(Transform root, RealSizeFrame frame, out Vector3 size)
        {
            size = Vector3.zero;
            if (root == null) return false;

            Quaternion toFrame = frame == RealSizeFrame.Object
                ? Quaternion.Inverse(root.rotation)
                : Quaternion.identity;
            Vector3 origin = root.position;
            var local = new Bounds();
            bool any = false;
            var corners = new Vector3[8];

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!Counts(r) || !TryCorners(r, corners)) continue;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = toFrame * (corners[i] - origin);
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
        /// <paramref name="axis"/>, measured in <paramref name="frame"/>, becomes
        /// <paramref name="metres"/>. Undoable. Returns the factor applied, or 0 when there
        /// was nothing to measure or nothing to do.</summary>
        public static float Fit(Transform root, RealSizeAxis axis, float metres, RealSizeFrame frame)
        {
            if (root == null || metres <= 0f || !TryMeasure(root, frame, out Vector3 size)) return 0f;
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

        /// <summary>Degrees between the root's own up and the world's: 0 for an upright
        /// root, about 90 for a Z-up scan stood up by rotating its root.</summary>
        public static float TiltDegrees(Transform root) =>
            root == null ? 0f : Vector3.Angle(root.up, Vector3.up);

        /// <summary>
        /// Move a root's tilt into its children, so the root's own up becomes the world's
        /// up while NOTHING moves on screen. Yaw is kept: a statue turned 30° in the room
        /// stays turned 30°. After this, Object and World measurements agree on height,
        /// physics and any tool that trusts the root's up read the object the way it looks,
        /// and the -90° import fix stops living on the root.
        ///
        /// Refuses a root with nothing under it to carry the rotation (a mesh sitting on
        /// the root itself needs an empty parent first), and a root or ancestor with
        /// non-uniform scale, which would skew the children when the root turns.
        /// </summary>
        public static bool TryMakeUpright(Transform root, out string problem)
        {
            problem = null;
            if (root == null) { problem = "Nothing selected."; return false; }
            if (TiltDegrees(root) < 0.01f)
            {
                problem = $"'{root.name}' is already upright.";
                return false;
            }
            if (root.childCount == 0)
            {
                problem = $"'{root.name}' has nothing under it to carry the rotation: its mesh sits on " +
                          "the root itself. Put it under an empty parent and make that upright instead.";
                return false;
            }
            if (!IsUniform(root.localScale) || (root.parent != null && !IsUniform(root.parent.lossyScale)))
            {
                problem = $"'{root.name}' or a parent has non-uniform scale, which would skew its children " +
                          "when the root turns. Make the scale uniform first.";
                return false;
            }

            // The new frame: world up, and the yaw the root already has. Forward is the
            // root's forward flattened onto the floor; when that is vertical (the classic
            // -90° X fix points forward straight up), the flattened right decides instead.
            Vector3 fwd = Flat(root.forward);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.Cross(Flat(root.right), Vector3.up);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            Quaternion upright = Quaternion.LookRotation(fwd.normalized, Vector3.up);

            int n = root.childCount;
            var recorded = new List<Object>(n + 1) { root };
            var pos = new Vector3[n];
            var rot = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                Transform c = root.GetChild(i);
                recorded.Add(c);
                pos[i] = c.position;
                rot[i] = c.rotation;
            }
            Undo.RecordObjects(recorded.ToArray(), "Make Upright");

            root.rotation = upright;
            for (int i = 0; i < n; i++) root.GetChild(i).SetPositionAndRotation(pos[i], rot[i]);

            if (PrefabUtility.IsPartOfPrefabInstance(root))
                foreach (Object o in recorded) PrefabUtility.RecordPrefabInstancePropertyModifications(o);
            if (root.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            return true;
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

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static bool IsUniform(Vector3 s) =>
            Mathf.Abs(s.x - s.y) < 1e-4f && Mathf.Abs(s.y - s.z) < 1e-4f;

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && parent != folder) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
