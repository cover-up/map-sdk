using System.Collections.Generic;
using CoverUp.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CoverUp.EditorTools
{
    /// <summary>
    /// Drawing tool for <see cref="MapBoundsPolygon"/>: drag the corners in the scene view, add a
    /// corner on an edge's midpoint, remove one, and TRACE the outline from the scene's colliders
    /// at a water level and depth (an island's coast a metre or two into the sea) with an outward
    /// offset and a simplify tolerance. Tracing writes the points; from then on they are the
    /// mapper's to drag.
    /// </summary>
    [CustomEditor(typeof(MapBoundsPolygon))]
    public sealed class MapBoundsPolygonEditor : Editor
    {
        private static float _waterY, _depth = 0.16f, _offset = 0f, _step = 0.5f, _tolerance = 0.3f, _extent = 80f;
        private static bool _waterFromEnvironment = true;
        private int _selected = -1;

        public override void OnInspectorGUI()
        {
            var poly = (MapBoundsPolygon)target;
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(poly.Points.Count + " corners. In the scene view: drag a corner to move it; the buttons below add and remove corners. " +
                "The fence is the closed outline between Floor and Ceiling (local Y).", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add corner (after selected)")) AddCorner(poly);
                using (new EditorGUI.DisabledScope(_selected < 0 || poly.Points.Count <= 3))
                    if (GUILayout.Button("Remove selected corner")) RemoveCorner(poly);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Trace from the scene", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Rays are cast down over the area around this object, against every collider in the scene; the outline follows the " +
                "height contour at (water level - depth), pushed out by the offset, then simplified. For an island: water level = the sea, " +
                "depth = how deep the water is at the fence.", MessageType.None);
            _waterFromEnvironment = EditorGUILayout.Toggle("Water level from MapEnvironment", _waterFromEnvironment);
            MapEnvironment env = _waterFromEnvironment ? MapEnvironment.FindInScene(poly.gameObject.scene) : null;
            using (new EditorGUI.DisabledScope(env != null))
                _waterY = EditorGUILayout.FloatField("Water level (world Y)", env != null ? env.SeaLevel : _waterY);
            _depth = EditorGUILayout.FloatField("Depth at the fence (m)", _depth);
            _offset = EditorGUILayout.FloatField("Push out by (m)", _offset);
            _step = Mathf.Max(0.1f, EditorGUILayout.FloatField("Sample step (m)", _step));
            _tolerance = Mathf.Max(0f, EditorGUILayout.FloatField("Simplify tolerance (m)", _tolerance));
            _extent = Mathf.Max(5f, EditorGUILayout.FloatField("Search radius (m)", _extent));
            if (GUILayout.Button("Trace outline"))
            {
                float water = env != null ? env.SeaLevel : _waterY;
                Trace(poly, water - _depth, _offset, _step, _tolerance, _extent);
            }
        }

        private void AddCorner(MapBoundsPolygon poly)
        {
            var pts = new List<Vector2>(poly.Points);
            int at = _selected >= 0 ? _selected : pts.Count - 1;
            Vector2 a = pts[at], b = pts[(at + 1) % pts.Count];
            Undo.RecordObject(poly, "Add bounds corner");
            pts.Insert(at + 1, (a + b) * 0.5f);
            poly.SetPoints(pts); _selected = at + 1;
            EditorUtility.SetDirty(poly); SceneView.RepaintAll();
        }

        private void RemoveCorner(MapBoundsPolygon poly)
        {
            var pts = new List<Vector2>(poly.Points);
            if (_selected < 0 || _selected >= pts.Count || pts.Count <= 3) return;
            Undo.RecordObject(poly, "Remove bounds corner");
            pts.RemoveAt(_selected); poly.SetPoints(pts); _selected = -1;
            EditorUtility.SetDirty(poly); SceneView.RepaintAll();
        }

        private void OnSceneGUI()
        {
            var poly = (MapBoundsPolygon)target;
            if (!MapBoundsVolume.ShowGizmos) return;
            Transform t = poly.transform;
            var pts = new List<Vector2>(poly.Points);
            bool changed = false;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 w = t.TransformPoint(new Vector3(pts[i].x, poly.Floor, pts[i].y));
                float size = HandleUtility.GetHandleSize(w) * (i == _selected ? 0.12f : 0.08f);
                Handles.color = i == _selected ? Color.white : new Color(1f, 0.6f, 0.2f);
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.FreeMoveHandle(w, size, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    // keep the corner on the fence's own floor plane
                    Plane floorPlane = new Plane(t.up, t.TransformPoint(new Vector3(0f, poly.Floor, 0f)));
                    Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                    if (floorPlane.Raycast(ray, out float enter)) moved = ray.GetPoint(enter);
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
                Undo.RecordObject(poly, "Move bounds corner");
                poly.SetPoints(pts); EditorUtility.SetDirty(poly);
            }
        }

        // ------------------------------------------------------------------ tracing
        /// <summary>Marching-squares contour of the collider height field at <paramref name="levelY"/>,
        /// the longest closed loop around this object, pushed out and simplified.</summary>
        private void Trace(MapBoundsPolygon poly, float levelY, float offset, float step, float tolerance, float extent)
        {
            Transform t = poly.transform;
            Vector3 centre = t.position;
            int n = Mathf.CeilToInt(extent * 2f / step) + 1;
            float x0 = centre.x - extent, z0 = centre.z - extent;
            var h = new float[n, n];
            float top = centre.y + 500f;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    var origin = new Vector3(x0 + i * step, top, z0 + j * step);
                    h[i, j] = Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2000f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : levelY - 1000f;
                }
            // marching squares: collect segments where the field crosses levelY
            var segs = new List<(Vector2 a, Vector2 b)>();
            Vector2 P(int i, int j) => new Vector2(x0 + i * step, z0 + j * step);
            Vector2 Lerp(Vector2 a, float fa, Vector2 b, float fb) { float tt = Mathf.Clamp01((levelY - fa) / Mathf.Max(1e-6f, fb - fa)); return Vector2.Lerp(a, b, tt); }
            for (int j = 0; j < n - 1; j++)
                for (int i = 0; i < n - 1; i++)
                {
                    float f00 = h[i, j], f10 = h[i + 1, j], f11 = h[i + 1, j + 1], f01 = h[i, j + 1];
                    int c = (f00 > levelY ? 1 : 0) | (f10 > levelY ? 2 : 0) | (f11 > levelY ? 4 : 0) | (f01 > levelY ? 8 : 0);
                    if (c == 0 || c == 15) continue;
                    Vector2 p00 = P(i, j), p10 = P(i + 1, j), p11 = P(i + 1, j + 1), p01 = P(i, j + 1);
                    Vector2 eB = Lerp(p00, f00, p10, f10), eR = Lerp(p10, f10, p11, f11), eT = Lerp(p01, f01, p11, f11), eL = Lerp(p00, f00, p01, f01);
                    switch (c)
                    {
                        case 1: case 14: segs.Add((eL, eB)); break;
                        case 2: case 13: segs.Add((eB, eR)); break;
                        case 3: case 12: segs.Add((eL, eR)); break;
                        case 4: case 11: segs.Add((eR, eT)); break;
                        case 5: segs.Add((eL, eT)); segs.Add((eB, eR)); break;
                        case 6: case 9: segs.Add((eB, eT)); break;
                        case 7: case 8: segs.Add((eL, eT)); break;
                        case 10: segs.Add((eL, eB)); segs.Add((eR, eT)); break;
                    }
                }
            if (segs.Count < 3) { Debug.LogWarning("[MapBoundsPolygon] Trace found no contour at Y = " + levelY.ToString("0.00") + " within " + extent + " m."); return; }

            // chain segments into loops by shared endpoints (snapped to a fine grid)
            var pointIndex = new Dictionary<Vector2Int, List<int>>();
            Vector2Int Key(Vector2 p) => new Vector2Int(Mathf.RoundToInt(p.x * 200f), Mathf.RoundToInt(p.y * 200f));
            for (int s = 0; s < segs.Count; s++)
            {
                foreach (var k in new[] { Key(segs[s].a), Key(segs[s].b) })
                { if (!pointIndex.TryGetValue(k, out var l)) pointIndex[k] = l = new List<int>(); l.Add(s); }
            }
            var used = new bool[segs.Count]; List<Vector2> best = null; float bestLen = 0f;
            for (int s = 0; s < segs.Count; s++)
            {
                if (used[s]) continue;
                var loop = new List<Vector2> { segs[s].a, segs[s].b }; used[s] = true; float len = (segs[s].b - segs[s].a).magnitude;
                Vector2 cur = segs[s].b; bool closed = false;
                for (int guard = 0; guard < segs.Count; guard++)
                {
                    if (!pointIndex.TryGetValue(Key(cur), out var cands)) break;
                    int next = -1; foreach (int c in cands) if (!used[c]) { next = c; break; }
                    if (next < 0) break;
                    used[next] = true;
                    Vector2 far = Key(segs[next].a) == Key(cur) ? segs[next].b : segs[next].a;
                    len += (far - cur).magnitude; cur = far;
                    if (Key(cur) == Key(loop[0])) { closed = true; break; }
                    loop.Add(cur);
                }
                if (closed && len > bestLen) { bestLen = len; best = loop; }
            }
            if (best == null) { Debug.LogWarning("[MapBoundsPolygon] Trace found contour pieces but no closed loop; widen the search radius."); return; }

            // push out along the outward normal (outward = away from the loop's interior)
            if (Mathf.Abs(offset) > 1e-4f)
            {
                bool ccw = SignedArea(best) > 0f; var pushed = new List<Vector2>(best.Count); int m = best.Count;
                for (int i = 0; i < m; i++)
                {
                    Vector2 prev = best[(i - 1 + m) % m], next = best[(i + 1) % m];
                    Vector2 dir = (next - prev).normalized; Vector2 nrm = new Vector2(dir.y, -dir.x); if (ccw) nrm = -nrm;
                    pushed.Add(best[i] + nrm * offset);
                }
                best = pushed;
            }
            best = Simplify(best, tolerance);
            var local = new List<Vector2>(best.Count);
            foreach (Vector2 w in best) { Vector3 l = t.InverseTransformPoint(new Vector3(w.x, levelY, w.y)); local.Add(new Vector2(l.x, l.z)); }
            Undo.RecordObject(poly, "Trace bounds outline");
            poly.SetPoints(local); EditorUtility.SetDirty(poly); SceneView.RepaintAll();
            Debug.Log("[MapBoundsPolygon] Traced " + local.Count + " corners (" + bestLen.ToString("0") + " m around) at Y = " + levelY.ToString("0.00") + ".");
        }

        private static float SignedArea(List<Vector2> loop)
        {
            float a = 0f; for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++) a += loop[j].x * loop[i].y - loop[i].x * loop[j].y; return a * 0.5f;
        }

        // Douglas-Peucker on a closed loop: split at the two farthest-apart points, simplify both halves.
        private static List<Vector2> Simplify(List<Vector2> loop, float tol)
        {
            if (tol <= 0f || loop.Count < 8) return loop;
            int a = 0, b = 0; float far = -1f;
            for (int i = 0; i < loop.Count; i++) { float d = (loop[i] - loop[0]).sqrMagnitude; if (d > far) { far = d; b = i; } }
            var first = loop.GetRange(a, b - a + 1); var second = loop.GetRange(b, loop.Count - b); second.Add(loop[0]);
            var outp = new List<Vector2>(); outp.AddRange(Dp(first, tol)); var s2 = Dp(second, tol); outp.AddRange(s2.GetRange(1, s2.Count - 2));
            return outp;
        }

        private static List<Vector2> Dp(List<Vector2> pts, float tol)
        {
            if (pts.Count < 3) return new List<Vector2>(pts);
            float maxD = 0f; int idx = 0; Vector2 a = pts[0], b = pts[pts.Count - 1];
            for (int i = 1; i < pts.Count - 1; i++)
            {
                Vector2 ab = b - a; float len2 = ab.sqrMagnitude; float tt = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(pts[i] - a, ab) / len2) : 0f;
                float d = (pts[i] - (a + ab * tt)).magnitude; if (d > maxD) { maxD = d; idx = i; }
            }
            if (maxD <= tol) return new List<Vector2> { a, b };
            var left = Dp(pts.GetRange(0, idx + 1), tol); var right = Dp(pts.GetRange(idx, pts.Count - idx), tol);
            left.AddRange(right.GetRange(1, right.Count - 1)); return left;
        }
    }
}
