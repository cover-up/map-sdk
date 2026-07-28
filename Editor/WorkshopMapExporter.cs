using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using CoverUp.Core;
using CoverUp.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.EditorTools
{
    /// <summary>
    /// P1 of Steam S6 (Docs/Steam.md §8.2): package the open _CoverUpMap scene as
    /// a Workshop map. Runs the Validate Map contract check, builds a
    /// self-contained AssetBundle per shipping platform (Windows + Linux), hashes
    /// each, writes map.json + preview.png, and zips the folder. Output lands in
    /// <project>/Workshop/&lt;mapId&gt;/ (+ .zip alongside).
    ///
    /// AssetBundles are platform-specific, so the editor's OWN platform bundle is
    /// required (that's the one you can sideload-test); the other platform is
    /// best-effort — a missing build module warns rather than aborts, so a Linux
    /// box still produces a testable Linux package.
    /// </summary>
    public static class WorkshopMapExporter
    {
        private struct Target { public string Key; public string File; public BuildTarget Build; }

        private static readonly Target[] Targets =
        {
            new Target { Key = "windows", File = "map.win.bundle",   Build = BuildTarget.StandaloneWindows64 },
            new Target { Key = "linux",   File = "map.linux.bundle", Build = BuildTarget.StandaloneLinux64 },
        };

        [MenuItem("Cover Up!/Maps/Export Workshop Map")]
        public static void Export() =>
            DoExport(SceneManager.GetActiveScene(), Targets, saveFirst: true, interactive: true);

        /// <summary>Auto-export path (on scene save): build ONLY the editor's own
        /// platform for speed, don't re-save the scene (it just was — re-saving
        /// would recurse), and report to the Console instead of a dialog.</summary>
        public static void QuickExport(Scene scene)
        {
            string key = WorkshopMapManifest.PlatformKey();
            Target[] one = Array.FindAll(Targets, t => t.Key == key);
            if (one.Length == 0) { Debug.LogWarning("[CoverUp] auto-export: unsupported platform."); return; }
            DoExport(scene, one, saveFirst: false, interactive: false);
        }

        private static void DoExport(Scene scene, Target[] targets, bool saveFirst, bool interactive)
        {
            if (string.IsNullOrEmpty(scene.path))
            {
                if (interactive) Fail("Save the scene to disk first.");
                return;
            }

            // 1. Contract must pass (same checks as Validate Map).
            var (errors, _, _) = MapSizeTools.Validate(scene);
            if (errors.Count > 0)
            {
                string msg = "Validate Map found problems — fix these first:\n• " + string.Join("\n• ", errors);
                if (interactive) Fail(msg); else Debug.LogWarning("[CoverUp] auto-export skipped — " + msg);
                return;
            }

            // 2. Metadata (optional WorkshopMapInfo on the _CoverUpMap root).
            WorkshopMapInfo info = FindInScene<WorkshopMapInfo>(scene);
            string mapId = SanitizeId(info != null && !string.IsNullOrWhiteSpace(info.MapId) ? info.MapId : scene.name);

            // 3. BuildAssetBundles reads assets from disk — persist the scene
            //    (skipped on the save-triggered path, which already saved it).
            if (saveFirst) EditorSceneManager.SaveScene(scene);

            // Export straight into the Local maps folder the game reads — the
            // creator's SDK and the game agree on it (Docs/MapCreatorGuide.md).
            string outRoot = LocalMapsFolder.Folder;
            string outDir = Path.Combine(outRoot, mapId);
            Directory.CreateDirectory(outDir);

            // 4. Build a self-contained scene bundle per platform.
            string editorKey = WorkshopMapManifest.PlatformKey(); // the bundle we can sandbox-test
            var bundles = new WorkshopBundles();
            string tempRoot = Path.Combine(outRoot, ".build", mapId); // intermediate; cleaned up after
            var builtKeys = new List<string>();
            try
            {
                foreach (Target t in targets)
                {
                    string built = BuildBundle(scene.path, mapId, tempRoot, t.Build);
                    if (built == null)
                    {
                        if (t.Key == editorKey)
                        {
                            string m = $"AssetBundle build failed for this editor's platform ({t.Key}). See the Console.";
                            if (interactive) Fail(m); else Debug.LogError("[CoverUp] auto-export — " + m);
                            return;
                        }
                        Debug.LogWarning($"[CoverUp] Skipped {t.Key} bundle — is the {t.Build} build module installed? "
                            + "The package will lack that platform until re-exported with it.");
                        continue;
                    }
                    // A bundle can BUILD and still be unloadable — most infamously when
                    // the project's manifest omits com.unity.modules.assetbundle, where
                    // BuildAssetBundles logs "module AssetBundle is disabled in the
                    // build", writes a file anyway, and returns a manifest. The game then
                    // rejects it ("not compatible with this newer version of the Unity
                    // runtime") and the mapper has already shipped. So prove the artifact
                    // instead of trusting the build result. Only for THIS editor's
                    // platform — a bundle for the other OS legitimately won't load here.
                    if (t.Key == editorKey && !BundleHasScene(built))
                    {
                        string m = $"The {t.Key} bundle built but contains no loadable scene — the map "
                            + "would fail to load in the game.\n\nMost likely this project is missing the "
                            + "AssetBundle module: add \"com.unity.modules.assetbundle\": \"1.0.0\" to "
                            + "Packages/manifest.json. Check the Console for "
                            + "\"module AssetBundle is disabled in the build\".";
                        if (interactive) Fail(m); else Debug.LogError("[CoverUp] auto-export — " + m);
                        return;
                    }

                    string dest = Path.Combine(outDir, t.File);
                    File.Copy(built, dest, true);
                    var reff = new WorkshopBundleRef { file = t.File, sha256 = Sha256(dest) };
                    if (t.Key == "windows") bundles.windows = reff; else bundles.linux = reff;
                    builtKeys.Add(t.Key);
                }
            }
            finally { TryDeleteDir(tempRoot); }

            // 5. Preview (best-effort) + contract (advisory) + manifest.
            string previewName = WritePreview(info, outDir);
            var manifest = new WorkshopMapManifest
            {
                format = WorkshopMapManifest.CurrentFormat,
                mapId = mapId,
                title = info != null && !string.IsNullOrWhiteSpace(info.Title) ? info.Title : mapId,
                author = "",
                authorSteamId = "",
                description = info != null ? info.Description : "",
                tags = info != null ? info.Tags : Array.Empty<string>(),
                scene = scene.name,
                bundles = bundles,
                preview = previewName,
                contract = ReadContract(scene),
                builtWith = new WorkshopBuiltWith
                {
                    game = Application.version,
                    unity = Application.unityVersion,
                    packageFormat = WorkshopMapManifest.CurrentFormat,
                },
                createdUtc = DateTime.UtcNow.ToString("o"),
            };
            File.WriteAllText(Path.Combine(outDir, WorkshopMapManifest.FileName), manifest.ToJson());

            // 6. Zip the content folder (this is the shareable "standalone file").
            string zipPath = Path.Combine(outRoot, mapId + ".zip");
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(outDir, zipPath);

            AssetDatabase.Refresh();
            Debug.Log($"[CoverUp] {(interactive ? "Exported" : "Auto-exported")} Workshop map '{mapId}' "
                + $"({string.Join("+", builtKeys)}) → {outDir}");
            if (interactive)
            {
                EditorUtility.DisplayDialog("Export Workshop Map",
                    $"Exported '{mapId}'  (platforms: {string.Join(", ", builtKeys)}).\n\n"
                    + $"Folder:\n{outDir}\n\n"
                    + "Pick it in the game's Sandbox (enter sandbox on the main menu). "
                    + "Re-export to hot-reload it live.", "OK");
            }
        }

        // Build ONE scene bundle for a target into a temp dir; returns the built
        // file path, or null if the build produced nothing (e.g. module missing).
        /// <summary>Load a freshly built bundle back and confirm it actually carries a
        /// scene — the same question the game's loader asks. Only valid for the running
        /// editor's own platform. Always unloads, so the file stays deletable.</summary>
        private static bool BundleHasScene(string bundlePath)
        {
            AssetBundle ab = null;
            try
            {
                ab = AssetBundle.LoadFromFile(bundlePath);
                return ab != null && ab.GetAllScenePaths().Length > 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CoverUp] couldn't verify bundle {bundlePath}: {e.Message}");
                return false;
            }
            finally { if (ab != null) ab.Unload(true); }
        }

        private static string BuildBundle(string scenePath, string mapId, string tempRoot, BuildTarget target)
        {
            string dir = Path.Combine(tempRoot, target.ToString());
            Directory.CreateDirectory(dir);
            var build = new AssetBundleBuild
            {
                assetBundleName = mapId + ".bundle",
                assetNames = new[] { scenePath },
            };
            AssetBundleManifest man;
            try
            {
                man = BuildPipeline.BuildAssetBundles(dir, new[] { build },
                    BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, target);
            }
            catch (Exception e) { Debug.LogWarning($"[CoverUp] {target} bundle build threw: {e.Message}"); return null; }
            if (man == null) return null;
            string[] names = man.GetAllAssetBundles(); // robust to Unity's name-casing
            if (names.Length == 0) return null;
            string built = Path.Combine(dir, names[0]);
            return File.Exists(built) ? built : null;
        }

        private static WorkshopContract ReadContract(Scene scene)
        {
            var c = new WorkshopContract();
            MapConfig cfg = FindInScene<MapConfig>(scene);
            float hider = cfg != null ? cfg.HiderScale : GameScale.Default;
            float hunter = cfg != null ? cfg.HunterScale : GameScale.Default;
            c.hiderScale = hider;
            c.hunterScale = hunter;
            c.approxHiderMeters = GameScale.ApproxHeightMeters(hider);
            c.approxHunterMeters = GameScale.ApproxHeightMeters(hunter);
            c.hasSpawn = FindInScene<MapSpawnDisc>(scene) != null;

            MapSizeVariants variants = MapSizeVariants.FindInScene(scene);
            if (variants == null)
            {
                // One-size map: the brackets can never fire (the clamp lands on
                // the only built size), so report the defaults rather than
                // implying an auto-size behaviour this map doesn't have.
                c.sizes = new[] { "OneSize" };
                c.autoSmallMaxPlayers = MapSizeRules.DefaultSmallMaxPlayers;
                c.autoMediumMaxPlayers = MapSizeRules.DefaultMediumMaxPlayers;
                return c;
            }
            (c.autoSmallMaxPlayers, c.autoMediumMaxPlayers) = variants.AutoThresholds;
            var sizes = new List<string>();
            if ((variants.BuiltMask & 1) != 0) sizes.Add("Small");
            if ((variants.BuiltMask & 2) != 0) sizes.Add("Medium");
            if ((variants.BuiltMask & 4) != 0) sizes.Add("Large");
            c.sizes = sizes.ToArray();
            return c;
        }

        private static string WritePreview(WorkshopMapInfo info, string outDir)
        {
            Texture2D tex = info != null ? info.Preview : null;
            if (tex == null)
            {
                Debug.LogWarning("[CoverUp] No preview assigned (WorkshopMapInfo.Preview) — package has no preview.png.");
                return "";
            }
            try
            {
                Texture2D readable = MakeReadable(tex);
                byte[] png = readable.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(readable);
                File.WriteAllBytes(Path.Combine(outDir, "preview.png"), png);
                return "preview.png";
            }
            catch (Exception e) { Debug.LogWarning("[CoverUp] Preview export failed: " + e.Message); return ""; }
        }

        // Copy any texture (regardless of Read/Write import setting) into a
        // readable Texture2D via a temporary RenderTexture blit.
        private static Texture2D MakeReadable(Texture2D src)
        {
            RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        // --------------------------------------------------------------- utils

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (T c in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.gameObject.scene == scene) return c;
            return null;
        }

        private static string Sha256(string file)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(file);
            byte[] hash = sha.ComputeHash(fs);
            var sb = new StringBuilder("sha256:", 71);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static string SanitizeId(string raw)
        {
            var sb = new StringBuilder(raw.Length);
            foreach (char ch in raw.ToLowerInvariant())
                sb.Append(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_');
            string s = sb.ToString().Trim('_');
            return string.IsNullOrEmpty(s) ? "map" : s;
        }

        private static void TryDeleteDir(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { /* temp; best-effort */ }
        }

        private static void Fail(string message)
        {
            Debug.LogError("[CoverUp] Export Workshop Map — " + message);
            EditorUtility.DisplayDialog("Export Workshop Map", message, "OK");
        }
    }
}
