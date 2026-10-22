using System.Collections.Generic;
using UnityEngine;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// A footprint where the sea is NOT (SDK 0.22, Docs/OneSeaDesign.md §6). Every map
    /// stands in one endless sea at local Y 0 (<see cref="MapEnvironment"/>), so a dry
    /// floor below 0, a cave, a basement, a sunken courtyard, would show water over it.
    /// Draw a closed outline around that floor and the sea is left out inside it: not
    /// drawn, not landed on by shots, not waded in. The shore foam at the hole's edge
    /// comes from the scene depth, so the cave wall reads as the coast there.
    ///
    /// Corners are in this object's local X/Z plane, in order around the outline, closed
    /// automatically; drag them in the scene view (MapSeaHoleEditor). Any number of holes,
    /// anywhere in the scene, found by type. A hole must stay inside the map's sea disc
    /// (<see cref="SeaDiscRadius"/> from the origin): past that the water is the world's
    /// and has no holes.
    ///
    /// The game bakes the active holes into a mask the water shader samples
    /// (<see cref="BakeMask"/>) and asks <see cref="AnyContains"/> for shots and wading.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapSeaHole : MonoBehaviour
    {
        /// <summary>How far from the map origin the map's own sea disc reaches; a hole
        /// has to lie inside it. The same number the game draws the disc with.</summary>
        public const float SeaDiscRadius = 300f;

        private static readonly List<MapSeaHole> Holes = new List<MapSeaHole>();

        [SerializeField]
        [Tooltip("The dry footprint, as corners in this object's local X/Z plane, in order around the outline. Closed automatically.")]
        private List<Vector2> points = new List<Vector2>
        {
            new Vector2(-5f, -5f), new Vector2(5f, -5f), new Vector2(5f, 5f), new Vector2(-5f, 5f),
        };

        public IReadOnlyList<Vector2> Points => points;
        public void SetPoints(List<Vector2> newPoints) { points = newPoints; _worldFrame = -1; }

        private void OnEnable() { Holes.Add(this); _worldFrame = -1; }
        private void OnDisable() => Holes.Remove(this);

        public static int ActiveCount => Holes.Count;

        /// <summary>Every enabled hole in the ACTIVE scene with a real outline.</summary>
        public static void CollectActive(List<MapSeaHole> into)
        {
            into.Clear();
            if (Holes.Count == 0) return;
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (MapSeaHole h in Holes)
                if (h != null && h.gameObject.scene == active && h.points.Count >= 3) into.Add(h);
        }

        /// <summary>Every enabled hole in <paramref name="scene"/> with a real outline, whichever
        /// scene is active: the loaded map is not the active scene while the player is still in
        /// the lobby, and its sea disc is drawn then.</summary>
        public static void CollectIn(UnityEngine.SceneManagement.Scene scene, List<MapSeaHole> into)
        {
            into.Clear();
            foreach (MapSeaHole h in Holes)
                if (h != null && h.gameObject.scene == scene && h.points.Count >= 3) into.Add(h);
        }

        /// <summary>True when world (x, z) lies inside any enabled hole of <paramref name="scene"/>.</summary>
        public static bool AnyContains(UnityEngine.SceneManagement.Scene scene, float x, float z)
        {
            foreach (MapSeaHole h in Holes)
                if (h != null && h.gameObject.scene == scene && h.points.Count >= 3 && h.ContainsXZ(x, z)) return true;
            return false;
        }

        // ------------------------------------------------------------------ world-space cache
        private readonly List<Vector2> _world = new List<Vector2>();
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
        }

        public void GetWorldOutline(List<Vector2> into)
        {
            RefreshWorld();
            into.Clear(); into.AddRange(_world);
        }

        public bool ContainsXZ(float x, float z)
        {
            RefreshWorld();
            return MapBoundsPolygon.ContainsXZ(_world, x, z);
        }

        /// <summary>The furthest world corner from the map origin, in XZ metres.</summary>
        public float ReachXZ()
        {
            RefreshWorld();
            float best = 0f;
            foreach (Vector2 p in _world) best = Mathf.Max(best, p.magnitude);
            return best;
        }

        public int ShapeHash()
        {
            RefreshWorld();
            unchecked
            {
                int h = _world.Count * 397;
                foreach (Vector2 p in _world) h = h * 31 + p.GetHashCode();
                return h;
            }
        }

        /// <summary>A top-down mask of <paramref name="holes"/> over the square from
        /// (<paramref name="minX"/>, <paramref name="minZ"/>) of side <paramref name="size"/>:
        /// R = 1 inside a hole, 0 elsewhere. The caller owns the texture.</summary>
        public static Texture2D BakeMask(List<MapSeaHole> holes, float minX, float minZ, float size, int texels)
        {
            var tex = new Texture2D(texels, texels, TextureFormat.R8, false, true)
            {
                name = "MapSeaHoles", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[texels * texels];
            var outlines = new List<List<Vector2>>(holes.Count);
            foreach (MapSeaHole h in holes) { var o = new List<Vector2>(); h.GetWorldOutline(o); outlines.Add(o); }
            float step = size / texels;
            for (int y = 0; y < texels; y++)
            {
                float z = minZ + (y + 0.5f) * step;
                for (int x = 0; x < texels; x++)
                {
                    float wx = minX + (x + 0.5f) * step;
                    bool inside = false;
                    for (int i = 0; i < outlines.Count && !inside; i++) inside = MapBoundsPolygon.ContainsXZ(outlines[i], wx, z);
                    pixels[y * texels + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 255);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);   // kept readable: a megabyte, and Validate and tests read it back
            return tex;
        }

#if UNITY_EDITOR
        // Scene-view aid while drawing. Never in play: the Game view's Gizmos toggle would paint it in.
        private void OnDrawGizmos()
        {
            if (!MapBoundsVolume.ShowGizmos || Application.isPlaying || points.Count < 2) return;
            _worldFrame = -1; RefreshWorld();
            int n = _world.Count;
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.9f);
            float y = MapEnvironment.SeaLevel + 0.02f;
            for (int i = 0, j = n - 1; i < n; j = i++)
                Gizmos.DrawLine(new Vector3(_world[j].x, y, _world[j].y), new Vector3(_world[i].x, y, _world[i].y));
        }
#endif
    }
}
