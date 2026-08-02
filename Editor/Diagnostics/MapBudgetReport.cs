using System.Text;
using CoverUp.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CoverUp.EditorTools
{
    /// <summary>
    /// The open map scene's resource census against the budget (Docs/Steam.md §8.9),
    /// as a table with every number and its cap side by side.
    ///
    /// Validate Map already reports the breaches; this reports the whole census
    /// whether or not anything breached, which is the difference between "am I in
    /// trouble" and "how much room do I have". It is also how the caps themselves get
    /// calibrated: run it on real maps, look at what real maps actually cost, and set
    /// the numbers from that rather than from a guess.
    ///
    /// Deliberately does NOT measure asset memory — that walks the whole dependency
    /// set and belongs to Map Size Report, which reports it far better.
    /// </summary>
    public static class MapBudgetReport
    {
        [MenuItem("Cover Up!/Maps/Diagnostics/Map Budget Report")]
        private static void RunFromMenu() => Run(EditorSceneManager.GetActiveScene());

        /// <summary>
        /// Batch-mode report on a named scene, so recalibrating the caps against real
        /// content is a command anyone can re-run rather than a thing someone did once:
        ///
        ///   Unity -batchmode -nographics -projectPath &lt;game&gt; \
        ///     -executeMethod CoverUp.EditorTools.MapBudgetReport.RunHeadless \
        ///     -scene Assets/CoverUp/Content/Scenes/Island_Decor.unity [-memory]
        ///
        /// <c>-memory</c> adds the asset-memory figure, which means walking the whole
        /// transitive dependency set — minutes on a real map, which is why it is opt-in
        /// here and off on the export-on-save path.
        /// </summary>
        public static void RunHeadless()
        {
            string path = null;
            bool memory = false;
            string[] argv = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length; i++)
            {
                if (argv[i] == "-memory") memory = true;
                else if (argv[i] == "-scene" && i + 1 < argv.Length) path = argv[i + 1];
            }

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[MapBudgetReport] pass -scene <path to a .unity>");
                EditorApplication.Exit(2);
                return;
            }
            Run(EditorSceneManager.OpenScene(path, OpenSceneMode.Single), memory ? path : null);
            EditorApplication.Exit(0);
        }

        private static void Run(UnityEngine.SceneManagement.Scene scene, string measurePath = null)
        {
            MapBudget.Census c = MapBudget.Take(scene);

            var sb = new StringBuilder();
            sb.AppendLine($"[MapBudgetReport] {scene.name}");
            sb.AppendLine();
            sb.AppendLine($"  {"",-28}{"count",12}{"soft",12}{"hard",12}");
            Row(sb, "renderers", c.Renderers, MapBudget.Soft.Renderers, null);
            Row(sb, "vertices", c.Vertices, MapBudget.Soft.Vertices, null);
            Row(sb, "mesh colliders", c.MeshColliders, null, null);
            Row(sb, "collider vertices", c.ColliderVertices, MapBudget.Soft.ColliderVertices, null);
            Row(sb, "realtime lights", c.RealtimeLights, MapBudget.Soft.RealtimeLights, MapBudget.Hard.RealtimeLights);
            Row(sb, "  of them directional", c.DirectionalLights, null, MapBudget.Hard.DirectionalLights);
            Row(sb, "  of them animated", c.AnimatedLights, null, null);
            Row(sb, "baked lights (free)", c.BakedLights, null, null);
            Row(sb, "audio sources", c.AudioSources, MapBudget.Soft.AudioSources, MapBudget.Hard.AudioSources);
            Row(sb, "  looping, non-positional", c.Looping2DSources, null, null);
            Row(sb, "particle systems", c.ParticleSystems, MapBudget.Soft.ParticleSystems, MapBudget.Hard.ParticleSystems);
            Row(sb, "max particles", c.TotalParticles, MapBudget.Soft.TotalParticles, MapBudget.Hard.TotalParticles);
            Row(sb, "dynamic rigidbodies", c.DynamicBodies, MapBudget.Soft.DynamicBodies, MapBudget.Hard.DynamicBodies);

            if (MapSizeVariants.FindInScene(scene) != null)
            {
                sb.AppendLine();
                sb.AppendLine("  Sized map: counts are shared + the heaviest single size, never the sum —");
                sb.AppendLine("  size roots are mutually exclusive, so no player ever pays for two at once.");
            }

            sb.AppendLine();
            if (measurePath != null)
            {
                MapSizeReport.Measurement m = MapSizeReport.Measure(measurePath, !Application.isBatchMode);
                sb.AppendLine($"  asset memory {m.TotalBytes / 1048576f:N0} MB "
                    + $"(soft {MapBudget.Soft.RuntimeBytes / 1048576f:N0} MB, "
                    + $"hard {MapBudget.Hard.RuntimeBytes / 1048576f:N0} MB)");
                sb.AppendLine($"  heaviest: {m.TopOffenders(5)}");
            }
            else
            {
                sb.AppendLine("  Asset memory and bundle size are not counted here — run Map Size Report.");
            }
            Debug.Log(sb.ToString());
        }

        private static void Row(StringBuilder sb, string label, long value, long? soft, long? hard)
        {
            bool overSoft = soft.HasValue && value > soft.Value;
            bool overHard = hard.HasValue && value > hard.Value;
            string mark = overHard ? "  ✗" : overSoft ? "  •" : "";
            sb.AppendLine($"  {label,-28}{value,12:N0}{(soft.HasValue ? soft.Value.ToString("N0") : "-"),12}"
                + $"{(hard.HasValue ? hard.Value.ToString("N0") : "-"),12}{mark}");
        }
    }
}
