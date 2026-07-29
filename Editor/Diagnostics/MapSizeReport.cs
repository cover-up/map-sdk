using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;

namespace CoverUp.EditorTools
{
    /// <summary>
    /// What is actually IN this map's bundle, by size. A one-off diagnostic: the export
    /// gives you one number and no way to attribute it, so tuning import settings turns
    /// into guess-and-re-export. This walks the scene's real dependency set — the same
    /// set BuildAssetBundles ships — and reports runtime size per asset, per type and
    /// per source pack.
    ///
    /// Runtime size is not compressed bundle size: the bundle is LZ4'd on top of this,
    /// so the absolute numbers run high. The RANKING is what matters, and that is what
    /// tells you where to spend effort.
    /// </summary>
    public static class MapSizeReport
    {
        [MenuItem("Cover Up!/Maps/Diagnostics/Map Size Report")]
        private static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError("[MapSizeReport] Save the scene to disk first.");
                return;
            }

            // recursive: true — a prefab pulls in its materials, which pull in their
            // textures. Anything short of the transitive set under-reports badly.
            string[] deps = AssetDatabase.GetDependencies(scene.path, true);

            var perAsset = new List<(string path, string type, long bytes)>();
            var perType = new Dictionary<string, long>();
            var perPack = new Dictionary<string, long>();

            try
            {
                for (int i = 0; i < deps.Length; i++)
                {
                    string path = deps[i];
                    if (path.EndsWith(".cs") || path.EndsWith(".shader")) continue;
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Map Size Report", path, i / (float)deps.Length)) return;

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

                    perAsset.Add((path, type, bytes));
                    perType.TryGetValue(type, out long t); perType[type] = t + bytes;
                    string pack = Pack(path);
                    perPack.TryGetValue(pack, out long p); perPack[pack] = p + bytes;
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            long total = perAsset.Sum(a => a.bytes);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[MapSizeReport] {scene.name}: {deps.Length} dependencies, {MB(total)} runtime total");

            sb.AppendLine("\n── by type ──");
            foreach (var kv in perType.OrderByDescending(k => k.Value))
                sb.AppendLine($"  {MB(kv.Value),10}  {Pct(kv.Value, total),5}  {kv.Key}");

            sb.AppendLine("\n── by source pack ──");
            foreach (var kv in perPack.OrderByDescending(k => k.Value).Take(15))
                sb.AppendLine($"  {MB(kv.Value),10}  {Pct(kv.Value, total),5}  {kv.Key}");

            sb.AppendLine("\n── 30 heaviest assets ──");
            foreach (var a in perAsset.OrderByDescending(x => x.bytes).Take(30))
                sb.AppendLine($"  {MB(a.bytes),10}  {a.type,-12}  {a.path}");

            Debug.Log(sb.ToString());
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
