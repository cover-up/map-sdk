using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.Gameplay
{
    /// <summary>Which players a <see cref="MapSpawnDisc"/> places.</summary>
    public enum MapSpawnRole : byte
    {
        /// <summary>Everyone lands here. The default — and a single Both disc is still a
        /// complete map, which is why existing maps need no edit.</summary>
        Both = 0,
        /// <summary>Hiders only.</summary>
        Hiders = 1,
        /// <summary>Hunters only.</summary>
        Hunters = 2,
    }

    /// <summary>
    /// Hand-placeable map spawn area: a filled disc where players land at a random point
    /// facing a random direction when warping up from the lobby. Lives only in map
    /// scenes; lookups go through the FindInScene helpers so callers always say which
    /// scene's disc they mean while the inhabited hub (which carries its own SpawnRing)
    /// is still loaded.
    ///
    /// <b>Per-role spawns:</b> a map can give hiders and hunters separate landing areas —
    /// hiders deeper in the space, hunters held at an entrance — by placing one disc per
    /// <see cref="MapSpawnRole"/>. A map with a single <see cref="MapSpawnRole.Both"/>
    /// disc behaves exactly as before, so this is additive, not a migration.
    ///
    /// Several discs may share a role; a player lands in a randomly chosen one, so
    /// scattering a few small discs is a legitimate way to spread a spawn out.
    ///
    /// Radius is world metres, deliberately not doll-scaled — the gizmo shows exactly
    /// where players land, and with hiders and hunters sized independently there is no
    /// one scale to use.
    /// </summary>
    public sealed class MapSpawnDisc : MonoBehaviour
    {
        [SerializeField] private float radius = 2.5f;

        [SerializeField]
        [Tooltip("Who lands here. Both = everyone (the default, and all a map needs). Use " +
                 "Hiders/Hunters to give the two sides separate landing areas — e.g. hunters at " +
                 "an entrance while hiders start deeper in. Several discs may share a role; a " +
                 "player lands in a random one of them. Every spawn must sit inside the SMALLEST " +
                 "size's bounds, or players land out of bounds at that size.")]
        private MapSpawnRole role = MapSpawnRole.Both;

        /// <summary>Who this disc places.</summary>
        public MapSpawnRole Role => role;

        /// <summary>Any disc belonging to one specific scene — the streamed-in map for
        /// warp-up placement, the active scene for bounds-escape restarts. A bare
        /// FindAnyObjectByType would grab whichever map happens to be loaded, even one
        /// the player is not standing in. Role-agnostic; use <see cref="FindForRole"/> to
        /// place an actual player.</summary>
        public static MapSpawnDisc FindInScene(Scene scene)
        {
            foreach (MapSpawnDisc disc in FindObjectsByType<MapSpawnDisc>())
            {
                if (disc.gameObject.scene == scene) return disc;
            }
            return null;
        }

        /// <summary>
        /// The disc to land a <paramref name="wanted"/> player in: one dedicated to that
        /// role if the map placed any, else a shared <see cref="MapSpawnRole.Both"/> disc,
        /// else null. Dedicated beats shared, so adding a Hunters disc to a map that
        /// already has a Both disc redirects only the hunters and leaves hiders alone.
        /// Picks at random when several qualify — placement is client-local (each client
        /// places its own blob and replicates the result), so this needs no sync.
        /// </summary>
        public static MapSpawnDisc FindForRole(Scene scene, MapSpawnRole wanted)
        {
            List<MapSpawnDisc> exact = null, shared = null;
            foreach (MapSpawnDisc disc in FindObjectsByType<MapSpawnDisc>())
            {
                if (disc.gameObject.scene != scene) continue;
                if (wanted != MapSpawnRole.Both && disc.role == wanted)
                    (exact ??= new List<MapSpawnDisc>()).Add(disc);
                else if (disc.role == MapSpawnRole.Both)
                    (shared ??= new List<MapSpawnDisc>()).Add(disc);
            }
            List<MapSpawnDisc> pick = exact ?? shared;
            if (pick == null || pick.Count == 0) return null;
            return pick[Random.Range(0, pick.Count)];
        }

        // How far ABOVE the disc the ground probe starts. Deliberately far smaller
        // than a storey: the probe casts down and takes the first hit, so any start
        // height that clears a ceiling lets a disc on a lower floor snap onto the
        // floor ABOVE it — landing that role in the wrong room entirely. This only
        // needs to cover a disc left sitting slightly sunk into its own floor.
        private const float GroundProbeUp = 0.5f;

        // How far DOWN the probe reaches. Generous, so a disc left floating well
        // above its floor still lands people on it.
        private const float GroundProbeDown = 25f;

        // A random spawn pose inside the disc. Uniform over area (the sqrt),
        // so density doesn't bunch toward the centre. Random needs no sync —
        // each client places only its own blob and replicates the result.
        // Snaps to whatever ground is under the point.
        public void SamplePoint(out Vector3 position, out Quaternion facing)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float r = radius * Mathf.Sqrt(Random.value);
            Vector3 c = transform.position;
            position = new Vector3(c.x + Mathf.Cos(angle) * r, c.y, c.z + Mathf.Sin(angle) * r);
            if (Physics.Raycast(position + Vector3.up * GroundProbeUp, Vector3.down,
                    out RaycastHit hit, GroundProbeUp + GroundProbeDown))
            {
                position.y = hit.point.y + 0.05f;
            }
            facing = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        // Scene-view aid while placing: the disc players land in, coloured by role so a
        // map's hider and hunter spawns are told apart at a glance.
        private void OnDrawGizmos()
        {
            Gizmos.color = role switch
            {
                MapSpawnRole.Hiders => new Color(0.35f, 0.95f, 0.45f, 0.75f),  // green
                MapSpawnRole.Hunters => new Color(1f, 0.55f, 0.20f, 0.75f),    // orange
                _ => new Color(0.3f, 0.9f, 1f, 0.7f),                          // cyan — everyone
            };
            const int segments = 48;
            Vector3 c = transform.position;
            Vector3 prev = c + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = c + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
