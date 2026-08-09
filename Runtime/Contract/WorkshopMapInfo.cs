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

        [SerializeField]
        [Tooltip("Extra images for the Workshop page's gallery, beside the thumbnail. The exporter writes them into screenshots/ and the publisher uploads them as the item's preview images, replacing whatever was there.")]
        private Texture2D[] screenshots = new Texture2D[0];

        public string MapId => mapId;
        public string Title => title;
        public string Description => description;
        public string[] Tags => tags ?? new string[0];
        public Texture2D Preview => preview;

        /// <summary>The item page's gallery, as opposed to <see cref="Preview"/>,
        /// which is the single thumbnail. Separate fields because Steam treats them
        /// as different things: one <c>SetItemPreview</c>, N
        /// <c>AddItemPreviewFile</c>.</summary>
        public Texture2D[] Screenshots => screenshots ?? new Texture2D[0];
    }
}
