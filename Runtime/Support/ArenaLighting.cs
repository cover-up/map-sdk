using UnityEngine;

namespace CoverUp.MapSdk
{
    /// <summary>
    /// The RUNTIME half of the arena lighting contract: the envelope the character
    /// is allowed to be lit inside, and the clamp that enforces it on whatever a
    /// loaded map (including a third-party Workshop bundle) baked into its
    /// RenderSettings. The authoring half lives in the editor-only
    /// <c>ArenaStandards</c>, which cannot run in a player build — so the numbers
    /// that BOTH sides need live here, and ArenaStandards references them.
    ///
    /// Why a runtime clamp at all: ambient is baked into each map scene's
    /// RenderSettings and rides inside the AssetBundle we do not author. The editor
    /// validator stops first-party maps drifting; this stops everyone else's from
    /// pushing the bløb out of the range it was measured and tuned against
    /// (Tools/blob-lab-unity/GAP-REPORT.md). Deterministic, so every peer converges
    /// on identical lighting with zero replication — which is what keeps a painted
    /// camouflage patch matching the wall on every machine.
    /// </summary>
    public static class ArenaLighting
    {
        // The ratified flat-ambient level, LINEAR (sRGB 0.699). Must match
        // ArenaStandards.FlatAmbient; that editor const is the authoring source and
        // this is the same value the runtime is allowed to see.
        public const float AmbientLinear = 0.4468f;

        // Envelope: how far a map may move each term before the character stops
        // reading the way it was tuned. Luminance ±12% of the ratified floor;
        // saturation a hint only (C1 ceiling); sun within ±20% of 0.592 and near
        // neutral; a bounded-warm sun is allowed because a uniformly warm sun tints
        // body and wall alike and does not break the match, but a saturated one
        // would. Reflection and fog are hard zero/off — a reflection's grazing
        // fresnel shades a flat wall unevenly and the body has no term to match it.
        public const float AmbientLinMin = AmbientLinear * 0.88f; // 0.393
        public const float AmbientLinMax = AmbientLinear * 1.12f; // 0.500
        public const float AmbientSatMax = 0.14f;
        public const float SunIntensityMin = 0.592f * 0.80f;      // 0.474
        public const float SunIntensityMax = 0.592f * 1.20f;      // 0.710
        public const float SunSatMax = 0.20f;                      // bounded-warm ok

        /// <summary>The flat ambient a tint produces at the ratified level: the tint
        /// gives hue/chroma, the level stays fixed so the lit:shadow ratio (the thing
        /// that read grey) never drifts. Saturation is clamped to a hint.</summary>
        public static Color TintedAmbient(Color tint)
        {
            Color.RGBToHSV(tint, out float h, out float s, out _);
            s = Mathf.Min(s, AmbientSatMax);
            // Rebuild at the fixed ambient level (value = the linear floor), then the
            // engine treats RenderSettings.ambientLight as-authored (gamma space).
            Color c = Color.HSVToRGB(h, s, 1f);
            float levelSrgb = Mathf.LinearToGammaSpace(AmbientLinear);
            return new Color(c.r * levelSrgb, c.g * levelSrgb, c.b * levelSrgb, 1f);
        }

        /// <summary>Pull the ACTIVE scene's baked lighting into the envelope, before
        /// the first rendered frame. Call once when a MAP scene becomes active
        /// (never the hub — the island is a deliberate exception and is never routed
        /// through here). Idempotent: a well-authored map passes through untouched.</summary>
        public static void NormalizeActiveScene()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ClampAmbient(RenderSettings.ambientLight);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0f;  // skybox must not shade surfaces
            RenderSettings.fog = false;                // fog is a distance tell paint can't match

            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l == null || l.type != LightType.Directional || !l.enabled) continue;
                l.intensity = Mathf.Clamp(l.intensity, SunIntensityMin, SunIntensityMax);
                l.color = ClampSaturation(l.color, SunSatMax);
            }
        }

        /// <summary>Clamp an ambient colour into the envelope: luminance into the
        /// ±12% band, saturation to a hint. Deterministic.</summary>
        public static Color ClampAmbient(Color ambient)
        {
            Color clamped = ClampSaturation(ambient, AmbientSatMax);
            float lin = Mathf.Max(0.0001f, GammaLuminanceToLinear(clamped));
            float target = Mathf.Clamp(lin, AmbientLinMin, AmbientLinMax);
            float scale = target / lin;
            return new Color(clamped.r * scale, clamped.g * scale, clamped.b * scale, 1f);
        }

        private static Color ClampSaturation(Color c, float satMax)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (s <= satMax) return c;
            return Color.HSVToRGB(h, satMax, v);
        }

        // Rec.709 luma of a gamma-space colour, evaluated on its LINEARised channels
        // (the physically meaningful brightness the clamp reasons about).
        private static float GammaLuminanceToLinear(Color c)
        {
            float r = Mathf.GammaToLinearSpace(c.r);
            float g = Mathf.GammaToLinearSpace(c.g);
            float b = Mathf.GammaToLinearSpace(c.b);
            return 0.2126f * r + 0.7152f * g + 0.0722f * b;
        }
    }
}
