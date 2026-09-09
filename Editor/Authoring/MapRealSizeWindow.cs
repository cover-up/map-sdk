using System.Text;
using CoverUp.Core;
using CoverUp.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.EditorTools
{
    /// <summary>
    /// The mapper's ruler: select an object, read what it currently measures, type what
    /// it really measures, press Fit. See <see cref="MapRealSize"/> for why a file cannot
    /// answer this on its own.
    ///
    /// Shows the map's own hider and hunter heights beside the readout, because "is 2.5 m
    /// right?" is only answerable against who has to hide behind it.
    /// </summary>
    public sealed class MapRealSizeWindow : EditorWindow
    {
        private const string PrefTarget = "CoverUp.MapRealSize.Target";
        private const string PrefAxis = "CoverUp.MapRealSize.Axis";
        private const int MaxRows = 8;

        // Fills the field, nothing more. Real-world sizes a mapper reaches for while
        // guessing a scan; the field stays the actual control.
        private static readonly (string label, float metres)[] Yardsticks =
        {
            ("Door 2.0", 2.0f),
            ("Person 1.7", 1.7f),
            ("Table 0.75", 0.75f),
            ("Bottle 0.25", 0.25f),
        };

        private float _target = 2f;
        private RealSizeAxis _axis = RealSizeAxis.Height;
        private string _lastResult;
        private MessageType _lastResultType = MessageType.Info;

        [MenuItem("Cover Up!/Maps/Real Size")]
        private static void Open()
        {
            var w = GetWindow<MapRealSizeWindow>("Real Size");
            w.minSize = new Vector2(320f, 320f);
            w.Show();
        }

        private void OnEnable()
        {
            _target = EditorPrefs.GetFloat(PrefTarget, 2f);
            _axis = (RealSizeAxis)EditorPrefs.GetInt(PrefAxis, (int)RealSizeAxis.Height);
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable() => Undo.undoRedoPerformed -= Repaint;
        private void OnSelectionChange() => Repaint();
        private void OnHierarchyChange() => Repaint();
        // Transform edits in the Inspector don't raise a hierarchy change; a 10 Hz
        // repaint keeps the readout honest while someone drags a scale handle.
        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            Transform[] roots = SceneSelection();

            DrawScaleLine();
            EditorGUILayout.Space();
            DrawReadout(roots);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            _target = EditorGUILayout.FloatField(new GUIContent("Real size (m)",
                "What the object measures in real life, in metres."), _target);
            _axis = (RealSizeAxis)EditorGUILayout.EnumPopup(new GUIContent("Refers to",
                "Which extent that number is. Height is the object's own up axis, whatever way " +
                "it is lying in the scene."), _axis);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetFloat(PrefTarget, _target);
                EditorPrefs.SetInt(PrefAxis, (int)_axis);
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Yardsticks");
            foreach ((string label, float metres) in Yardsticks)
            {
                if (!GUILayout.Button(label)) continue;
                _target = metres;
                EditorPrefs.SetFloat(PrefTarget, _target);
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(roots.Length == 0 || _target <= 0f))
            {
                if (GUILayout.Button($"Fit to {_target:0.##} m", GUILayout.Height(28f))) Fit(roots);
            }
            using (new EditorGUI.DisabledScope(roots.Length == 0))
            {
                if (GUILayout.Button(new GUIContent("Save as prefab variant",
                        "Write the fitted object out as a variant beside its source asset, so every " +
                        "future drag-in is already this size. Re-saving an existing variant updates it.")))
                    Save(roots);
            }

            if (!string.IsNullOrEmpty(_lastResult))
                EditorGUILayout.HelpBox(_lastResult, _lastResultType);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Height is the object's own up axis. Everything rendered under the selection " +
                "counts, plinth included; select a child to fit the figure alone. The fit is always " +
                "uniform, so proportions never change. Ctrl+Z undoes it.",
                MessageType.None);
        }

        // Top-level scene objects only. Selecting a parent and its child fits the
        // parent once, never both; an asset picked in the Project window is skipped.
        private static Transform[] SceneSelection()
        {
            Transform[] all = Selection.GetTransforms(SelectionMode.TopLevel | SelectionMode.Editable);
            int n = 0;
            foreach (Transform t in all) if (t != null && t.gameObject.scene.IsValid()) n++;
            if (n == all.Length) return all;
            var list = new Transform[n];
            int i = 0;
            foreach (Transform t in all) if (t != null && t.gameObject.scene.IsValid()) list[i++] = t;
            return list;
        }

        private static void DrawScaleLine()
        {
            Scene scene = SceneManager.GetActiveScene();
            MapConfig cfg = null;
            foreach (MapConfig c in FindObjectsByType<MapConfig>(FindObjectsInactive.Include))
                if (c.gameObject.scene == scene) { cfg = c; break; }

            float hider = GameScale.ApproxHeightMeters(cfg != null ? cfg.HiderScale : GameScale.Default);
            float hunter = GameScale.ApproxHeightMeters(cfg != null ? cfg.HunterScale : GameScale.Default);
            string src = cfg != null ? "this map's MapConfig" : "default scale, no MapConfig";
            EditorGUILayout.LabelField(
                $"For comparison: hider ≈ {hider:0.00} m, hunter ≈ {hunter:0.00} m ({src}).",
                EditorStyles.miniLabel);
        }

        private static void DrawReadout(Transform[] roots)
        {
            EditorGUILayout.LabelField("Selected, as it stands now", EditorStyles.boldLabel);
            if (roots.Length == 0)
            {
                EditorGUILayout.HelpBox("Select an object in the scene to measure it.", MessageType.Info);
                return;
            }

            int shown = 0;
            foreach (Transform root in roots)
            {
                if (shown++ == MaxRows)
                {
                    EditorGUILayout.LabelField($"… and {roots.Length - MaxRows} more");
                    break;
                }
                string line = MapRealSize.TryMeasure(root, out Vector3 s)
                    ? $"W {s.x:0.00}   H {s.y:0.00}   D {s.z:0.00} m"
                    : "nothing rendered under it";
                EditorGUILayout.LabelField(root.name, line);
            }
        }

        private void Fit(Transform[] roots)
        {
            var sb = new StringBuilder();
            int fitted = 0;
            foreach (Transform root in roots)
            {
                bool measured = MapRealSize.TryMeasure(root, out Vector3 before);
                float k = MapRealSize.Fit(root, _axis, _target);
                if (k <= 0f)
                {
                    sb.AppendLine(measured
                        ? $"{root.name}: already {_target:0.##} m."
                        : $"{root.name}: nothing rendered under it, skipped.");
                    continue;
                }
                fitted++;
                float was = MapRealSize.Extent(before, _axis);
                sb.AppendLine($"{root.name}: {_axis} {was:0.00} → {_target:0.00} m  (×{k:0.###})");
            }
            _lastResult = sb.ToString().TrimEnd();
            _lastResultType = fitted > 0 ? MessageType.Info : MessageType.Warning;
            Debug.Log("Real Size\n" + _lastResult);
        }

        private void Save(Transform[] roots)
        {
            var sb = new StringBuilder();
            bool anyProblem = false;
            foreach (Transform root in roots)
            {
                if (MapRealSize.TrySaveAsPrefab(root.gameObject, out string path, out string problem))
                    sb.AppendLine($"{root.name} → {path}");
                else
                {
                    anyProblem = true;
                    sb.AppendLine(problem);
                }
            }
            _lastResult = sb.ToString().TrimEnd();
            _lastResultType = anyProblem ? MessageType.Warning : MessageType.Info;
            Debug.Log("Real Size\n" + _lastResult);
        }
    }
}
