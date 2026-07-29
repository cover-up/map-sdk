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
        public const float SunIntensity = 0.7f;
        public static readonly Quaternion SunRotation = Quaternion.Euler(50f, -30f, 0f);
        public static readonly Color FlatAmbient = new Color(0.55f, 0.55f, 0.55f);
        public const float SurfaceSmoothness = 0.1f;

        /// <summary>Create the canonical arena sun in the open scene and apply
        /// the flat ambient. For NEW scenes; existing ones use
        /// <see cref="ApplyLightingToOpenScene"/>.</summary>
        public static Light BuildLighting()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = SunIntensity;
            light.color = Color.white;
            lightGo.transform.rotation = SunRotation;
            ApplyAmbient();
            return light;
        }

        /// <summary>Retune an EXISTING scene to the canonical arena lighting:
        /// every directional light's intensity/color plus the flat ambient.
        /// Touches nothing else — geometry, materials, props and light
        /// orientation (a per-map aesthetic) stay as authored.</summary>
        public static void ApplyLightingToOpenScene()
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                light.intensity = SunIntensity;
                light.color = Color.white;
            }
            ApplyAmbient();
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
