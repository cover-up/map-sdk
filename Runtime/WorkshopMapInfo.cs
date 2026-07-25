using UnityEngine;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// Optional authoring metadata for a Workshop map, dropped on the
    /// <c>_CoverUpMap</c> root next to <see cref="MapConfig"/>. The exporter
    /// reads it into <c>map.json</c>; it is inert at runtime. Absent = defaults
    /// (mapId = scene name, empty title/description/tags, no preview). Author and
    /// Steam id are filled by Steam at publish time, not here.
    /// </summary>
    public sealed class WorkshopMapInfo : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Stable id — the catalog key and the join-time content match. Defaults to the scene name if blank. Renaming it orphans the map's identity.")]
        private string mapId = "";

        [SerializeField]
        [Tooltip("Display name shown in the map browser and on the Workshop page.")]
        private string title = "";

        [SerializeField, TextArea(2, 5)]
        [Tooltip("Short description for the browser detail pane / Workshop page.")]
        private string description = "";

        [SerializeField]
        [Tooltip("Free-form tags (e.g. indoor, small, medium, large) for browsing.")]
        private string[] tags = new string[0];

        [SerializeField]
        [Tooltip("Preview image (Workshop thumbnail + browser). Any texture; the exporter writes it out as preview.png.")]
        private Texture2D preview;

        public string MapId => mapId;
        public string Title => title;
        public string Description => description;
        public string[] Tags => tags ?? new string[0];
        public Texture2D Preview => preview;
    }
}
