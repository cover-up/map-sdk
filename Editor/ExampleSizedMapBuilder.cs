using CoverUp.Core;
using CoverUp.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoverUp.EditorTools
{
    /// <summary>
    /// Builds a minimal, fully-scaffolded example of a size-variant map — the
    /// reference the Steam Workshop template is cut from. It realizes the
    /// canonical corridor example: three rooms in a row, spawn in room 1, and
    /// doors ADDED per size so smaller sizes jail the player closer to spawn:
    ///
    ///   Small  → door between rooms 1&2  → players kept in room 1.
    ///   Medium → door between rooms 2&3  → players roam rooms 1&2.
    ///   Large  → no doors                → the whole corridor is open.
    ///
    /// The shared structure (floor, outer walls, the two divider walls WITH
    /// doorway gaps, spawn, lighting, MapConfig) lives in Base and never changes;
    /// each size folder only ADDS its keep-in <see cref="MapBoundsVolume"/>(s)
    /// and the door plugs. Scene tree (the _CoverUpMap contract):
    ///
    ///   _CoverUpMap            [MapConfig, MapSizeVariants]
    ///   ├── Base                floor, outer + divider walls, spawn, sun
    ///   └── Sizes
    ///       ├── Small           Bounds(room 1)      + Door 1↔2
    ///       ├── Medium          Bounds(rooms 1–2)   + Door 2↔3
    ///       └── Large           Bounds(rooms 1–3)
    ///
    /// CREATE-ONCE, like ColorBoxMapBuilder: an existing scene on disk is never
    /// regenerated, so edits are safe. Delete the .unity to reseed. The example
    /// is deliberately NOT registered in Build Settings or MapCatalog — it's a
    /// template/reference, played solo via Cover Up!/Maps/Walk Through Open Scene.
    /// Run Cover Up!/Maps/Validate Map on it to see the contract pass.
    /// </summary>
    public static class ExampleSizedMapBuilder
    {
        // Where the example lands. The GAME keeps its copy under Assets/CoverUp/Content,
        // but a mapper's project has no such folder and has no business growing one — the
        // SDK shipping the game's internal layout into every third-party project is a leak,
        // not a convention. So: use the game's folders when they already exist (the game
        // repo behaves exactly as before, and its existing example is still found), and a
        // neutral Assets/Maps everywhere else.
        private const string GameSceneFolder = "Assets/CoverUp/Content/Scenes";
        private const string GameMaterialFolder = "Assets/CoverUp/Content/Materials";
        private const string MapSceneFolder = "Assets/Maps/Scenes";
        private const string MapMaterialFolder = "Assets/Maps/Materials";
        private const string SceneName = "example_sized_map";

        private static bool InGameProject => System.IO.Directory.Exists(GameSceneFolder);
        private static string SceneFolder => InGameProject ? GameSceneFolder : MapSceneFolder;
        private static string MaterialFolder => InGameProject ? GameMaterialFolder : MapMaterialFolder;

        // Corridor geometry (metres). Three RoomLen×RoomWide rooms along +X.
        private const float RoomLen = 16f;   // each room's length along X
        private const float RoomWide = 12f;  // corridor width along Z
        private const float WallH = 5f;
        private const float WallT = 0.4f;
        private const float DoorGap = 4f;    // doorway opening width along Z
        private const float BoundsInset = 0.2f; // keep the volume flush inside the walls
        private const float BoundsY = 5.5f, BoundsH = 13f; // seals the airspace above the walls

        private static readonly Color FloorCol = new Color(0.62f, 0.60f, 0.58f);
        private static readonly Color WallCol = new Color(0.70f, 0.72f, 0.75f);
        private static readonly Color DoorCol = new Color(0.80f, 0.45f, 0.30f); // stands out: "this is the door"

        // Headless entry: -executeMethod CoverUp.EditorTools.ExampleSizedMapBuilder.Run
        public static void Run()
        {
            try { Build(); AssetDatabase.SaveAssets(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogError("[CoverUp] Example sized map build failed: " + e); EditorApplication.Exit(1); }
        }

        [MenuItem("Cover Up!/Maps/Create Example Sized Map")]
        public static void Build()
        {
            string path = $"{SceneFolder}/{SceneName}.unity";
            if (System.IO.File.Exists(path))
            {
                Debug.Log($"[CoverUp] {SceneName} already exists — kept as-is (edit it directly, or delete the .unity to reseed).");
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                return;
            }
            System.IO.Directory.CreateDirectory(SceneFolder);
            System.IO.Directory.CreateDirectory(MaterialFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ArenaStandards.BuildLighting();

            Material floorMat = ArenaStandards.SurfaceMaterial(MaterialFolder, SceneName + "_floor", FloorCol);
            Material wallMat = ArenaStandards.SurfaceMaterial(MaterialFolder, SceneName + "_wall", WallCol);
            Material doorMat = ArenaStandards.SurfaceMaterial(MaterialFolder, SceneName + "_door", DoorCol);

            // Room centres along X (rooms 1,2,3), corridor centred on the origin.
            float x1 = -RoomLen, x2 = 0f, x3 = RoomLen;    // room centres
            float d12 = -RoomLen / 2f, d23 = RoomLen / 2f; // divider X (between 1&2, 2&3)
            float halfX = RoomLen * 1.5f;                  // corridor half-length
            float halfZ = RoomWide / 2f;

            // ---- the contract root ------------------------------------------------
            var root = new GameObject("_CoverUpMap");
            var cfg = root.AddComponent<MapConfig>();
            SetFloat(cfg, "playerScale", 1.185f); // ≈ 1.6 m dolls, matching the room scale
            var variants = root.AddComponent<MapSizeVariants>();

            // Workshop authoring metadata (read by Export Workshop Map into map.json).
            var info = root.AddComponent<WorkshopMapInfo>();
            var infoSo = new SerializedObject(info);
            infoSo.FindProperty("mapId").stringValue = "example_sized_map";
            infoSo.FindProperty("title").stringValue = "Example — Sized Corridor";
            infoSo.FindProperty("description").stringValue =
                "Three rooms, spawn in room 1; doors added per size (Small jails room 1, Medium opens 1-2, Large opens all three).";
            SerializedProperty tags = infoSo.FindProperty("tags");
            tags.arraySize = 2;
            tags.GetArrayElementAtIndex(0).stringValue = "indoor";
            tags.GetArrayElementAtIndex(1).stringValue = "example";
            infoSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Base: everything shared across sizes -----------------------------
            var baseRoot = Child(root.transform, "Base");
            Surface(baseRoot, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(RoomLen * 3f, 0.1f, RoomWide), floorMat, true);

            // Outer perimeter.
            Surface(baseRoot, "Wall_W", new Vector3(-halfX, WallH / 2f, 0f), new Vector3(WallT, WallH, RoomWide), wallMat, true);
            Surface(baseRoot, "Wall_E", new Vector3(halfX, WallH / 2f, 0f), new Vector3(WallT, WallH, RoomWide), wallMat, true);
            Surface(baseRoot, "Wall_N", new Vector3(0f, WallH / 2f, halfZ), new Vector3(RoomLen * 3f, WallH, WallT), wallMat, true);
            Surface(baseRoot, "Wall_S", new Vector3(0f, WallH / 2f, -halfZ), new Vector3(RoomLen * 3f, WallH, WallT), wallMat, true);

            // Divider walls: two segments each, leaving a central doorway gap the
            // per-size doors plug. Segment length = (RoomWide - DoorGap) / 2.
            DividerWithGap(baseRoot, "Divider_1", d12, wallMat);
            DividerWithGap(baseRoot, "Divider_2", d23, wallMat);

            // Spawn sits in room 1 — Small always contains it, so it stays in Base.
            var spawn = Child(baseRoot, "MapSpawnDisc");
            spawn.localPosition = new Vector3(x1, 0f, 0f);
            spawn.gameObject.AddComponent<MapSpawnDisc>();

            // ---- Sizes: each only ADDS bounds + door plugs ------------------------
            var sizesRoot = Child(root.transform, "Sizes");

            // Small: jailed in room 1 (west wall .. divider 1). Door plugs 1↔2.
            var small = Child(sizesRoot, "Small");
            Bounds(small, "Bounds_Room1", CentreX(-halfX, d12), SpanX(-halfX, d12));
            DoorPlug(small, "Door_1to2", d12, doorMat);

            // Medium: rooms 1–2 (west wall .. divider 2). Door plugs 2↔3;
            // 1↔2 is open (no door here), so the two rooms join.
            var medium = Child(sizesRoot, "Medium");
            Bounds(medium, "Bounds_Room1_2", CentreX(-halfX, d23), SpanX(-halfX, d23));
            DoorPlug(medium, "Door_2to3", d23, doorMat);

            // Large: the whole corridor, no doors.
            var large = Child(sizesRoot, "Large");
            Bounds(large, "Bounds_Room1_3", CentreX(-halfX, halfX), SpanX(-halfX, halfX));

            // Wire the variant roots + the solo/editor-play default.
            var so = new SerializedObject(variants);
            so.FindProperty("small").objectReferenceValue = small.gameObject;
            so.FindProperty("medium").objectReferenceValue = medium.gameObject;
            so.FindProperty("large").objectReferenceValue = large.gameObject;
            so.FindProperty("soloWalkSize").enumValueIndex = (int)MapSize.Medium;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Author-time: show Medium (Small/Large start inactive). Runtime picks
            // one anyway — MapLoader.ApplySize in MP, MapSizeVariants.Awake solo.
            small.gameObject.SetActive(false);
            medium.gameObject.SetActive(true);
            large.gameObject.SetActive(false);

            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[CoverUp] Created example sized map → {path}. " +
                      "Open it and use Cover Up!/Maps/Preview Size + Walk Through Open Scene; Validate Map to check the contract.");
        }

        // Two wall segments straddling a central DoorGap opening at divider X.
        private static void DividerWithGap(Transform parent, string name, float x, Material mat)
        {
            var group = Child(parent, name);
            float segLen = (RoomWide - DoorGap) / 2f;       // length of each segment along Z
            float segZ = (DoorGap + segLen) / 2f;           // segment centre offset from Z=0
            Surface(group, name + "_A", new Vector3(x, WallH / 2f, segZ), new Vector3(WallT, WallH, segLen), mat, true);
            Surface(group, name + "_B", new Vector3(x, WallH / 2f, -segZ), new Vector3(WallT, WallH, segLen), mat, true);
        }

        // The removable door: a slab filling the divider's gap. NOT GI-static —
        // it toggles with its size root, and static-flagged toggling warns.
        private static void DoorPlug(Transform parent, string name, float x, Material mat)
        {
            Surface(parent, name, new Vector3(x, WallH / 2f, 0f), new Vector3(WallT, WallH, DoorGap), mat, false);
        }

        // A keep-in volume spanning [x0..x1] over the full corridor width, inset
        // to sit flush inside the walls and tall enough to seal the airspace.
        private static void Bounds(Transform parent, string name, float centreX, float spanX)
        {
            var go = Child(parent, name);
            go.localPosition = new Vector3(centreX, BoundsY, 0f);
            go.localScale = new Vector3(spanX - BoundsInset, BoundsH, RoomWide - BoundsInset);
            go.gameObject.AddComponent<MapBoundsVolume>();
        }

        private static float CentreX(float x0, float x1) => (x0 + x1) / 2f;
        private static float SpanX(float x0, float x1) => Mathf.Abs(x1 - x0);

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void Surface(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool giStatic)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.AddComponent<CamouflageSurface>(); // hiders can blend to any surface
            if (giStatic)
            {
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI
                    | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic);
            }
        }

        private static void SetFloat(Component c, string prop, float value)
        {
            var so = new SerializedObject(c);
            so.FindProperty(prop).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
