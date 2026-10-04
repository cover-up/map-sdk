using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// Asks the game for the lobby island's sky and sea around a map. The map carries
    /// only this marker: the sky material, the water shader and its mesh all live in
    /// the game build, so an outdoor map gets both without shipping a byte of either.
    ///
    /// Put it on an empty object and move that object to where the water surface
    /// should be: its world Y is the sea level, its XZ the middle of the sea. The sea
    /// is an endless flat plane with no collider, so keep players out of it with the
    /// map's bounds. Shots do land on it.
    ///
    /// Lighting is not touched. The sun and the flat ambient stay the arena standard,
    /// so camouflage reads the same under this sky as under any other, and fog stays
    /// off as on every map. In the editor the sea is drawn as a gizmo only; the sky
    /// shows in the game.
    ///
    /// One per scene. A game build older than SDK 0.14.0 ignores the marker and shows
    /// the map with whatever sky the scene itself carries.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapEnvironment : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Show the lobby island's sky instead of the scene's own skybox.")]
        private bool islandSky = true;

        [SerializeField]
        [Tooltip("Show the lobby island's sea as an endless plane at this object's height.")]
        private bool sea = true;

        public bool IslandSky => islandSky;
        public bool Sea => sea;

        /// <summary>World Y of the water surface.</summary>
        public float SeaLevel => transform.position.y;

        public static MapEnvironment FindInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                MapEnvironment env = root.GetComponentInChildren<MapEnvironment>(false);
                if (env != null) return env;
            }
            return null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!sea) return;
            Vector3 p = transform.position;
            Gizmos.color = new Color(0.10f, 0.45f, 0.70f, 0.35f);
            Gizmos.DrawCube(p, new Vector3(400f, 0.01f, 400f));
            Gizmos.color = new Color(0.10f, 0.45f, 0.70f, 0.9f);
            Gizmos.DrawWireCube(p, new Vector3(400f, 0.01f, 400f));
        }
#endif
    }
}
