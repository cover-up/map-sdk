using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// The playable space as ONE question: the union of every active <see cref="MapBoundsVolume"/>
    /// (box) and <see cref="MapBoundsPolygon"/> (drawn outline) in the active scene. Everything that
    /// used to ask the volumes directly asks here, so boxes and outlines can mix in one map.
    /// </summary>
    public static class MapBounds
    {
        private static readonly List<Matrix4x4> BoxWorldToLocal = new List<Matrix4x4>();
        private static readonly List<Vector3> BoxAxes = new List<Vector3>();
        private static readonly List<MapBoundsPolygon> Polys = new List<MapBoundsPolygon>();
        private static int _frame = -1;

        /// <summary>Refresh the active set, at most once per frame in play.</summary>
        public static void Collect()
        {
            int frame = Application.isPlaying ? Time.frameCount : -2;
            if (frame == _frame && frame != -2) return;
            _frame = frame;
            MapBoundsVolume.CollectActive(BoxWorldToLocal, BoxAxes);
            MapBoundsPolygon.CollectActive(Polys);
        }

        /// <summary>Force the next query to re-read the live set (after enabling or disabling bounds this frame).</summary>
        public static void Invalidate() => _frame = -1;

        public static bool Any { get { Collect(); return BoxWorldToLocal.Count > 0 || Polys.Count > 0; } }
        public static int BoxCount { get { Collect(); return BoxWorldToLocal.Count; } }
        public static int PolygonCount { get { Collect(); return Polys.Count; } }
        public static IReadOnlyList<Matrix4x4> Boxes { get { Collect(); return BoxWorldToLocal; } }
        public static IReadOnlyList<Vector3> BoxAxisLengths { get { Collect(); return BoxAxes; } }
        public static IReadOnlyList<MapBoundsPolygon> Polygons { get { Collect(); return Polys; } }

        /// <summary>True when the point lies inside any active bounds, with <paramref name="slack"/>
        /// metres of tolerance on every face.</summary>
        public static bool Contains(Vector3 world, float slack = 0f)
        {
            Collect();
            for (int i = 0; i < BoxWorldToLocal.Count; i++)
            {
                Vector3 local = BoxWorldToLocal[i].MultiplyPoint3x4(world);
                Vector3 axes = BoxAxes[i];
                if ((Mathf.Abs(local.x) - 0.5f) * axes.x <= slack
                    && (Mathf.Abs(local.y) - 0.5f) * axes.y <= slack
                    && (Mathf.Abs(local.z) - 0.5f) * axes.z <= slack) return true;
            }
            for (int i = 0; i < Polys.Count; i++) if (Polys[i].Contains(world, slack)) return true;
            return false;
        }

        /// <summary>The nearest point inside the union; false when the point already is inside
        /// (or there are no bounds). <paramref name="inset"/> and <paramref name="headroom"/> pull
        /// the result in from the faces for a body's half width and height.</summary>
        public static bool TryClamp(Vector3 world, out Vector3 clamped, float inset = 0f, float headroom = 0f)
        {
            clamped = world;
            Collect();
            if (BoxWorldToLocal.Count == 0 && Polys.Count == 0) return false;
            if (inset <= 0f && headroom <= 0f && Contains(world)) return false;
            float best = float.MaxValue;
            for (int i = 0; i < BoxWorldToLocal.Count; i++)
            {
                Vector3 axes = BoxAxes[i];
                float hx = Mathf.Max(0f, 0.5f - inset / Mathf.Max(axes.x, 1e-4f));
                float hz = Mathf.Max(0f, 0.5f - inset / Mathf.Max(axes.z, 1e-4f));
                float top = Mathf.Max(-0.5f, 0.5f - headroom / Mathf.Max(axes.y, 1e-4f));
                Vector3 local = BoxWorldToLocal[i].MultiplyPoint3x4(world);
                var inside = new Vector3(Mathf.Clamp(local.x, -hx, hx), Mathf.Clamp(local.y, -0.5f, top), Mathf.Clamp(local.z, -hz, hz));
                if (inside == local) return false;
                Vector3 w = BoxWorldToLocal[i].inverse.MultiplyPoint3x4(inside);
                float d = (w - world).sqrMagnitude;
                if (d < best) { best = d; clamped = w; }
            }
            for (int i = 0; i < Polys.Count; i++)
            {
                Vector3 w = Polys[i].Clamp(world, inset, headroom);
                float d = (w - world).sqrMagnitude;
                if (d < 1e-10f) { clamped = world; return false; }
                if (d < best) { best = d; clamped = w; }
            }
            return best != float.MaxValue;
        }

        /// <summary>How far outside the union a point is, 0 inside.</summary>
        public static float DistanceOutside(Vector3 world)
            => TryClamp(world, out Vector3 c) ? Vector3.Distance(world, c) : 0f;

        /// <summary>World-space box around every active bounds, for a bake over the whole set.</summary>
        public static bool WorldBounds(out Bounds bounds)
        {
            Collect();
            bounds = default; bool any = false;
            for (int i = 0; i < BoxWorldToLocal.Count; i++)
            {
                Matrix4x4 l2w = BoxWorldToLocal[i].inverse;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = l2w.MultiplyPoint3x4(new Vector3((c & 1) == 0 ? -0.5f : 0.5f, (c & 2) == 0 ? -0.5f : 0.5f, (c & 4) == 0 ? -0.5f : 0.5f));
                    if (!any) { bounds = new Bounds(corner, Vector3.zero); any = true; } else bounds.Encapsulate(corner);
                }
            }
            for (int i = 0; i < Polys.Count; i++)
            {
                Bounds b = Polys[i].WorldBounds();
                if (!any) { bounds = b; any = true; } else bounds.Encapsulate(b);
            }
            return any;
        }

        /// <summary>A fingerprint of the active set's shapes: when it changes, a bake of them is stale.</summary>
        public static int ShapeHash()
        {
            Collect();
            unchecked
            {
                int h = BoxWorldToLocal.Count * 7919 + Polys.Count * 104729;
                for (int i = 0; i < BoxWorldToLocal.Count; i++) h = h * 31 + BoxWorldToLocal[i].GetHashCode();
                for (int i = 0; i < Polys.Count; i++) h = h * 31 + Polys[i].ShapeHash();
                return h;
            }
        }
    }
}
