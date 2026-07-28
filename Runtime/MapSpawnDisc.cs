using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// Hand-placeable map spawn area: a filled disc where everyone — hider
    /// and hunter alike — lands at a random point facing a random direction
    /// when warping up from the lobby. Lives only in map scenes (box_*);
    /// lookups go through FindInScene so callers always say which scene's
    /// disc they mean while the inhabited hub (which carries its own
    /// SpawnRing) is still loaded. Radius is world metres, deliberately not
    /// doll-scaled — the gizmo shows exactly where players land, and with
    /// hiders and hunters sized independently there is no one scale to use.
    /// </summary>
    public sealed class MapSpawnDisc : MonoBehaviour
    {
        [SerializeField] private float radius = 2.5f;

        /// <summary>The disc belonging to one specific scene — the streamed-in
        /// map for warp-up placement, the active scene for bounds-escape
        /// restarts. A bare FindAnyObjectByType would grab whichever map
        /// happens to be loaded, even one the player is not standing in.</summary>
        public static MapSpawnDisc FindInScene(Scene scene)
        {
            foreach (MapSpawnDisc disc in FindObjectsByType<MapSpawnDisc>(FindObjectsSortMode.None))
            {
                if (disc.gameObject.scene == scene) return disc;
            }
            return null;
        }

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
            if (Physics.Raycast(position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 25f))
                position.y = hit.point.y + 0.05f;
            facing = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        // Scene-view aid while placing: the disc players will land in.
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.7f);
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
