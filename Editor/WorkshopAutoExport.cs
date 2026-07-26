using System;
using CoverUp.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.EditorTools
{
    /// <summary>
    /// Slice 3 of the sandbox loop: when a MAP scene is saved and this toggle is
    /// on, auto-export its package for the editor's OWN platform only (fast), so
    /// the workflow becomes "save in Unity → the running game hot-reloads the
    /// map". Toggle under <c>Cover Up! → Maps → Auto-Export On Save</c> (checkmark,
    /// off by default, remembered per-machine). Full both-platform exports stay on
    /// the manual <c>Export Workshop Map</c> menu / are done before publishing.
    /// See Docs/MapCreatorGuide.md.
    /// </summary>
    [InitializeOnLoad]
    public static class WorkshopAutoExport
    {
        private const string PrefKey = "CoverUp.Workshop.AutoExport";
        private const string MenuPath = "Cover Up!/Maps/Auto-Export On Save";

        static WorkshopAutoExport()
        {
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, false);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath)]
        private static void Toggle() => Enabled = !Enabled;

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void OnSceneSaved(Scene scene)
        {
            if (!Enabled || !IsMapScene(scene)) return;
            // BuildAssetBundles is heavy — defer it off the save callback so the
            // save completes first (and we never re-enter mid-save).
            string path = scene.path;
            EditorApplication.delayCall += () =>
            {
                Scene s = EditorSceneManager.GetSceneByPath(path);
                if (!s.IsValid() || !s.isLoaded) return;
                try { WorkshopMapExporter.QuickExport(s); }
                catch (Exception e) { Debug.LogWarning("[CoverUp] auto-export failed: " + e.Message); }
            };
        }

        // Only map scenes (carry the _CoverUpMap contract's MapConfig) auto-export.
        private static bool IsMapScene(Scene scene)
        {
            foreach (MapConfig c in UnityEngine.Object.FindObjectsByType<MapConfig>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c.gameObject.scene == scene) return true;
            }
            return false;
        }
    }
}
