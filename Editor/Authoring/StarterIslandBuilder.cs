using System.Collections.Generic;
using CoverUp.Core;
using CoverUp.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.EditorTools
{
    /// <summary>
    /// The map a mapper starts from (SDK 0.22, Docs/OneSeaDesign.md §8): a small island
    /// that already follows every rule. Land rising from the sea at local Y 0, the hub's
    /// profile (a flat sand beach, a 1:20 shelf into knee-deep water), the bounds drawn in
    /// that water, the spawns on the plateau, the arena sun, the island sky, Tiny and
    /// Small built, and one open-sided shed as the example of "no interiors" (a room with
    /// one open side, sun reaching in, nothing to walk through a door to see). Delete it
    /// or grow it; Validate Map tells you what the contract still wants.
    ///
    /// Everything is generated from a seedless profile so the scene is the same on every
    /// machine; the ground is a saved mesh asset, the materials plain URP/Lit.
    /// </summary>
    public static class StarterIslandBuilder
    {
        private const string GameSceneFolder = "Assets/CoverUp/Content/Scenes";
        private const string GameAssetFolder = "Assets/CoverUp/Content/Materials";
        private const string MapSceneFolder = "Assets/Maps/Scenes";
        private const string MapAssetFolder = "Assets/Maps/Materials";
        private const string SceneName = "starter_island";

        private static bool InGameProject => System.IO.Directory.Exists(GameSceneFolder);
        private static string SceneFolder => InGameProject ? GameSceneFolder : MapSceneFolder;
        private static string AssetFolder => InGameProject ? GameAssetFolder : MapAssetFolder;

        // The island's shape, in metres. Hub pattern: waterline about 18 m out, the shelf
        // 10 m wide to -0.5 m (1:20), then the seabed falls away; a 2.5 m plateau inside.
        private const float Waterline = 18f;
        private const float ShelfWidth = 10f;
        private const float ShelfDepth = 0.5f;
        private const float Plateau = 2.5f;
        private const float KneeDeep = 0.4f;     // where the bounds stand (Docs/IslandMapHowTo.md)
        private const float MeshHalf = 50f;      // the ground mesh's half size
        private const float Cell = 2f;
        private const float BoundsFloor = -2f, BoundsCeiling = 30f;
        private const int OutlineCorners = 36;

        private static readonly Color SandCol = new Color(0.86f, 0.78f, 0.58f);
        private static readonly Color GrassCol = new Color(0.55f, 0.62f, 0.26f);
        private static readonly Color WoodCol = new Color(0.52f, 0.36f, 0.22f);
        private static readonly Color RockCol = new Color(0.55f, 0.52f, 0.48f);

        public static void Run()
        {
            try { Build(); AssetDatabase.SaveAssets(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogError("[CoverUp] Starter island build failed: " + e); EditorApplication.Exit(1); }
        }

        [MenuItem("Cover Up!/Maps/Create Starter Island")]
        public static void Build()
        {
            string path = $"{SceneFolder}/{SceneName}.unity";
            if (System.IO.File.Exists(path))
            {
                Debug.Log($"[CoverUp] {SceneName} already exists, kept as-is (edit it directly, or delete the .unity to reseed).");
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                return;
            }
            System.IO.Directory.CreateDirectory(SceneFolder);
            System.IO.Directory.CreateDirectory(AssetFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Light sun = ArenaStandards.BuildLighting();
            Material sandMat = ArenaStandards.SurfaceMaterial(AssetFolder, SceneName + "_sand", SandCol);
            Material grassMat = ArenaStandards.SurfaceMaterial(AssetFolder, SceneName + "_grass", GrassCol);
            Material woodMat = ArenaStandards.SurfaceMaterial(AssetFolder, SceneName + "_wood", WoodCol);
            Material rockMat = ArenaStandards.SurfaceMaterial(AssetFolder, SceneName + "_rock", RockCol);

            var root = new GameObject(MapContract.Root);
            var cfg = root.AddComponent<MapConfig>();
            SetFloat(cfg, "hiderScale", GameScale.Default);
            SetFloat(cfg, "hunterScale", GameScale.Default);
            var variants = root.AddComponent<MapSizeVariants>();
            var info = root.AddComponent<WorkshopMapInfo>();
            var infoSo = new SerializedObject(info);
            infoSo.FindProperty("mapId").stringValue = SceneName;
            infoSo.FindProperty("title").stringValue = "Starter Island";
            infoSo.FindProperty("description").stringValue =
                "A small island rising from the sea, the way every map does: beach, shelf, bounds in the shallows, " +
                "one open-sided shed. Build on it or replace it.";
            SerializedProperty tags = infoSo.FindProperty("tags");
            tags.arraySize = 2;
            tags.GetArrayElementAtIndex(0).stringValue = "island";
            tags.GetArrayElementAtIndex(1).stringValue = "starter";
            infoSo.ApplyModifiedPropertiesWithoutUndo();

            var baseRoot = Child(root.transform, MapContract.Base);
            var fixtures = Child(baseRoot, MapContract.Fixtures);
            var content = Child(baseRoot, MapContract.Content);
            sun.transform.SetParent(fixtures, true);
            Child(fixtures, "Environment").gameObject.AddComponent<MapEnvironment>();   // the island sky; the sea is at Y 0 regardless

            // Ground: one mesh, the whole island and its seabed out to the mesh edge.
            Mesh ground = BuildGroundMesh();
            ground = SaveMesh(ground, SceneName + "_ground");
            var groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(content, false);
            groundGo.AddComponent<MeshFilter>().sharedMesh = ground;
            groundGo.AddComponent<MeshRenderer>().sharedMaterial = sandMat;
            groundGo.AddComponent<MeshCollider>().sharedMesh = ground;
            groundGo.AddComponent<CamouflageSurface>();
            groundGo.AddComponent<CameraProofSurface>();
            GameObjectUtility.SetStaticEditorFlags(groundGo, StaticEditorFlags.ContributeGI
                | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic);

            // A grass cap on the plateau: a second, smaller disc 2 cm up, so the island is
            // not one colour and the example shows a second surface to blend into.
            Mesh grass = SaveMesh(BuildCapMesh(Waterline * 0.55f, 0.05f), SceneName + "_grass");
            var grassGo = new GameObject("Grass");
            grassGo.transform.SetParent(content, false);
            grassGo.AddComponent<MeshFilter>().sharedMesh = grass;
            grassGo.AddComponent<MeshRenderer>().sharedMaterial = grassMat;
            grassGo.AddComponent<CamouflageSurface>();
            GameObjectUtility.SetStaticEditorFlags(grassGo, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);

            // The shed: a roof on four posts with one back wall, open on three sides.
            var shed = Child(content, "Shed");
            float shedX = 6f, shedZ = -2f;
            float shedY = GridHeight(shedX, shedZ);
            shed.localPosition = new Vector3(shedX, shedY, shedZ);
            for (int i = 0; i < 4; i++)
                Block(shed, "Post_" + i, new Vector3((i & 1) == 0 ? -2.2f : 2.2f, 1.5f, (i & 2) == 0 ? -1.7f : 1.7f),
                    new Vector3(0.3f, 3f, 0.3f), woodMat);
            Block(shed, "Roof", new Vector3(0f, 3.1f, 0f), new Vector3(5.2f, 0.2f, 4.2f), woodMat);
            Block(shed, "BackWall", new Vector3(0f, 1.5f, 1.85f), new Vector3(5.2f, 3f, 0.2f), woodMat);
            Block(shed, "Bench", new Vector3(0f, 0.45f, 1.2f), new Vector3(2.4f, 0.1f, 0.6f), woodMat);

            // A few rocks, so the beach has something to hide beside.
            Rock(content, "Rock_A", new Vector3(-9f, 0f, 7f), new Vector3(1.6f, 1.1f, 1.3f), 20f, rockMat);
            Rock(content, "Rock_B", new Vector3(-12f, 0f, -5f), new Vector3(2.2f, 1.4f, 1.8f), 65f, rockMat);
            Rock(content, "Rock_C", new Vector3(4f, 0f, 11f), new Vector3(1.2f, 0.8f, 1.0f), 110f, rockMat);

            // Spawns on the plateau, a few metres apart.
            Spawn(fixtures, "HiderSpawn", new Vector3(-3f, 0f, 3f), MapSpawnRole.Hiders);
            Spawn(fixtures, "HunterSpawn", new Vector3(-3f, 0f, -4f), MapSpawnRole.Hunters);

            var reference = Child(root.transform, MapContract.Reference);
            reference.gameObject.tag = MapReferenceDoll.EditorOnlyTag;
            Doll(reference, MapDollRole.Hider, new Vector3(0f, GridHeight(0f, 1.2f), 1.2f));
            Doll(reference, MapDollRole.Hunter, new Vector3(0f, GridHeight(0f, -1.2f), -1.2f));

            // Sizes: Tiny fences the dry island, Small stands in knee-deep water.
            var sizesRoot = Child(root.transform, MapContract.Sizes);
            var tiny = Child(sizesRoot, "Tiny");
            Outline(tiny, "Bounds_Island", bearing => CoastRadius(bearing) - 1f);
            var small = Child(sizesRoot, "Small");
            Outline(small, "Bounds_KneeDeep", bearing => CoastRadius(bearing) + ShelfWidth * (KneeDeep / ShelfDepth));
            var so = new SerializedObject(variants);
            so.FindProperty("tiny.root").objectReferenceValue = tiny.gameObject;
            so.FindProperty("small.root").objectReferenceValue = small.gameObject;
            so.FindProperty("soloWalkSize").enumValueIndex = (int)MapSize.Small;
            so.ApplyModifiedPropertiesWithoutUndo();
            tiny.gameObject.SetActive(false);
            small.gameObject.SetActive(true);

            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[CoverUp] Created the starter island → {path}. Open it, Walk Through Open Scene, Validate Map; " +
                      "then build on it. The sea is at Y 0: keep floors above it or draw a MapSeaHole over a dry one.");
        }

        // ------------------------------------------------------------------ the profile
        // Coast noise: a few sines so the waterline is not a circle (the hub's lesson,
        // Docs/IslandMapHowTo.md). Deterministic, no Perlin, so every machine builds the same.
        private static float CoastRadius(float bearing)
            => Waterline + 1.5f * Mathf.Sin(3f * bearing + 0.7f) + 0.9f * Mathf.Sin(7f * bearing + 2.1f);

        /// <summary>Ground height at local (x, z), the sea at 0.</summary>
        private static float Height(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float bearing = Mathf.Atan2(z, x);
            float coast = CoastRadius(bearing);
            if (r < coast)
            {
                // Flat sand at the waterline, rising to the plateau by 40% of the way in.
                float t = (coast - r) / coast;
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.06f) / 0.34f));
                float bump = 0.12f * Mathf.Sin(x * 0.9f + 1f) * Mathf.Sin(z * 0.7f + 2f);
                return 0.05f + Plateau * s + bump * s;
            }
            float outAt = r - coast;
            if (outAt < ShelfWidth) return -outAt * (ShelfDepth / ShelfWidth);
            return Mathf.Max(-6f, -ShelfDepth - (outAt - ShelfWidth) * 0.25f);
        }

        private static Mesh BuildGroundMesh()
        {
            int n = Mathf.RoundToInt(MeshHalf * 2f / Cell) + 1;
            var verts = new Vector3[n * n];
            var uvs = new Vector2[n * n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    float wx = -MeshHalf + x * Cell, wz = -MeshHalf + z * Cell;
                    verts[z * n + x] = new Vector3(wx, Height(wx, wz), wz);
                    uvs[z * n + x] = new Vector2(wx * 0.1f, wz * 0.1f);
                }
            var tris = new int[(n - 1) * (n - 1) * 6];
            int t = 0;
            for (int z = 0; z < n - 1; z++)
                for (int x = 0; x < n - 1; x++)
                {
                    int i = z * n + x;
                    tris[t++] = i; tris[t++] = i + n; tris[t++] = i + n + 1;
                    tris[t++] = i; tris[t++] = i + n + 1; tris[t++] = i + 1;
                }
            var mesh = new Mesh { name = SceneName + "_ground", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        // The ground MESH's height at (x, z): the profile sampled at the grid corners and
        // blended between them, which is what the triangles actually show. Anything laid on
        // the ground follows this, not the profile, or it sinks between two grid vertices.
        private static float GridHeight(float x, float z)
        {
            float gx = (x + MeshHalf) / Cell, gz = (z + MeshHalf) / Cell;
            int ix = Mathf.FloorToInt(gx), iz = Mathf.FloorToInt(gz);
            float fx = gx - ix, fz = gz - iz;
            float X(int i) => -MeshHalf + i * Cell;
            float h00 = Height(X(ix), X(iz)), h10 = Height(X(ix + 1), X(iz));
            float h01 = Height(X(ix), X(iz + 1)), h11 = Height(X(ix + 1), X(iz + 1));
            // The grid splits each cell along the (i, i+n+1) diagonal: match that split.
            return fx + fz <= 1f
                ? h00 + (h10 - h00) * fx + (h01 - h00) * fz
                : h11 + (h01 - h11) * (1f - fx) + (h10 - h11) * (1f - fz);
        }

        // A disc draped on the ground mesh, lifted a little.
        private static Mesh BuildCapMesh(float radius, float lift)
        {
            const int rings = 24, segs = 48;
            var verts = new List<Vector3> { new Vector3(0f, GridHeight(0f, 0f) + lift, 0f) };
            var tris = new List<int>();
            for (int r = 1; r <= rings; r++)
            {
                float rad = radius * r / rings;
                for (int s = 0; s < segs; s++)
                {
                    float a = s / (float)segs * Mathf.PI * 2f;
                    float x = Mathf.Cos(a) * rad, z = Mathf.Sin(a) * rad;
                    verts.Add(new Vector3(x, GridHeight(x, z) + lift, z));
                }
            }
            for (int s = 0; s < segs; s++) { tris.Add(0); tris.Add(1 + (s + 1) % segs); tris.Add(1 + s); }
            for (int r = 0; r < rings - 1; r++)
                for (int s = 0; s < segs; s++)
                {
                    int a = 1 + r * segs + s, b = 1 + r * segs + (s + 1) % segs;
                    int c = 1 + (r + 1) * segs + s, d = 1 + (r + 1) * segs + (s + 1) % segs;
                    tris.Add(a); tris.Add(d); tris.Add(c);
                    tris.Add(a); tris.Add(b); tris.Add(d);
                }
            var mesh = new Mesh { name = SceneName + "_grass" };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{AssetFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // ------------------------------------------------------------------ pieces
        private static void Outline(Transform parent, string name, System.Func<float, float> radiusAt)
        {
            var go = Child(parent, name);
            var poly = go.gameObject.AddComponent<MapBoundsPolygon>();
            var pts = new List<Vector2>(OutlineCorners);
            for (int i = 0; i < OutlineCorners; i++)
            {
                float a = i / (float)OutlineCorners * Mathf.PI * 2f;
                float r = radiusAt(a);
                pts.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
            }
            poly.SetPoints(pts);
            poly.SetHeights(BoundsFloor, BoundsCeiling);
        }

        private static void Spawn(Transform fixtures, string name, Vector3 at, MapSpawnRole role)
        {
            var t = Child(fixtures, name);
            t.localPosition = new Vector3(at.x, GridHeight(at.x, at.z), at.z);
            var disc = t.gameObject.AddComponent<MapSpawnDisc>();
            var so = new SerializedObject(disc);
            so.FindProperty("role").enumValueIndex = (int)role;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Block(Transform parent, string name, Vector3 centre, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.AddComponent<CamouflageSurface>();
            go.AddComponent<CameraProofSurface>();
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI
                | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic);
        }

        private static void Rock(Transform parent, string name, Vector3 at, Vector3 size, float yaw, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(at.x, GridHeight(at.x, at.z) + size.y * 0.35f, at.z);
            go.transform.localRotation = Quaternion.Euler(8f, yaw, -6f);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.AddComponent<CamouflageSurface>();
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void Doll(Transform parent, MapDollRole role, Vector3 at)
        {
            Transform t = Child(parent, $"Reference_{role}");
            t.localPosition = at;
            t.gameObject.tag = MapReferenceDoll.EditorOnlyTag;
            MapReferenceDoll doll = t.gameObject.AddComponent<MapReferenceDoll>();
            var so = new SerializedObject(doll);
            so.FindProperty("role").enumValueIndex = (int)role;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Component c, string prop, float value)
        {
            var so = new SerializedObject(c);
            so.FindProperty(prop).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
