using CoverUp.Core;
using CoverUp.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CoverUp.EditorTools
{
    /// <summary>
    /// Mapper-facing inspector for <see cref="MapConfig"/>: the two role scale
    /// sliders, plus a live "how tall is the doll" readout and one-click
    /// reference presets — so a bare 1.185 isn't a mystery number. Editing here
    /// still routes through the field's OnValidate, so the in-scene doll
    /// re-sizes live. No runtime behaviour; purely authoring UX.
    /// </summary>
    [CustomEditor(typeof(MapConfig))]
    public sealed class MapConfigEditor : Editor
    {
        // The documented reference points (GameScale). Custom values via slider.
        private static readonly (string label, float scale)[] Presets =
        {
            ("Tiny (~1 ft)", 0.23f),
            ("Human", GameScale.Default),
        };

        // On by default: most maps want both roles the same, and the link makes
        // that the one-drag case instead of two drags that must be kept equal.
        private static bool _linked = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty hider = serializedObject.FindProperty("hiderScale");
            SerializedProperty hunter = serializedObject.FindProperty("hunterScale");

            _linked = EditorGUILayout.ToggleLeft(
                new GUIContent("Link roles", "Keep hider and hunter at the same scale."),
                _linked);

            EditorGUILayout.PropertyField(hider, new GUIContent("Hider Scale"));
            using (new EditorGUI.DisabledScope(_linked))
            {
                EditorGUILayout.PropertyField(hunter, new GUIContent("Hunter Scale"));
            }
            if (_linked) hunter.floatValue = hider.floatValue;

            EditorGUILayout.HelpBox(
                $"Hider ≈ {GameScale.ApproxHeightMeters(hider.floatValue):0.00} m tall, " +
                $"hunter ≈ {GameScale.ApproxHeightMeters(hunter.floatValue):0.00} m.\n" +
                "Each scales that role's whole player — body, collider, movement, camera and " +
                "gun — not just the mesh. Exposure rings and scoring distances follow the HIDER " +
                "scale only, so sizing hunters never moves the balance. Applied once at map load " +
                $"(each clamped to {GameScale.MinScale:0.##}–{GameScale.MaxScale:0.##}).",
                MessageType.Info);

            float ratio = RoleRatio(hider.floatValue, hunter.floatValue);
            if (ratio > GameScale.AdvisedMaxRoleRatio)
            {
                EditorGUILayout.HelpBox(
                    $"The roles differ by {ratio:0.#}×. Past about {GameScale.AdvisedMaxRoleRatio:0.#}× " +
                    "the shared camera framing and the controller's step/skin tuning stop suiting " +
                    "both roles at once — walk both of them before shipping. Also note a hunter " +
                    "that big moves proportionally faster.",
                    MessageType.Warning);
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Presets");
            foreach ((string label, float value) in Presets)
            {
                string tip = $"≈ {GameScale.ApproxHeightMeters(value):0.00} m ({value:0.###})";
                if (!GUILayout.Button(new GUIContent(label, tip))) continue;
                hider.floatValue = value;
                if (_linked) hunter.floatValue = value;
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties(); // routes through OnValidate → live preview
        }

        private static float RoleRatio(float a, float b)
        {
            float lo = Mathf.Max(0.0001f, Mathf.Min(a, b));
            return Mathf.Max(a, b) / lo;
        }
    }
}
