using UnityEngine;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// Which shaders the eyedropper is allowed to read a "true" albedo out of —
    /// shared so the runtime sampler and the export validator can't drift, exactly
    /// like <see cref="MapSceneGuard"/>. See Docs/Steam.md §8.8.
    ///
    /// <para><b>The problem.</b> <c>SurfaceSampler</c> does not read the pixel a
    /// player sees; it reads the material's <c>_BaseColor</c> × <c>_BaseMap</c> texel
    /// and mirrors URP/Lit's metallic/smoothness rules. That is deliberate — it is
    /// what makes a painted patch survive relighting instead of baking in whatever
    /// light happened to fall on it (the Paint v2 "chrome bug" fix). But it takes on
    /// faith that the shader actually renders what those properties say. A custom map
    /// shader can declare <c>_BaseColor</c>, satisfy every <c>HasProperty</c> check we
    /// make, and render from something else entirely — so the eyedropper hands a hider
    /// a colour that does not match the wall, and camouflage silently stops
    /// working.</para>
    ///
    /// <para><b>The rule.</b> Trust only the shaders whose semantics the sampler
    /// genuinely mirrors: URP's standard family and the world-surface shaders that
    /// ship in this build. Anything else falls back to the sampler's existing
    /// screen-sample path — less precise, but true by construction, because it is
    /// literally what was rendered. A map is never blocked; it just doesn't get to
    /// define what "the true colour" means.</para>
    ///
    /// <para><b>What this does not close.</b> The check is by shader NAME, because a
    /// map bundle carries its own copy of every shader it references — a legitimate
    /// map's URP/Lit is a different object from ours, so reference identity would
    /// reject every honest Workshop map along with the dishonest ones. A hand-built
    /// bundle can therefore still name a lying shader "Universal Render Pipeline/Lit".
    /// Closing that needs shader provenance we don't have; what it buys an attacker is
    /// bounded to the same eyedropper inaccuracy, and Validate Map warns an honest
    /// author long before it matters.</para>
    /// </summary>
    public static class CamoShaderPolicy
    {
        /// <summary>Shaders whose albedo and finish properties <c>SurfaceSampler</c>
        /// reproduces faithfully. URP's own family, plus the shaders in this build
        /// that a player can actually stand in front of and eyedrop.</summary>
        public static readonly string[] Trusted =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Universal Render Pipeline/Complex Lit",
            "Universal Render Pipeline/Baked Lit",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Terrain/Lit",
            "Universal Render Pipeline/Particles/Lit",
            "Universal Render Pipeline/Particles/Unlit",
            // Ours. Omitting one costs precision on that surface, never correctness:
            // the sampler just falls back to the screen, which is where it was before
            // Paint v2 anyway.
            "CoverUp/LowPolyGround",
            "CoverUp/StylizedWater",
            "CoverUp/GrassBlade",
            "CoverUp/MapMirror",
            // The blob body's own shaders — eyedropping ANOTHER player to copy their
            // paint is a shipped feature, and both read albedo as _BaseMap × _BaseColor,
            // which is precisely what the sampler reconstructs. BlobGroundMatch says so
            // in its own header: same property names, so the eyedropper keeps working.
            "CoverUp/BlobCamo",
            "CoverUp/BlobGroundMatch",
        };

        /// <summary>True when the eyedropper may treat this shader's declared albedo
        /// as the truth. Null (a missing/stripped shader) is never trusted.</summary>
        public static bool IsAlbedoTruthful(Shader shader)
        {
            if (shader == null) return false;
            string name = shader.name;
            for (int i = 0; i < Trusted.Length; i++)
            {
                if (Trusted[i] == name) return true;
            }
            return false;
        }
    }
}
