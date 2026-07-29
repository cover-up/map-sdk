using CoverUp.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CoverUp.EditorTools
{
    /// <summary>
    /// Editor-wide show/hide for the keep-in bounds gizmos — a checkable
    /// menu item so the orange boxes can get out of the way while
    /// decorating. Display only: enforcement never turns off.
    /// </summary>
    public static class MapBoundsGizmos
    {
        private const string MenuPath = "Cover Up!/Maps/Show Bounds Volumes";
        private const string PrefKey = "CoverUp.ShowBoundsVolumes";

        [InitializeOnLoadMethod]
        private static void Restore()
        {
            MapBoundsVolume.ShowGizmos = EditorPrefs.GetBool(PrefKey, true);
            // The menu doesn't exist yet during InitializeOnLoad — check it a tick later.
            EditorApplication.delayCall += () => Menu.SetChecked(MenuPath, MapBoundsVolume.ShowGizmos);
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            MapBoundsVolume.ShowGizmos = !MapBoundsVolume.ShowGizmos;
            EditorPrefs.SetBool(PrefKey, MapBoundsVolume.ShowGizmos);
            Menu.SetChecked(MenuPath, MapBoundsVolume.ShowGizmos);
            SceneView.RepaintAll();
        }
    }
}
