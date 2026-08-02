using System.Collections.Generic;
using CoverUp.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.Gameplay
{
    /// <summary>
    /// What a map is allowed to cost, and the census that measures it. The resource
    /// counterpart to <see cref="MapSceneGuard"/>, and deliberately the same shape:
    /// ONE set of numbers, read by every enforcement point, so they cannot drift.
    /// See Docs/Steam.md §8.9.
    ///
    /// Three enforcement points share this:
    /// <list type="bullet">
    /// <item>Export refusal — <c>MapSizeTools.Validate</c> turns <see cref="Hard"/>
    ///   breaches into errors (which abort Export and therefore Publish) and
    ///   <see cref="Soft"/> breaches into warnings.</item>
    /// <item>Publish refusal — the exporter records the census into map.json and the
    ///   publish tool refuses a package that breaches it.</item>
    /// <item>Runtime — <c>MapGovernor</c> re-takes the census on the loaded scene and
    ///   refuses the map on a hard breach. That is the net for a bundle built outside
    ///   our pipeline, exactly as the runtime component strip is for
    ///   <see cref="MapSceneGuard"/>.</item>
    /// </list>
    ///
    /// <para><b>The map.json census is advisory and must never be trusted.</b> A
    /// downloaded map.json is attacker-controlled, so it is for the publish gate and
    /// the browser card only. The runtime census, taken on the actual loaded scene,
    /// is what enforces. Same rule the <c>contract</c> block already lives under
    /// (Docs/Steam.md §8.1).</para>
    ///
    /// <para><b>Sized maps count as their worst ACTIVE case, not their sum.</b> Size
    /// roots are mutually exclusive at runtime, so summing Small+Medium+Large would
    /// refuse a perfectly ordinary three-size map for costs no player ever pays at
    /// once. <see cref="Take"/> therefore counts everything outside the size roots as
    /// shared, counts each size root separately, and adds the per-metric maximum. It
    /// stays deterministic (same bundle, same number on every client) because it
    /// reads the scene's structure, never which root happens to be active when it
    /// runs.</para>
    /// </summary>
    public static class MapBudget
    {
        // ------------------------------------------------------------ the numbers
        //
        // Hard caps are the absurdity ceilings that block; soft caps are where a
        // creator should look again at what they built. Everything is generous on
        // purpose: this is a grief and safety floor, not a performance budget.
        //
        // Calibrated 2026-08-01 against the densest first-party content we have,
        // Island_Decor (the hub island):
        //
        //     959 renderers · 5,747,066 vertices · 112 mesh colliders
        //     134,703 collider vertices · 1 audio source · 587 MB asset memory
        //
        // Rule of thumb used: a soft cap sits at roughly 2–5× that, so a map has to be
        // meaningfully heavier than our own heaviest scene before a creator hears
        // anything. The vertex cap is the one that moved during calibration — it was
        // provisionally 4M, which Island_Decor itself exceeds, i.e. it would have
        // warned on maps no denser than the game's own island.
        //
        // Re-measure with:
        //   Unity -batchmode -nographics -projectPath <game> \
        //     -executeMethod CoverUp.EditorTools.MapBudgetReport.RunHeadless \
        //     -scene <scene.unity> -memory

        /// <summary>Hard caps. A breach blocks export/publish and is refused at load.</summary>
        public static class Hard
        {
            /// <summary>Per-platform bundle file size. Deliberately the loosest cap
            /// here: §8.5's download consent already puts the byte count in front of
            /// the player before anything is fetched, so ordinary size is consented
            /// cost, not ambush. This only stops the absurd.</summary>
            public const long BundleBytes = 1536L << 20;   // 1.5 GB

            /// <summary>Whole-package ceiling, as Steam reports an item's size. Derived
            /// rather than typed: a package carries BOTH platform bundles (Steam sizes
            /// the item, not the file a given player will load), plus a preview and a
            /// manifest, so two bundles and a little slack is the honest bound. This is
            /// what the pre-download consent refuses against — the only budget check
            /// that can happen before a byte is fetched.</summary>
            public const long PackageBytes = 2 * BundleBytes + (16L << 20);

            /// <summary>Runtime memory of the bundle's transitive asset set
            /// (<c>MapSizeReport.Measure</c>). THE cap that matters: every client in
            /// the lobby loads this, so it is the one that OOM-kills a match rather
            /// than merely costing a download. Export-time only — the profiler API it
            /// comes from reports nothing in a release player.</summary>
            public const long RuntimeBytes = 3L << 30;     // 3 GB

            /// <summary>A second sun is a lighting bug in every case we have ever
            /// seen, and two directional lights double full-scene shading.</summary>
            public const int DirectionalLights = 1;

            public const int RealtimeLights = 64;
            public const int AudioSources = 64;

            /// <summary>Non-kinematic rigidbodies. The physics bomb: every one is a
            /// solver island every client simulates for the whole round.</summary>
            public const int DynamicBodies = 128;

            public const int ParticleSystems = 64;
            public const int TotalParticles = 200_000;
        }

        /// <summary>Soft caps. A breach warns the creator and does nothing else —
        /// never checked at runtime, because these are craft problems, not safety
        /// ones, and the runtime's only honest response to a heavy map is to refuse
        /// it (see the class remarks).</summary>
        public static class Soft
        {
            public const long BundleBytes = 300L << 20;
            public const long RuntimeBytes = 1536L << 20;
            public const int RealtimeLights = 24;
            public const int AudioSources = 16;
            public const int DynamicBodies = 32;
            public const int ParticleSystems = 16;
            public const int TotalParticles = 50_000;
            public const int Renderers = 5_000;
            /// <summary>~2× Island_Decor's 5.75M. Deliberately not tighter: dense
            /// scatter geometry (our own ground cover is the heaviest thing in the
            /// game) is normal map-making, not a warning sign.</summary>
            public const long Vertices = 12_000_000;
            public const long ColliderVertices = 500_000;
        }

        /// <summary>A source is "2D" below this spatial blend, i.e. it plays at full
        /// volume everywhere in the map rather than falling off with distance. The
        /// grief shape is a 2D looping clip at max amplitude with playOnAwake, which
        /// fires the instant the scene loads and cannot be walked away from.</summary>
        public const float TwoDSpatialBlend = 0.5f;

        /// <summary>Ceiling on the SUMMED volume of a map's looping non-positional
        /// sources. A total rather than a per-source clamp because five sources at 1.0
        /// are five times as loud as one, and a per-source cap would pass all five.
        /// The runtime scales the whole group down to this; the export gate reports it
        /// as an error so no creator ships one thinking it plays as authored.</summary>
        public const float MaxLooping2DVolume = 1f;

        // ------------------------------------------------------------ the census

        /// <summary>What a map scene costs. Plain numbers, no verdict — the same
        /// struct the editor prints, the exporter records and the runtime re-derives.
        /// Serializable so it can ride in map.json as the advisory block.</summary>
        [System.Serializable]
        public struct Census
        {
            public int Renderers;
            public int RealtimeLights;
            public int DirectionalLights;
            public int BakedLights;
            /// <summary>Lights under an Animator/Animation. Advisory only: reading a
            /// curve to decide whether it strobes is not a thing we build at export,
            /// so the real defence is the runtime flash clamp.</summary>
            public int AnimatedLights;
            public int AudioSources;
            /// <summary>playOnAwake + loop + effectively 2D: the shape that plays at
            /// you the moment the map loads and never stops.</summary>
            public int Looping2DSources;
            public int ParticleSystems;
            public int DynamicBodies;
            public int MeshColliders;
            public long Vertices;
            public long ColliderVertices;
            public long TotalParticles;
            /// <summary>Summed authored volume of the <see cref="Looping2DSources"/>.</summary>
            public float Looping2DVolume;
        }

        /// <summary>Take the census of a map scene. Cheap enough to run on every map
        /// load: it reads component metadata and <c>Mesh.vertexCount</c>, never mesh
        /// data, so nothing here decompresses a mesh or touches a non-readable one.</summary>
        public static Census Take(Scene scene)
        {
            var shared = new Census();
            var perSize = new Census[3];

            // Size roots first, so every counted object can be attributed. A map with
            // no MapSizeVariants leaves this empty and everything lands in `shared`.
            var roots = new List<Transform>();
            MapSizeVariants variants = MapSizeVariants.FindInScene(scene);
            if (variants != null)
            {
                foreach (MapSize s in new[] { MapSize.Small, MapSize.Medium, MapSize.Large })
                {
                    GameObject r = variants.Root(s);
                    roots.Add(r != null ? r.transform : null);
                }
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                CountRenderers(root, roots, ref shared, perSize);
                CountLights(root, roots, ref shared, perSize);
                CountAudio(root, roots, ref shared, perSize);
                CountPhysics(root, roots, ref shared, perSize);
                CountParticles(root, roots, ref shared, perSize);
            }

            return Combine(shared, perSize);
        }

        // Which size root owns this transform: 0/1/2, or -1 for shared (outside every
        // size root, i.e. Base — live at every size).
        private static int SizeIndex(Transform t, List<Transform> roots)
        {
            for (int i = 0; i < roots.Count; i++)
                if (roots[i] != null && t.IsChildOf(roots[i])) return i;
            return -1;
        }

        // shared + the per-metric maximum across the size variants. Per-METRIC rather
        // than picking one "heaviest size" wholesale: a map whose Small is dense with
        // geometry and whose Large is dense with lights must be judged on both.
        private static Census Combine(Census shared, Census[] perSize)
        {
            Census c = shared;
            c.Renderers += Max(perSize, s => s.Renderers);
            c.RealtimeLights += Max(perSize, s => s.RealtimeLights);
            c.DirectionalLights += Max(perSize, s => s.DirectionalLights);
            c.BakedLights += Max(perSize, s => s.BakedLights);
            c.AnimatedLights += Max(perSize, s => s.AnimatedLights);
            c.AudioSources += Max(perSize, s => s.AudioSources);
            c.Looping2DSources += Max(perSize, s => s.Looping2DSources);
            c.ParticleSystems += Max(perSize, s => s.ParticleSystems);
            c.DynamicBodies += Max(perSize, s => s.DynamicBodies);
            c.MeshColliders += Max(perSize, s => s.MeshColliders);
            c.Vertices += MaxL(perSize, s => s.Vertices);
            c.ColliderVertices += MaxL(perSize, s => s.ColliderVertices);
            c.TotalParticles += MaxL(perSize, s => s.TotalParticles);
            c.Looping2DVolume += MaxF(perSize, s => s.Looping2DVolume);
            return c;
        }

        private static int Max(Census[] a, System.Func<Census, int> f)
        {
            int m = 0;
            for (int i = 0; i < a.Length; i++) m = Mathf.Max(m, f(a[i]));
            return m;
        }

        private static long MaxL(Census[] a, System.Func<Census, long> f)
        {
            long m = 0;
            for (int i = 0; i < a.Length; i++) m = System.Math.Max(m, f(a[i]));
            return m;
        }

        private static float MaxF(Census[] a, System.Func<Census, float> f)
        {
            float m = 0f;
            for (int i = 0; i < a.Length; i++) m = Mathf.Max(m, f(a[i]));
            return m;
        }

        private static void CountRenderers(GameObject root, List<Transform> roots, ref Census shared, Census[] perSize)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                int i = SizeIndex(r.transform, roots);
                if (i < 0) shared.Renderers++; else perSize[i].Renderers++;

                // vertexCount is metadata, so this costs nothing and works on a
                // non-readable mesh. Skinned meshes carry their own reference.
                Mesh mesh = null;
                if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null) mesh = mf.sharedMesh;
                }
                if (mesh == null) continue;
                if (i < 0) shared.Vertices += mesh.vertexCount; else perSize[i].Vertices += mesh.vertexCount;
            }
        }

        private static void CountLights(GameObject root, List<Transform> roots, ref Census shared, Census[] perSize)
        {
            foreach (Light l in root.GetComponentsInChildren<Light>(true))
            {
                int i = SizeIndex(l.transform, roots);
                // A fully baked light costs nothing at runtime — it is already in the
                // lightmap — so it must not count against a realtime cap. Mixed does
                // shade in realtime, so it does.
                bool realtime = l.lightmapBakeType != LightmapBakeType.Baked;
                bool animated = l.GetComponentInParent<Animator>() != null
                                || l.GetComponentInParent<Animation>() != null;

                if (i < 0)
                {
                    if (!realtime) shared.BakedLights++;
                    else if (l.type == LightType.Directional) { shared.DirectionalLights++; shared.RealtimeLights++; }
                    else shared.RealtimeLights++;
                    if (animated) shared.AnimatedLights++;
                }
                else
                {
                    if (!realtime) perSize[i].BakedLights++;
                    else if (l.type == LightType.Directional) { perSize[i].DirectionalLights++; perSize[i].RealtimeLights++; }
                    else perSize[i].RealtimeLights++;
                    if (animated) perSize[i].AnimatedLights++;
                }
            }
        }

        private static void CountAudio(GameObject root, List<Transform> roots, ref Census shared, Census[] perSize)
        {
            foreach (AudioSource a in root.GetComponentsInChildren<AudioSource>(true))
            {
                int i = SizeIndex(a.transform, roots);
                // Looping and non-positional: plays everywhere in the map, forever,
                // and cannot be walked away from. playOnAwake is deliberately NOT part
                // of the test — it decides when it starts, not whether it dominates —
                // and leaving it out is what keeps this number identical to the set
                // MapGovernor scales at runtime.
                bool shouty = a.loop && a.spatialBlend < TwoDSpatialBlend;
                if (i < 0)
                {
                    shared.AudioSources++;
                    if (shouty) { shared.Looping2DSources++; shared.Looping2DVolume += a.volume; }
                }
                else
                {
                    perSize[i].AudioSources++;
                    if (shouty) { perSize[i].Looping2DSources++; perSize[i].Looping2DVolume += a.volume; }
                }
            }
        }

        private static void CountPhysics(GameObject root, List<Transform> roots, ref Census shared, Census[] perSize)
        {
            foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
            {
                if (rb.isKinematic) continue;   // no solver island; costs nothing to speak of
                int i = SizeIndex(rb.transform, roots);
                if (i < 0) shared.DynamicBodies++; else perSize[i].DynamicBodies++;
            }
            foreach (MeshCollider mc in root.GetComponentsInChildren<MeshCollider>(true))
            {
                int i = SizeIndex(mc.transform, roots);
                int verts = mc.sharedMesh != null ? mc.sharedMesh.vertexCount : 0;
                if (i < 0) { shared.MeshColliders++; shared.ColliderVertices += verts; }
                else { perSize[i].MeshColliders++; perSize[i].ColliderVertices += verts; }
            }
        }

        private static void CountParticles(GameObject root, List<Transform> roots, ref Census shared, Census[] perSize)
        {
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                int i = SizeIndex(ps.transform, roots);
                int max = ps.main.maxParticles;
                if (i < 0) { shared.ParticleSystems++; shared.TotalParticles += max; }
                else { perSize[i].ParticleSystems++; perSize[i].TotalParticles += max; }
            }
        }

        // ------------------------------------------------------------ the verdict

        /// <summary>Judge a census. Hard breaches go in <paramref name="hard"/>
        /// (errors: block export, refuse at load), soft ones in
        /// <paramref name="soft"/> (warnings). Pass a negative
        /// <paramref name="bundleBytes"/> or <paramref name="runtimeBytes"/> to skip
        /// that check — the runtime knows the bundle's size but not its profiled
        /// memory, and Validate Map on an unexported scene knows neither.
        /// <paramref name="soft"/> may be null when only the blocking verdict is
        /// wanted (the runtime never reports soft breaches).
        /// <paramref name="heaviestAssets"/> is an optional "x.png (180.0 MB), …"
        /// line appended to the memory messages — the editor has it from
        /// <c>MapSizeReport.Measure</c>, the publish gate (which only re-reads a
        /// recorded number) does not.</summary>
        public static void Check(in Census c, long bundleBytes, long runtimeBytes,
                                 List<string> hard, List<string> soft, string heaviestAssets = null)
        {
            string heaviest = string.IsNullOrEmpty(heaviestAssets)
                ? "" : " Heaviest: " + heaviestAssets + ".";

            if (bundleBytes >= 0)
            {
                if (bundleBytes > Hard.BundleBytes)
                    hard.Add($"Bundle is {MB(bundleBytes)}, over the {MB(Hard.BundleBytes)} limit.");
                else if (bundleBytes > Soft.BundleBytes)
                    soft?.Add($"Bundle is {MB(bundleBytes)} — over the {MB(Soft.BundleBytes)} guideline. "
                        + "Everyone in the lobby downloads this before they can play. "
                        + "Run Cover Up!/Maps/Diagnostics/Map Size Report to see what's heaviest.");
            }

            if (runtimeBytes >= 0)
            {
                if (runtimeBytes > Hard.RuntimeBytes)
                    hard.Add($"Map assets need {MB(runtimeBytes)} of memory, over the {MB(Hard.RuntimeBytes)} limit. "
                        + "Every player loads all of this at once." + heaviest);
                else if (runtimeBytes > Soft.RuntimeBytes)
                    soft?.Add($"Map assets need {MB(runtimeBytes)} of memory — over the "
                        + $"{MB(Soft.RuntimeBytes)} guideline." + heaviest
                        + " Cover Up!/Maps/Diagnostics/Map Size Report breaks it down by asset and pack.");
            }

            if (c.DirectionalLights > Hard.DirectionalLights)
                hard.Add($"{c.DirectionalLights} realtime directional lights — a map gets {Hard.DirectionalLights}. "
                    + "A second sun doubles the shading cost of every surface and is almost always a mistake.");

            if (c.RealtimeLights > Hard.RealtimeLights)
                hard.Add($"{c.RealtimeLights} realtime lights, over the limit of {Hard.RealtimeLights}. "
                    + "Bake the ones that don't move (baked lights are free and don't count).");
            else if (c.RealtimeLights > Soft.RealtimeLights)
                soft?.Add($"{c.RealtimeLights} realtime lights — over the guideline of {Soft.RealtimeLights}. "
                    + "Baked lights don't count against this.");

            if (c.AudioSources > Hard.AudioSources)
                hard.Add($"{c.AudioSources} AudioSources, over the limit of {Hard.AudioSources}.");
            else if (c.AudioSources > Soft.AudioSources)
                soft?.Add($"{c.AudioSources} AudioSources — over the guideline of {Soft.AudioSources}.");

            // The player-safety one, and an error rather than a warning: a looping
            // non-positional clip plays at full volume everywhere in the map and
            // cannot be walked away from, so its total is the one audio number a
            // player has no defence against beyond leaving. The runtime scales the
            // group down to the same ceiling regardless — this exists so a creator
            // finds out at export instead of shipping a map that plays quieter than
            // they built it.
            if (c.Looping2DSources > 0 && c.Looping2DVolume > MaxLooping2DVolume)
                hard.Add($"{c.Looping2DSources} looping non-positional AudioSource(s) at a combined volume "
                    + $"of {c.Looping2DVolume:0.##}, over {MaxLooping2DVolume:0.##}. Give them a spatial "
                    + "blend so they fade with distance, or turn them down. In game the whole group is "
                    + "scaled to that ceiling and then held under the player's Game Sounds setting.");

            if (c.DynamicBodies > Hard.DynamicBodies)
                hard.Add($"{c.DynamicBodies} non-kinematic Rigidbodies, over the limit of {Hard.DynamicBodies}. "
                    + "Every client simulates all of them for the whole round.");
            else if (c.DynamicBodies > Soft.DynamicBodies)
                soft?.Add($"{c.DynamicBodies} non-kinematic Rigidbodies — over the guideline of "
                    + $"{Soft.DynamicBodies}. Tick Is Kinematic on anything that doesn't need to be pushed.");

            if (c.ParticleSystems > Hard.ParticleSystems)
                hard.Add($"{c.ParticleSystems} particle systems, over the limit of {Hard.ParticleSystems}.");
            else if (c.ParticleSystems > Soft.ParticleSystems)
                soft?.Add($"{c.ParticleSystems} particle systems — over the guideline of {Soft.ParticleSystems}.");

            if (c.TotalParticles > Hard.TotalParticles)
                hard.Add($"Particle systems can emit {c.TotalParticles:N0} particles at once, over the limit of "
                    + $"{Hard.TotalParticles:N0}. Lower Max Particles on the heaviest ones.");
            else if (c.TotalParticles > Soft.TotalParticles)
                soft?.Add($"Particle systems can emit {c.TotalParticles:N0} particles at once — over the "
                    + $"guideline of {Soft.TotalParticles:N0}.");

            if (c.Renderers > Soft.Renderers)
                soft?.Add($"{c.Renderers} renderers — over the guideline of {Soft.Renderers}. "
                    + "Consider combining static geometry.");

            if (c.Vertices > Soft.Vertices)
                soft?.Add($"{c.Vertices:N0} vertices — over the guideline of {Soft.Vertices:N0}.");

            if (c.ColliderVertices > Soft.ColliderVertices)
                soft?.Add($"Mesh colliders total {c.ColliderVertices:N0} vertices — over the guideline of "
                    + $"{Soft.ColliderVertices:N0}. A box or capsule collider on a detailed prop is far "
                    + "cheaper and plays the same.");

            if (c.AnimatedLights > 0)
                soft?.Add($"{c.AnimatedLights} light(s) are under an Animator. In game, how fast a map light "
                    + "may change is capped for photosensitivity (Docs/Steam.md §8.9), so a fast flicker "
                    + "will look slower than it does in the editor.");
        }

        private static string MB(long bytes) =>
            bytes >= (1L << 30) ? $"{bytes / (float)(1L << 30):0.0} GB" : $"{bytes / (float)(1L << 20):0.0} MB";
    }
}
