using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// Asks the game for the lobby island's sky around a map. The map carries only
    /// this marker: the sky material lives in the game build, so an outdoor map gets
    /// it without shipping a byte of it.
    ///
    /// THE SEA IS NOT A CHOICE. Every map is an island (SDK 0.22, Docs/OneSeaDesign.md):
    /// the game draws one endless sea at the map's local Y 0, the same sea the lobby
    /// island stands in 600 m away, and whatever the map builds rises from it. Build
    /// the land above 0 and draw the bounds in knee-deep water, the way the hub does.
    /// The sea has no collider; shots do land on it. A dry floor below 0 (a cave, a
    /// basement) needs a sea hole over it (MapSeaHole, SDK 0.22). This object's
    /// position no longer matters; the gizmo draws the sea where it will be.
    ///
    /// Lighting is not touched. The sun and the flat ambient stay the arena standard,
    /// so camouflage reads the same under this sky as under any other. Haze past the
    /// playable reach is the game's.
    ///
    /// One per scene. Earlier SDKs let this marker place the sea and switch it off; a
    /// scene saved with those settings loads fine, the sea is simply at 0 and on.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapEnvironment : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Show the lobby island's sky instead of the scene's own skybox.")]
        private bool islandSky = true;

        public bool IslandSky => islandSky;

        /// <summary>World Y of the water surface: the map's local 0, always.</summary>
        public const float SeaLevel = 0f;

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
            Vector3 p = new Vector3(0f, SeaLevel, 0f);
            Gizmos.color = new Color(0.10f, 0.45f, 0.70f, 0.35f);
            Gizmos.DrawCube(p, new Vector3(600f, 0.01f, 600f));
            Gizmos.color = new Color(0.10f, 0.45f, 0.70f, 0.9f);
            Gizmos.DrawWireCube(p, new Vector3(600f, 0.01f, 600f));
        }
#endif
    }
}
