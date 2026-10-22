using System.Collections.Generic;
using CoverUp.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CoverUp.EditorTools
{
    /// <summary>Corner handles for a <see cref="MapSeaHole"/>, the way the bounds outline has
    /// them: drag a corner on the object's plane, add after the selected one, remove it.</summary>
    [CustomEditor(typeof(MapSeaHole))]
    public sealed class MapSeaHoleEditor : Editor
    {
        private int _selected = -1;

        public override void OnInspectorGUI()
        {
            var hole = (MapSeaHole)target;
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(hole.Points.Count + " corners. Inside this outline there is no sea: not drawn, not shot, " +
                "not waded. Draw it around a dry floor that lies below Y 0 (a cave, a basement). In the scene view: drag a " +
                "corner to move it; the buttons add and remove corners. Keep it inside the map's sea disc (" +
                MapSeaHole.SeaDiscRadius.ToString("0") + " m from the origin).", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add corner (after selected)")) AddCorner(hole);
                using (new EditorGUI.DisabledScope(_selected < 0 || hole.Points.Count <= 3))
                    if (GUILayout.Button("Remove selected corner")) RemoveCorner(hole);
            }
        }

        private void AddCorner(MapSeaHole hole)
        {
            var pts = new List<Vector2>(hole.Points);
            int at = _selected >= 0 ? _selected : pts.Count - 1;
            Vector2 a = pts[at], b = pts[(at + 1) % pts.Count];
            Undo.RecordObject(hole, "Add sea hole corner");
            pts.Insert(at + 1, (a + b) * 0.5f);
            hole.SetPoints(pts); _selected = at + 1;
            EditorUtility.SetDirty(hole); SceneView.RepaintAll();
        }

        private void RemoveCorner(MapSeaHole hole)
        {
            var pts = new List<Vector2>(hole.Points);
            if (_selected < 0 || _selected >= pts.Count || pts.Count <= 3) return;
            Undo.RecordObject(hole, "Remove sea hole corner");
            pts.RemoveAt(_selected); hole.SetPoints(pts); _selected = -1;
            EditorUtility.SetDirty(hole); SceneView.RepaintAll();
        }

        private void OnSceneGUI()
        {
            var hole = (MapSeaHole)target;
            if (!MapBoundsVolume.ShowGizmos) return;
            Transform t = hole.transform;
            var pts = new List<Vector2>(hole.Points);
            bool changed = false;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 w = t.TransformPoint(new Vector3(pts[i].x, 0f, pts[i].y));
                float size = HandleUtility.GetHandleSize(w) * (i == _selected ? 0.12f : 0.08f);
                Handles.color = i == _selected ? Color.white : new Color(0.2f, 0.6f, 1f);
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.FreeMoveHandle(w, size, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    // keep the corner on the hole's own plane
                    Plane plane = new Plane(t.up, t.position);
                    Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                    if (plane.Raycast(ray, out float enter)) moved = ray.GetPoint(enter);
                    Vector3 local = t.InverseTransformPoint(moved);
                    pts[i] = new Vector2(local.x, local.z); _selected = i; changed = true;
                }
                else if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                         && HandleUtility.DistanceToCircle(w, size) < 1f)
                {
                    _selected = i; Repaint();
                }
            }
            if (changed)
            {
                Undo.RecordObject(hole, "Move sea hole corner");
                hole.SetPoints(pts); EditorUtility.SetDirty(hole);
            }
        }
    }
}
