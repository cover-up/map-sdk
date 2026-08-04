using UnityEditor;
using UnityEngine;

namespace CoverUp.EditorTools
{
    /// <summary>
    /// The single source of truth for how GAMEPLAY map scenes are lit and how
    /// their blendable surfaces are authored. The paint system depends on
    /// these rules — build every new arena through this class, never with
    /// local constants:
    ///
    ///  1. Lighting is deliberately flat: a soft white sun over a strong
    ///     neutral flat ambient. The eyedropper copies true material albedo,
    ///     so equal lighting on body and surface is what makes a painted
    ///     patch read identical from any angle — a harsh sun/ambient ratio
    ///     re-introduces the shading mismatch we fixed in 2026-07.
    ///     Since 2026-08-04 the rig also carries a dim FILL lamp from low
    ///     opposite the sun, borrowed from the menu bløb's studio rig
    ///     (CoverUp/PaintPreview): it models down- and back-faces the flat
    ///     ambient leaves plate-flat, and because it only ADDS light where
    ///     the sun reaches least it narrows the lit/unlit spread — it cannot
    ///     re-introduce the mismatch above. The sun matches the studio key
    ///     (0.78). The ambient LEVEL is still under evaluation against the
    ///     menu look (0.55 vs 0.40 vs 0.30, dev console `stage`); 0.55 stays
    ///     canonical until Pål ratifies a lower floor.
    ///  2. Blendable surfaces use URP/Lit with a flat _BaseColor and a matte
    ///     finish (<see cref="SurfaceMaterial"/>). The eyedropper reads
    ///     _BaseColor (plus MaterialPropertyBlock recolors); custom shaders
    ///     without it degrade to screen sampling and lose material fidelity.
    ///  3. Every surface a hider should blend to carries CamouflageSurface.
    ///  4. No reflection probes and no glossy trim: the blob body ships with
    ///     environment reflections OFF, so probe-lit or shiny props would
    ///     visibly split from a body painted to match them.
    ///
    /// The island LOBBY is the deliberate exception (skybox + Trilight mood
    /// lighting in IslandBuilder) — social space, not a scoring arena.
    /// </summary>
    public static class ArenaStandards
    {
        public const float SunIntensity = 0.78f;
        public static readonly Quaternion SunRotation = Quaternion.Euler(50f, -30f, 0f);
        public static readonly Color FlatAmbient = new Color(0.55f, 0.55f, 0.55f);
        public const float SurfaceSmoothness = 0.1f;

        /// <summary>The fill lamp, at the studio rig's key:fill ratio. Found BY
        /// NAME by the retune below and by the game's `stage` console command,
        /// so a fill must carry exactly this name — a second white directional
        /// with any other name gets normalised to sun intensity instead.</summary>
        public const string FillLightName = "Fill Light";
        public const float FillIntensity = 0.16f;

        /// <summary>Where the fill aims, derived from the sun rather than a
        /// constant: low and opposite (sun yaw + 180°, pitched 15° upward), so a
        /// map that turned its sun keeps a sensible fill. Mirrors the studio
        /// rig's lower-front fill, which exists to keep down- and back-faces off
        /// the flat ambient floor.</summary>
        public static Quaternion FillRotation(Quaternion sunRotation)
            => Quaternion.Euler(-15f, sunRotation.eulerAngles.y + 180f, 0f);

        /// <summary>Create the canonical arena sun + fill in the open scene and
        /// apply the flat ambient. For NEW scenes; existing ones use
        /// <see cref="ApplyLightingToOpenScene"/>. Returns the sun.</summary>
        public static Light BuildLighting()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = SunIntensity;
            light.color = Color.white;
            lightGo.transform.rotation = SunRotation;
            BuildFill(light);
            ApplyAmbient();
            return light;
        }

        /// <summary>Retune an EXISTING scene to the canonical arena lighting:
        /// every directional light's intensity/color plus the flat ambient, and
        /// a fill lamp created (or retuned) opposite the first sun found.
        /// Touches nothing else — geometry, materials, props and sun
        /// orientation (a per-map aesthetic) stay as authored.</summary>
        [MenuItem("Cover Up!/Maps/Apply Arena Lighting")]
        public static void ApplyLightingToOpenScene()
        {
            Light sun = null, fill = null;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                if (light.gameObject.name == FillLightName) { fill = light; continue; }
                light.intensity = SunIntensity;
                light.color = Color.white;
                if (sun == null) sun = light;
            }
            if (sun != null && fill == null) fill = BuildFill(sun);
            if (sun != null && fill != null)
            {
                fill.intensity = FillIntensity;
                fill.color = Color.white;
                fill.transform.rotation = FillRotation(sun.transform.rotation);
            }
            ApplyAmbient();
            // Menu-invoked retunes must be saveable; builders save right after
            // anyway, so a spuriously dirty flag costs nothing there.
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }

        private static Light BuildFill(Light sun)
        {
            var fillGo = new GameObject(FillLightName);
            fillGo.transform.SetParent(sun.transform.parent, false);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = FillIntensity;
            fill.color = Color.white;
            fill.shadows = LightShadows.None;
            fillGo.transform.rotation = FillRotation(sun.transform.rotation);
            return fill;
        }

        private static void ApplyAmbient()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = FlatAmbient;
        }

        /// <summary>The canonical blendable-surface material: URP/Lit, flat
        /// _BaseColor, matte. Idempotent per (folder, name) — safe to call
        /// from create-once builders on every run.</summary>
        public static Material SurfaceMaterial(string folder, string name, Color color)
        {
            string path = $"{folder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            mat.SetFloat("_Smoothness", SurfaceSmoothness); // matte — no specular tells
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
