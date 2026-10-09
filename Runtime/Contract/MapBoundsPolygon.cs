using System.Collections.Generic;
using UnityEngine;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// A keep-in boundary drawn as a closed outline: a fence the mapper traces by hand (or
    /// traces from the terrain) rather than a box. The playable space is the union of every
    /// active <see cref="MapBoundsVolume"/> and <see cref="MapBoundsPolygon"/>, so the two mix:
    /// boxes for rooms and corridors, outlines for a coast that is nothing like a rectangle.
    ///
    /// Points are in this object's local XZ plane; the fence stands between <see cref="Floor"/>
    /// and <see cref="Ceiling"/> (local Y). Move, rotate and scale the object like a volume.
    /// Winding does not matter. The outline must not cross itself; Validate Map says when it
    /// does. Lives under a size root like every bounds volume (see <see cref="MapSizeVariants"/>).
    ///
    /// Asked for by Pål (2026-10-10) for the island maps: "the boundaries about a metre into the
    /// ocean, so we could manually draw that".
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapBoundsPolygon : MonoBehaviour
    {
        // Live outlines, maintained by enable/disable like the volumes.
        private static readonly List<MapBoundsPolygon> Polygons = new List<MapBoundsPolygon>();

        [SerializeField]
        [Tooltip("The fence, as corners in this object's local X/Z plane, in order around the outline. Closed automatically.")]
        private List<Vector2> points = new List<Vector2>
        {
            new Vector2(-10f, -10f), new Vector2(10f, -10f), new Vector2(10f, 10f), new Vector2(-10f, 10f),
        };

        [SerializeField]
        [Tooltip("Bottom of the fence, local Y. Put it a little under the lowest floor players can reach.")]
        private float floor = -2f;

        [SerializeField]
        [Tooltip("Top of the fence, local Y. As tall as the highest thing players can climb to, plus a jump.")]
        private float ceiling = 30f;

        public IReadOnlyList<Vector2> Points => points;
        public float Floor => floor;
        public float Ceiling => ceiling;

        /// <summary>Editor-side write access (the inspector's handles and the tracer).</summary>
        public void SetPoints(List<Vector2> newPoints) { points = newPoints; _worldFrame = -1; }
        public void SetHeights(float newFloor, float newCeiling) { floor = newFloor; ceiling = newCeiling; _worldFrame = -1; }

        private void OnEnable() { Polygons.Add(this); _worldFrame = -1; }
        private void OnDisable() => Polygons.Remove(this);

        public static int ActiveCount => Polygons.Count;

        /// <summary>Every live outline in the active scene, in registration order.</summary>
        public static void CollectActive(List<MapBoundsPolygon> into)
        {
            into.Clear();
            if (Polygons.Count == 0) return;
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (MapBoundsPolygon p in Polygons)
                if (p.gameObject.scene == active && p.points.Count >= 3) into.Add(p);
        }

        // ------------------------------------------------------------------ world-space cache
        // The outline in world XZ plus the fence heights, refreshed once per frame on first use
        // (the body clamp asks many times per frame). Outside play (editor tools) it refreshes
        // on every call, since frames do not advance.
        private readonly List<Vector2> _world = new List<Vector2>();
        private float _worldFloor, _worldCeiling;
        private int _worldFrame = -1;

        private void RefreshWorld()
        {
            int frame = Application.isPlaying ? Time.frameCount : -2;
            if (frame == _worldFrame && frame != -2) return;
            _worldFrame = frame;
            _world.Clear();
            Transform t = transform;
            foreach (Vector2 p in points)
            {
                Vector3 w = t.TransformPoint(new Vector3(p.x, 0f, p.y));
                _world.Add(new Vector2(w.x, w.z));
            }
            _worldFloor = t.TransformPoint(new Vector3(0f, floor, 0f)).y;
            _worldCeiling = t.TransformPoint(new Vector3(0f, ceiling, 0f)).y;
            if (_worldCeiling < _worldFloor) { float s = _worldFloor; _worldFloor = _worldCeiling; _worldCeiling = s; }
        }

        /// <summary>The outline's corners in world XZ (read-only snapshot for drawing).</summary>
        public void GetWorldOutline(List<Vector2> into, out float worldFloor, out float worldCeiling)
        {
            RefreshWorld();
            into.Clear(); into.AddRange(_world);
            worldFloor = _worldFloor; worldCeiling = _worldCeiling;
        }

        // ------------------------------------------------------------------ geometry
        /// <summary>True when the world point's XZ lies inside the outline (even-odd rule).</summary>
        public bool ContainsXZ(float x, float z)
        {
            RefreshWorld();
            return ContainsXZ(_world, x, z);
        }

        public static bool ContainsXZ(List<Vector2> outline, float x, float z)
        {
            bool inside = false; int n = outline.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 a = outline[i], b = outline[j];
                if ((a.y > z) != (b.y > z) && x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        /// <summary>Nearest point on the outline to a world XZ, and its distance.</summary>
        public float ClosestOnOutline(float x, float z, out Vector2 closest)
        {
            RefreshWorld();
            return ClosestOnOutline(_world, x, z, out closest);
        }

        public static float ClosestOnOutline(List<Vector2> outline, float x, float z, out Vector2 closest)
        {
            var p = new Vector2(x, z); float best = float.MaxValue; closest = p; int n = outline.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 a = outline[j], b = outline[i], ab = b - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
                Vector2 c = a + ab * t; float d = (p - c).sqrMagnitude;
                if (d < best) { best = d; closest = c; }
            }
            return Mathf.Sqrt(best);
        }

        /// <summary>Signed distance in XZ: negative inside the outline, positive outside.</summary>
        public float SignedDistanceXZ(float x, float z)
        {
            float d = ClosestOnOutline(x, z, out _);
            return ContainsXZ(x, z) ? -d : d;
        }

        /// <summary>True when the world point lies inside the fence, with <paramref name="slack"/>
        /// metres of tolerance on every face (so a body resting on the fence counts as inside).</summary>
        public bool Contains(Vector3 world, float slack = 0f)
        {
            RefreshWorld();
            if (world.y < _worldFloor - slack || world.y > _worldCeiling + slack) return false;
            if (ContainsXZ(world.x, world.z)) return true;
            return slack > 0f && ClosestOnOutline(world.x, world.z, out _) <= slack;
        }

        /// <summary>The nearest point inside the fence to a world point (the point itself when inside).
        /// <paramref name="inset"/> pulls the result that far in from the outline, for a body's half width;
        /// <paramref name="headroom"/> keeps that much under the ceiling, for its height.</summary>
        public Vector3 Clamp(Vector3 world, float inset = 0f, float headroom = 0f)
        {
            RefreshWorld();
            float y = Mathf.Clamp(world.y, _worldFloor, Mathf.Max(_worldFloor, _worldCeiling - headroom));
            float dist = ClosestOnOutline(world.x, world.z, out Vector2 c);
            bool inside = ContainsXZ(world.x, world.z);
            if (inside && (inset <= 0f || dist >= inset)) return new Vector3(world.x, y, world.z);
            if (inset <= 0f) return new Vector3(c.x, y, c.y);
            // step in from the outline along the direction that leads inside
            Vector2 p = new Vector2(world.x, world.z);
            Vector2 away = inside ? (p - c) : (c - p);
            if (away.sqrMagnitude < 1e-10f) away = EdgeNormalAt(c);
            away.Normalize();
            Vector2 a = c + away * inset, b = c - away * inset;
            Vector2 pick = ContainsXZ(a.x, a.y) ? a : (ContainsXZ(b.x, b.y) ? b : c);
            return new Vector3(pick.x, y, pick.y);
        }

        // A perpendicular to the edge the point sits on (either side; the caller tests which is inside).
        private Vector2 EdgeNormalAt(Vector2 c)
        {
            int n = _world.Count; float best = float.MaxValue; Vector2 normal = Vector2.right;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 a = _world[j], b = _world[i], ab = b - a; float len2 = ab.sqrMagnitude; if (len2 < 1e-8f) continue;
                float t = Mathf.Clamp01(Vector2.Dot(c - a, ab) / len2); float d = (c - (a + ab * t)).sqrMagnitude;
                if (d < best) { best = d; normal = new Vector2(-ab.y, ab.x).normalized; }
            }
            return normal;
        }

        /// <summary>World-space box around the fence (for the clay filter's bake and the validator).</summary>
        public Bounds WorldBounds()
        {
            RefreshWorld();
            var b = new Bounds(new Vector3(_world[0].x, (_worldFloor + _worldCeiling) * 0.5f, _world[0].y), Vector3.zero);
            foreach (Vector2 p in _world) { b.Encapsulate(new Vector3(p.x, _worldFloor, p.y)); b.Encapsulate(new Vector3(p.x, _worldCeiling, p.y)); }
            return b;
        }

        /// <summary>A cheap fingerprint of the fence's world shape, for callers that cache a bake of it.</summary>
        public int ShapeHash()
        {
            RefreshWorld();
            unchecked
            {
                int h = _world.Count * 397 ^ _worldFloor.GetHashCode() ^ (_worldCeiling.GetHashCode() * 31);
                foreach (Vector2 p in _world) h = h * 31 + p.GetHashCode();
                return h;
            }
        }

        // ------------------------------------------------------------------ scene view
        // Scene-view aid while drawing. Never in play: the Game view's Gizmos toggle would paint it into the
        // game, and the `bounds` console command is the way to see the fence there.
        private void OnDrawGizmos()
        {
            if (!MapBoundsVolume.ShowGizmos || Application.isPlaying || points.Count < 2) return;
            _worldFrame = -1; RefreshWorld();
            int n = _world.Count;
            Gizmos.color = new Color(1f, 0.45f, 0.15f, 0.8f);
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var a0 = new Vector3(_world[j].x, _worldFloor, _world[j].y); var b0 = new Vector3(_world[i].x, _worldFloor, _world[i].y);
                var a1 = new Vector3(_world[j].x, _worldCeiling, _world[j].y); var b1 = new Vector3(_world[i].x, _worldCeiling, _world[i].y);
                Gizmos.DrawLine(a0, b0); Gizmos.DrawLine(a1, b1); Gizmos.DrawLine(b0, b1);
            }
        }
    }
}
