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
    ///     RATIFIED 2026-08-05, and no longer under evaluation: sun 0.592 over
    ///     flat ambient 0.699 sRGB, solved together against nine measured
    ///     reference captures so the body's shadow side sits at 0.43 of its lit
    ///     side. The old 0.78/0.55 pair put it at 0.252 and read grey. The fill
    ///     lamp added 2026-08-04 is RETIRED to 0: the raised ambient does its job,
    ///     and a directional fill from behind makes the shading ramp non-monotone,
    ///     which no measured reference ramp is. See SunIntensity and FillIntensity
    ///     below, Tools/blob-lab-unity/GAP-REPORT.md for the measurements, and the
    ///     dev console `stage` command for live comparison.
    ///  1b. Arena suns cast SOFT SHADOWS (SunShadows). Previously unassigned, so
    ///     scripted arenas silently had none and no body was ever grounded.
    ///  2. Blendable surfaces use URP/Lit with a flat _BaseColor and a matte
    ///     finish (<see cref="SurfaceMaterial"/>). The eyedropper reads
    ///     _BaseColor (plus MaterialPropertyBlock recolors); custom shaders
    ///     without it degrade to screen sampling and lose material fidelity.
    ///  3. Every surface a hider should blend to carries CamouflageSurface.
    ///  4. No reflection probes and no glossy trim: the blob body ships with
    ///     environment reflections OFF, so probe-lit or shiny props would
    ///     visibly split from a body painted to match them. Since 2026-08-05
    ///     that extends to the SKYBOX, which is an environment reflection
    ///     source in its own right — see <see cref="ReflectionIntensity"/>.
    ///
    /// The island LOBBY is the deliberate exception (skybox + Trilight mood
    /// lighting in IslandBuilder) — social space, not a scoring arena.
    /// </summary>
    public static class ArenaStandards
    {
        /// <summary>Sun and ambient are ONE solved pair, not two dials. They were
        /// measured on 2026-08-05 against nine reference captures
        /// (Tools/blob-lab/LOOK-SPEC-MEASURED.md, Tools/blob-lab-unity/GAP-REPORT.md)
        /// and solved together so the body's shadow side lands at 0.43 of its lit
        /// side, which is what the reference measures across five independent
        /// same-body pairs. The previous 0.78/0.55 pair put it at 0.252, nearly
        /// twice as dark, which is why an unpainted bløb read grey instead of white.
        ///
        /// Total irradiance on a sun-facing surface is unchanged to within 0.4%
        /// (0.592 + 0.4468 = 1.0388 against the old 1.0433). This redistributes
        /// light, it does not add any. Move ONE of them and you break the ratio the
        /// character's whole look is built on: raising the sun to chase a brighter
        /// peak widens exactly the failure this pair was solved to close.</summary>
        public const float SunIntensity = 0.592f;
        public static readonly Quaternion SunRotation = Quaternion.Euler(50f, -30f, 0f);

        /// <summary>Flat ambient, sRGB 0.699 = linear 0.4468. See
        /// <see cref="SunIntensity"/> — these two are solved as a pair.</summary>
        public static readonly Color FlatAmbient = new Color(0.699f, 0.699f, 0.699f);
        public const float SurfaceSmoothness = 0.1f;

        /// <summary>How much the environment (in practice the skybox) reflects
        /// into surfaces. ZERO for arenas, and it is not a stylistic preference:
        /// a flat ambient does NOT stop the skybox lighting a map. URP's
        /// environment-reflection term is separate from ambient, and its
        /// fresnel rises toward grazing angles, so even at
        /// <see cref="SurfaceSmoothness"/> a flat wall picks up the sky
        /// gradient unevenly ACROSS ITSELF — brighter where it turns away from
        /// the viewer. That breaks camouflage twice over: one surface is no
        /// longer one colour, and the bløb (environment reflections OFF, per
        /// rule 4 above) has no such term to match it with. Diagnosed on
        /// diorama 2026-08-05. Maps keep their skybox as a BACKDROP — only its
        /// contribution to surface shading is removed.</summary>
        public const float ReflectionIntensity = 0f;

        /// <summary>The fill lamp, RETIRED 2026-08-05 (intensity 0), kept as a
        /// named constant because existing scenes still carry the light and the
        /// retune below has to find and silence it.
        ///
        /// It was added 2026-08-04 to model down- and back-faces that the ambient
        /// floor left plate-flat. Raising the ambient to <see cref="FlatAmbient"/>
        /// removed its reason to exist, and measurement removed its defence: every
        /// shading ramp in the reference set is MONOTONE from lit to shadow, while
        /// a directional fill from behind lifts the back face above the terminator
        /// (0.406 against 0.252 on the old rig) and puts a second bright band on a
        /// body that should only ever fall away. A hider whose back face is
        /// brighter than their own terminator does not match the wall behind them.
        ///
        /// Found BY NAME by the retune below and by the game's `stage` console
        /// command, so a fill must carry exactly this name — a second white
        /// directional with any other name gets normalised to sun intensity.</summary>
        public const string FillLightName = "Fill Light";
        public const float FillIntensity = 0f;

        /// <summary>Arena suns cast soft shadows. Previously never assigned at all,
        /// so a scripted arena inherited AddComponent's default of None and no
        /// character was ever grounded. The floor directly beneath a standing body
        /// should read 0.25–0.38x the open floor; the reference measures 0.30x, and
        /// it is the single strongest "there is a body here" cue in the whole
        /// reference set. The URP asset already pays for main-light shadows, so the
        /// marginal cost is casters, not a new feature.</summary>
        public const LightShadows SunShadows = LightShadows.Soft;

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
            light.shadows = SunShadows;
            lightGo.transform.rotation = SunRotation;
            if (FillIntensity > 0f) BuildFill(light);
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
            foreach (var light in Object.FindObjectsByType<Light>())
            {
                if (light.type != LightType.Directional) continue;
                if (light.gameObject.name == FillLightName) { fill = light; continue; }
                light.intensity = SunIntensity;
                light.color = Color.white;
                light.shadows = SunShadows;
                if (sun == null) sun = light;
            }
            // Fill retired: silence the lamp an older SDK left behind rather than
            // deleting it, so a re-tuned scene diffs as one changed value and a map
            // author who wants it back can still see where it was.
            if (FillIntensity <= 0f)
            {
                if (fill != null)
                {
                    fill.intensity = 0f;
                    fill.enabled = false;
                }
            }
            else
            {
                if (sun != null && fill == null) fill = BuildFill(sun);
                if (sun != null && fill != null)
                {
                    fill.enabled = true;
                    fill.intensity = FillIntensity;
                    fill.color = Color.white;
                    fill.transform.rotation = FillRotation(sun.transform.rotation);
                }
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
            // Read the open scene's per-map tint (neutral if none). ArenaLighting
            // holds the level and the clamp; the tint gives only hue/chroma so the
            // ratified ambient level, and the lit:shadow ratio, stay fixed.
            Color tint = OpenSceneAmbientTint();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = CoverUp.MapSdk.ArenaLighting.TintedAmbient(tint);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = ReflectionIntensity;
        }

        private static Color OpenSceneAmbientTint()
        {
            foreach (var cfg in Object.FindObjectsByType<CoverUp.Gameplay.MapConfig>(FindObjectsSortMode.None))
                if (cfg != null) return cfg.AmbientTint;
            return Color.white;
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
