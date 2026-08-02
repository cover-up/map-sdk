using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;

namespace CoverUp.EditorTools
{
    /// <summary>
    /// What is actually IN this map's bundle, by size. Started as a one-off
    /// diagnostic: the export gives you one number and no way to attribute it, so
    /// tuning import settings turns into guess-and-re-export. This walks the scene's
    /// real dependency set — the same set BuildAssetBundles ships — and reports
    /// runtime size per asset, per type and per source pack.
    ///
    /// Runtime size is not compressed bundle size: the bundle is LZ4'd on top of this,
    /// so the absolute numbers run high. The RANKING is what matters, and that is what
    /// tells you where to spend effort.
    ///
    /// <para><see cref="Measure"/> is the measurement and <see cref="Run"/> is the
    /// menu item that prints it. They are split because the memory budget
    /// (<c>MapBudget.Hard.RuntimeBytes</c>, Docs/Steam.md §8.9) is enforced from the
    /// same numbers — before the split this walked the whole dependency set, logged it
    /// and threw it away, so the one place in the pipeline that knew what a map
    /// weighed was the one place that could not act on it.</para>
    /// </summary>
    public static class MapSizeReport
    {
        /// <summary>One asset's contribution to the bundle.</summary>
        public readonly struct Entry
        {
            public readonly string Path;
            public readonly string Type;
            public readonly long Bytes;
            public Entry(string path, string type, long bytes) { Path = path; Type = type; Bytes = bytes; }
        }

        /// <summary>The measured weight of a scene's transitive dependency set.
        /// <see cref="Cancelled"/> means the user aborted the progress bar, and every
        /// other field is then incomplete — callers that enforce a budget must treat a
        /// cancelled measurement as "unknown", never as "under budget".</summary>
        public readonly struct Measurement
        {
            public readonly long TotalBytes;
            public readonly int Dependencies;
            public readonly IReadOnlyList<Entry> Assets;          // heaviest first
            public readonly IReadOnlyDictionary<string, long> PerType;
            public readonly IReadOnlyDictionary<string, long> PerPack;
            public readonly bool Cancelled;

            public Measurement(long total, int deps, IReadOnlyList<Entry> assets,
                               IReadOnlyDictionary<string, long> perType,
                               IReadOnlyDictionary<string, long> perPack, bool cancelled)
            {
                TotalBytes = total; Dependencies = deps; Assets = assets;
                PerType = perType; PerPack = perPack; Cancelled = cancelled;
            }

            /// <summary>The heaviest assets as one short human line, for a budget
            /// error. A cap that names the 180 MB texture gets fixed; a cap that only
            /// says "too big" gets cursed.</summary>
            public string TopOffenders(int count)
            {
                if (Assets == null || Assets.Count == 0) return "";
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < Mathf.Min(count, Assets.Count); i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append($"{Assets[i].Path} ({MB(Assets[i].Bytes)})");
                }
                return sb.ToString();
            }
        }

        [MenuItem("Cover Up!/Maps/Diagnostics/Map Size Report")]
        private static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError("[MapSizeReport] Save the scene to disk first.");
                return;
            }

            Measurement m = Measure(scene.path, showProgress: true);
            if (m.Cancelled) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[MapSizeReport] {scene.name}: {m.Dependencies} dependencies, "
                + $"{MB(m.TotalBytes)} runtime total");

            sb.AppendLine("\n── by type ──");
            foreach (var kv in m.PerType.OrderByDescending(k => k.Value))
                sb.AppendLine($"  {MB(kv.Value),10}  {Pct(kv.Value, m.TotalBytes),5}  {kv.Key}");

            sb.AppendLine("\n── by source pack ──");
            foreach (var kv in m.PerPack.OrderByDescending(k => k.Value).Take(15))
                sb.AppendLine($"  {MB(kv.Value),10}  {Pct(kv.Value, m.TotalBytes),5}  {kv.Key}");

            sb.AppendLine("\n── 30 heaviest assets ──");
            foreach (Entry a in m.Assets.Take(30))
                sb.AppendLine($"  {MB(a.Bytes),10}  {a.Type,-12}  {a.Path}");

            Debug.Log(sb.ToString());
        }

        /// <summary>Walk a scene's transitive dependency set and measure it. Slow (it
        /// loads every dependency), so callers that run on the save path skip it —
        /// see <c>MapSizeTools.Validate</c>'s <c>measureMemory</c> flag.
        /// <paramref name="showProgress"/> draws a cancelable bar; pass false from
        /// batch mode, where there is no one to cancel it and the bar would be an
        /// unclosable dialog.</summary>
        public static Measurement Measure(string scenePath, bool showProgress)
        {
            // recursive: true — a prefab pulls in its materials, which pull in their
            // textures. Anything short of the transitive set under-reports badly.
            string[] deps = AssetDatabase.GetDependencies(scenePath, true);

            var perAsset = new List<Entry>();
            var perType = new Dictionary<string, long>();
            var perPack = new Dictionary<string, long>();
            bool cancelled = false;

            try
            {
                for (int i = 0; i < deps.Length; i++)
                {
                    string path = deps[i];
                    if (path.EndsWith(".cs") || path.EndsWith(".shader")) continue;
                    if (showProgress && EditorUtility.DisplayCancelableProgressBar(
                            "Map Size Report", path, i / (float)deps.Length))
                    {
                        cancelled = true;
                        break;
                    }

                    long bytes = 0;
                    string type = "?";
                    foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if (o == null) continue;
                        bytes += Profiler.GetRuntimeMemorySizeLong(o);
                        // The heaviest sub-object names the asset — a .fbx reports as
                        // Mesh, not GameObject, which is what you actually want to see.
                        if (o is Mesh || o is Texture || o is AudioClip) type = o.GetType().Name;
                        else if (type == "?") type = o.GetType().Name;
                    }
                    if (bytes <= 0) continue;

                    perAsset.Add(new Entry(path, type, bytes));
                    perType.TryGetValue(type, out long t); perType[type] = t + bytes;
                    string pack = Pack(path);
                    perPack.TryGetValue(pack, out long p); perPack[pack] = p + bytes;
                }
            }
            finally { if (showProgress) EditorUtility.ClearProgressBar(); }

            perAsset.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));
            long total = 0;
            foreach (Entry e in perAsset) total += e.Bytes;
            return new Measurement(total, deps.Length, perAsset, perType, perPack, cancelled);
        }

        // Second path segment: "Assets/QA_Books/Textures/x.tga" → "QA_Books". Vendor
        // packs are the unit you actually act on (cap it, or delete it).
        private static string Pack(string path)
        {
            string[] parts = path.Split('/');
            return parts.Length > 1 ? parts[1] : path;
        }

        private static string MB(long b) => $"{b / 1048576f:0.0} MB";
        private static string Pct(long part, long whole) => whole > 0 ? $"{100f * part / whole:0}%" : "-";
    }

}
