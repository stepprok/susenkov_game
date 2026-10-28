using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class MapLoader : MonoBehaviour
{
    public string resourceName = "map";
    public Material roadMaterial;

    [Header("Missing prefabs")]
    [Tooltip("If a prefab from the map is not found in Resources/MapPrefabs, build a placeholder from Unity primitives (same shapes/colors as the editor) instead of skipping it.")]
    public bool generateMissingPrefabs = true;
    [Tooltip("Optional material for the generated placeholders. Empty = the render pipeline's default material, tinted per part.")]
    public Material fallbackMaterial;

    [Header("Ground tiles (from the editor's 'New tile' tool)")]
    [Tooltip("Creates one ground mesh (with the sculpted terrain heights + MeshCollider) per tile. Leave off if you already have your own ground/terrain.")]
    public bool generateGround = false;
    public Material groundMaterial;
    public LayerMask groundMask = ~0;

    [Header("Your own ground (instead of generated tiles)")]
    [Tooltip("Your ground object (scene object or prefab). Its size is read automatically and it is duplicated once per tile in the map. Takes priority over 'Generate Ground'.")]
    public GameObject groundTemplate;
    [Tooltip("Scale each copy so it covers exactly one tile (tileSize x tileSize). Off = keep your ground's own size.")]
    public bool fitGroundToTileSize = true;
    [Tooltip("Add a MeshCollider to ground parts that have none, so props can be placed on top of the ground.")]
    public bool addGroundColliders = true;

    [Header("NavMesh (baked at runtime from the ground) + NPCs")]
    [Tooltip("Bake a NavMesh over all tiles after the ground and objects are built. Works with generated ground, your own ground, or any ground with colliders under the map.")]
    public bool buildNavMesh = true;
    public float agentRadius = 0.5f;
    public float agentHeight = 2f;
    public float agentMaxSlope = 45f;
    [Tooltip("PhysicsColliders = uses colliders (recommended, works without Read/Write). RenderMeshes = uses visible meshes (needs Read/Write on the meshes).")]
    public NavMeshCollectGeometry navGeometry = NavMeshCollectGeometry.PhysicsColliders;
    [Tooltip("Placed map objects (buildings, props...) become not-walkable so NPCs walk around them. They need colliders when using PhysicsColliders.")]
    public bool objectsBlockNavMesh = true;
    [Tooltip("NPC prefab to spawn on the NavMesh. A NavMeshAgent is added automatically if it has none.")]
    public GameObject npcPrefab;
    public int npcCount = 10;

    [Header("Save the map into the scene (Edit mode)")]
    [Tooltip("Folder under Assets/ where generated meshes, materials and the NavMesh are saved when building into the scene.")]
    public string bakedAssetsFolder = "GeneratedMap";
    [Tooltip("Name of the scene copy created by 'Build + save as new scene' (saved in Assets/Scenes).")]
    public string bakedSceneName = "MapBaked";
    [Tooltip("When you press Play, first build the map into the scene and save a scene copy (only if it is not built yet).")]
    public bool bakeSceneOnPlay = false;
    [SerializeField, HideInInspector] bool built;
    public bool IsBuilt => built;

    MapData LoadData()
    {
        var data = JsonUtility.FromJson<MapData>(Resources.Load<TextAsset>(resourceName).text);
        if (data.objects == null) data.objects = new MapObj[0];
        if (data.splines == null) data.splines = new MapSpline[0];
        return data;
    }

    IEnumerator Start()
    {
        var data = LoadData();

        // The map was already built into this scene in Edit mode (see "Build map into scene"): don't build it again.
        if (built)
        {
            if (npcPrefab != null && npcCount > 0) yield return SpawnNpcs(data);
            yield break;
        }

        var placed = Generate(data);
        if (buildNavMesh) yield return BuildNavMesh(data, placed);
    }

    // Builds ground, objects and splines (works at runtime and in Edit mode). Returns the placed objects.
    List<GameObject> Generate(MapData data)
    {

        // 1) ground tiles
        if (groundTemplate != null)
        {
            BuildOwnGround(data.tiles, data.tileSize);
            Physics.SyncTransforms();
        }
        else if (generateGround && data.tiles != null)
        {
            foreach (var t in data.tiles) BuildTile(t, data.tileSize);
            Physics.SyncTransforms();
        }

        // 2) sample ground heights BEFORE spawning anything, so props don't land on other props
        var objY = new float[data.objects.Length];
        for (int i = 0; i < objY.Length; i++) objY[i] = GroundY(data.objects[i].x, data.objects[i].z);

        // 3) objects
        var placed = new List<GameObject>();
        for (int i = 0; i < data.objects.Length; i++)
        {
            var o = data.objects[i];
            var prefab = Resources.Load<GameObject>("MapPrefabs/" + o.prefab);
            GameObject go;
            if (prefab != null) go = SpawnPrefab(prefab);
            else
            {
                go = generateMissingPrefabs ? BuildFallback(o.prefab) : null;
                if (go == null) { Debug.LogWarning("Missing prefab: " + o.prefab); continue; }
                if (warned.Add(o.prefab)) Debug.LogWarning("Prefab '" + o.prefab + "' not found in Resources/MapPrefabs - generated a placeholder instead.");
            }
            if (Mathf.Abs(o.bend) > 0.01f) ApplyBend(go, o.bend, o.bendDir, o.bendAxis == "length");

            go.transform.SetPositionAndRotation(
                new Vector3(o.x, objY[i] + o.y, o.z),
                Quaternion.Euler(o.pitch, o.yaw, o.roll));
            go.transform.localScale *= o.scale;
            placed.Add(go);
        }

        // 4) splines (roads / rivers)
        foreach (var s in data.splines)
        {
            if (s.kind != "road" && s.kind != "river") continue;
            var go = new GameObject(s.kind);
            go.transform.SetParent(transform);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.TransformZ;
            lr.transform.rotation = Quaternion.Euler(90, 0, 0);
            lr.widthMultiplier = s.width;
            lr.loop = s.closed;
            lr.numCornerVertices = 4;
            if (roadMaterial != null) lr.sharedMaterial = roadMaterial;
            lr.positionCount = s.points.Length;
            for (int i = 0; i < s.points.Length; i++)
                lr.SetPosition(i, new Vector3(s.points[i].x, GroundY(s.points[i].x, s.points[i].z) + 0.05f, s.points[i].z));
        }

        return placed;
    }

    GameObject SpawnPrefab(GameObject prefab)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) // Edit mode: keep the link to the prefab asset so it stays editable
        {
            var g = UnityEditor.PrefabUtility.InstantiatePrefab(prefab, transform) as GameObject;
            g.transform.SetPositionAndRotation(transform.position, Quaternion.identity);
            return g;
        }
#endif
        return Instantiate(prefab, transform.position, Quaternion.identity, transform);
    }

    // Bakes a NavMesh over every tile (no AI Navigation package needed), then spawns NPCs on it.
    IEnumerator BuildNavMesh(MapData data, List<GameObject> placed)
    {
        var tiles = data.tiles != null && data.tiles.Length > 0 ? data.tiles : new[] { new MapTile() };
        float half = data.tileSize / 2f, minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var t in tiles)
        {
            minX = Mathf.Min(minX, t.x - half); maxX = Mathf.Max(maxX, t.x + half);
            minZ = Mathf.Min(minZ, t.z - half); maxZ = Mathf.Max(maxZ, t.z + half);
        }
        var bounds = new Bounds(new Vector3((minX + maxX) / 2f, 0f, (minZ + maxZ) / 2f), new Vector3(maxX - minX, 1000f, maxZ - minZ));

        var markups = new List<NavMeshBuildMarkup>();
        if (objectsBlockNavMesh)
            foreach (var go in placed)
                if (go != null) markups.Add(new NavMeshBuildMarkup { root = go.transform, overrideArea = true, area = 1, ignoreFromBuild = false }); // area 1 = Not Walkable

        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(bounds, ~0, navGeometry, 0, markups, sources);
        if (sources.Count == 0) { Debug.LogWarning("NavMesh: no geometry found. Does the ground have a collider (or Read/Write meshes when using RenderMeshes)?"); yield break; }

        var settings = NavMesh.GetSettingsByID(0);
        settings.agentRadius = agentRadius; settings.agentHeight = agentHeight; settings.agentSlope = agentMaxSlope;

        var navData = new NavMeshData(settings.agentTypeID) { position = bounds.center };
        NavMesh.AddNavMeshData(navData);
        var op = NavMeshBuilder.UpdateNavMeshDataAsync(navData, settings, sources, bounds);
        while (!op.isDone) yield return null;

        yield return SpawnNpcs(data);
    }

    IEnumerator SpawnNpcs(MapData data)
    {
        var tiles = data.tiles != null && data.tiles.Length > 0 ? data.tiles : new[] { new MapTile() };
        float half = data.tileSize / 2f;
        if (npcPrefab == null || npcCount <= 0) yield break;
        int spawned = 0, tries = 0;
        while (spawned < npcCount && tries < npcCount * 40)
        {
            tries++;
            var t = tiles[Random.Range(0, tiles.Length)];
            float x = t.x + Random.Range(-half, half) * 0.95f, z = t.z + Random.Range(-half, half) * 0.95f;
            if (!NavMesh.SamplePosition(new Vector3(x, GroundY(x, z), z), out var hit, 3f, NavMesh.AllAreas)) continue;
            var npc = Instantiate(npcPrefab, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            var agent = npc.GetComponent<NavMeshAgent>();
            if (agent == null) agent = npc.AddComponent<NavMeshAgent>();
            agent.Warp(hit.position);
            spawned++;
            if (tries % 50 == 0) yield return null;
        }
        Debug.Log("NavMesh built. Spawned " + spawned + "/" + npcCount + " NPCs.");
    }

    // ---------- placeholders for prefabs missing from Resources/MapPrefabs ----------
    // Mirrors the editor's shapes. Editor space is prefab space with X mirrored, so Part() flips X.
    readonly HashSet<string> warned = new HashSet<string>();
    readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

    struct Spec
    {
        public string shape, col; public float a, b, c;
        public Spec(string shape, string col, float a, float b, float c) { this.shape = shape; this.col = col; this.a = a; this.b = b; this.c = c; }
    }

    static readonly Dictionary<string, Spec> Fallbacks = MakeFallbacks();

    static Dictionary<string, Spec> MakeFallbacks()
    {
        var d = new Dictionary<string, Spec>(System.StringComparer.OrdinalIgnoreCase);
        void F(string n, string s, string col, float a = 1f, float b = 1f, float c = 1f) { d[n] = new Spec(s, col, a, b, c); }

        F("Stall", "stop", "#d96a2b", 3f, 2.6f, 2f); F("Machine", "box", "#3b4a7a", 2f, 2.6f, 2f);
        F("Crate", "box", "#a47b45", 1f, 1f, 1f); F("Fence", "box", "#9c6b3b", 1.6f, 1.1f, .15f);
        foreach (var i in new[] { "001", "003", "009", "013", "019", "020", "022" }) F("road_" + i, "tile", "#3a3d42", 10f, .1f, 10f);
        for (int i = 1; i <= 5; i++) F("Set_B_Tile_0" + i, "tile", "#a7abb0", 6f, .2f, 6f);
        F("Eco_Building_Tower", "tower", "#3f6fa8", 10f, 36f); F("Eco_Building_Grid", "tower", "#c77a3a", 9f, 28f);
        F("Eco_Building_Slope", "terrace", "#7a8f6a", 16f, 14f); F("Eco_Building_Terrace", "terrace", "#6f9a62", 14f, 12f);
        F("Eco_Building_Block", "tower", "#d98a3a", 8f, 22f); F("Regular_Building_Twist", "twist", "#2f4f8f", 9f, 40f);
        F("Regular_Building_Tall", "tower", "#c98a3a", 8f, 40f);
        F("Bush_06", "pot", "#3f8a46"); F("Bush_07", "pot", "#4a9a4a"); F("Bush_10", "box", "#4f8a2f", 3f, 1.5f, 1.5f); F("Palm_03", "palm", "#7a5a36");
        F("Car_06", "car", "#5a7388", 4.2f, 1.2f, 1.9f); F("Car_13", "car", "#c2552b", 4.2f, 1.1f, 1.9f);
        F("Car_16", "car", "#b8b32c", 4f, 1.1f, 1.9f); F("Car_19", "car", "#d7683a", 3.6f, 1.2f, 1.7f);
        F("Futuristic_Car", "car", "#9fb8c8", 4.6f, 1.2f, 2f); F("Van", "car", "#7d8790", 5f, 2.2f, 2.1f);
        F("Bus_Stop", "stop", "#8fb0c0", 4f, 2.6f, 1.5f); F("Fountain", "fountain", "#c98a5a", 3f); F("Graffiti_03", "graf", "#8a8f95", 3f, 2.5f);
        F("Spotlight_01", "pole", "#444444", 7f); F("Spotlight_02", "pole", "#e5b93a", 6f);
        F("traffic_light_01", "tl", "#333333", 5.5f); F("traffic_light_02", "tl", "#333333", 5.5f); F("traffic_light_03", "tl", "#333333", 5.5f);
        F("Trash_02", "bin", "#d9a21f"); F("Trash_03", "bin", "#c4342d"); F("Trash_04", "bin", "#2f64c4"); F("Trash_05", "bin", "#3f9a3f");
        F("Trash_06", "box", "#6a6f78", 1.8f, 1.2f, .4f); F("Trash_Can_01", "box", "#3f7a4a", 1.2f, 1f, .8f); F("Trash_Can_02", "box", "#c0642b", 1.2f, 1f, .8f);
        F("Trash_Can_03", "bin", "#555555"); F("Trash_Can_04", "bin", "#6a6a6a");
        F("Billboard_01", "bill", "#3f7fd1", 5f, 3f); F("Billboard_02", "bill", "#4fa05a", 5f, 3f); F("Billboard_03", "bill", "#59b0b0", 7f, 2f);
        F("Billboard_04", "bill", "#e8e8f0", 6f, 1.6f); F("Billboard_05", "bill", "#eeeeee", 6f, 2.2f); F("Billboard_06", "bill", "#e040c8", 6f, 1.4f);
        F("Signboard_01", "bill", "#4a3a7a", 1.5f, 3.5f);
        return d;
    }

    GameObject BuildFallback(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (!Fallbacks.TryGetValue(name, out var s))
        {
            // unknown name: guess from keywords (e.g. "Street_Lamp_02" -> lamp)
            string l = name.ToLowerInvariant();
            if (l.Contains("lamp") || l.Contains("spotlight") || l.Contains("streetlight")) s = Fallbacks["Spotlight_01"];
            else if (l.Contains("palm") || l.Contains("tree")) s = Fallbacks["Palm_03"];
            else if (l.Contains("bush")) s = Fallbacks["Bush_06"];
            else if (l.Contains("car")) s = Fallbacks["Car_06"];
            else if (l.Contains("traffic")) s = Fallbacks["traffic_light_01"];
            else if (l.Contains("billboard") || l.Contains("signboard")) s = Fallbacks["Billboard_01"];
            else if (l.Contains("trash")) s = Fallbacks["Trash_02"];
            else return null;
        }

        var root = new GameObject(name + " (placeholder)");
        root.transform.SetParent(transform, false);
        var t = root.transform;
        float a = s.a, b = s.b, c = s.c; string col = s.col;

        switch (s.shape)
        {
            case "box": Box(t, new Vector3(0, b / 2, 0), new Vector3(a, b, c), col); break;
            case "tile":
                Box(t, new Vector3(0, b / 2, 0), new Vector3(a, b, c), col);
                if (name.StartsWith("road_", System.StringComparison.OrdinalIgnoreCase))
                    Box(t, new Vector3(0, b + .01f, 0), new Vector3(.3f, .02f, c * .8f), "#e8e0b0");
                break;
            case "tower":
                Box(t, new Vector3(0, b / 2, 0), new Vector3(a, b, a), col);
                for (int i = 1; i < 6; i++) Box(t, new Vector3(0, b * i / 6f, 0), new Vector3(a + .1f, b * .04f, a + .1f), "#ffd27a");
                break;
            case "terrace":
                for (int i = 0; i < 4; i++)
                {
                    float h = b / 4f, sz = a * (1f - i * .2f);
                    Box(t, new Vector3(i * a * .05f, i * h + h / 2f, 0), new Vector3(sz, h, sz), col);
                }
                break;
            case "twist":
                for (int i = 0; i < 8; i++)
                {
                    float h = b / 8f, sz = a * (1f - i * .04f);
                    Box(t, new Vector3(0, i * h + h / 2f, 0), new Vector3(sz, h, sz), i % 2 == 1 ? "#ff9a3c" : col,
                        Quaternion.AngleAxis(-i * .2f * Mathf.Rad2Deg, Vector3.up));
                }
                break;
            case "pot":
                Cyl(t, .6f, .6f, new Vector3(0, .3f, 0), "#9aa0a6");
                Sph(t, .85f, new Vector3(0, 1.1f, 0), col);
                break;
            case "palm":
                Cyl(t, .16f, 6f, new Vector3(0, 3f, 0), col);
                for (int i = 0; i < 6; i++)
                    Box(t, new Vector3(Mathf.Sin(i * 1.047f) * 1.3f, 5.9f, Mathf.Cos(i * 1.047f) * 1.3f), new Vector3(3f, .08f, .6f), "#3f8a46",
                        Quaternion.AngleAxis(23f, Vector3.right) * Quaternion.AngleAxis(-i * 60f, Vector3.up));
                break;
            case "car":
                Box(t, new Vector3(0, b * .45f, 0), new Vector3(a, b * .55f, c), col);
                Box(t, new Vector3(-a * .05f, b * .95f, 0), new Vector3(a * .5f, b * .5f, c * .85f), "#cfe6f5");
                foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                        Cyl(t, .35f, .3f, new Vector3(x * a * .32f, .35f, z * c * .5f), "#111111", Quaternion.AngleAxis(90f, Vector3.right));
                break;
            case "bill":
                foreach (int x in new[] { -1, 1 }) Cyl(t, .12f, b + 1.5f, new Vector3(x * a * .35f, (b + 1.5f) / 2f, 0), "#444444");
                Box(t, new Vector3(0, 1.5f + b / 2f, 0), new Vector3(a, b, .15f), col);
                break;
            case "pole": // street lamp / spotlight
                Cyl(t, .1f, a, new Vector3(0, a / 2f, 0), "#555555");
                Box(t, new Vector3(.4f, a, 0), new Vector3(.9f, .25f, .3f), col);
                break;
            case "tl":
                Cyl(t, .1f, a, new Vector3(0, a / 2f, 0), "#333333");
                Box(t, new Vector3(0, a - .4f, 0), new Vector3(.4f, 1.1f, .3f), "#222222");
                var lights = new[] { "#ee3333", "#eeee33", "#33cc55" };
                for (int i = 0; i < 3; i++) Sph(t, .12f, new Vector3(0, a - .1f - i * .3f, .16f), lights[i]);
                break;
            case "bin":
                Cyl(t, .4f, .9f, new Vector3(0, .45f, 0), col);
                Cyl(t, .43f, .1f, new Vector3(0, .95f, 0), "#222222");
                break;
            case "fountain":
                Cyl(t, a, .5f, new Vector3(0, .25f, 0), col);
                Cyl(t, a * .8f, .15f, new Vector3(0, .55f, 0), "#4aa3d8");
                Cyl(t, .2f, 1.4f, new Vector3(0, 1.2f, 0), col);
                Sph(t, .4f, new Vector3(0, 2f, 0), "#4aa3d8");
                break;
            case "stop":
                foreach (int x in new[] { -1, 1 }) Cyl(t, .08f, b, new Vector3(x * a * .45f, b / 2f, 0), "#444444");
                Box(t, new Vector3(0, b, 0), new Vector3(a, .15f, c), col);
                Box(t, new Vector3(0, b * .45f, -c * .4f), new Vector3(a * .9f, b * .6f, .05f), "#bcd8e6");
                break;
            case "graf": Box(t, new Vector3(0, b / 2f + .2f, 0), new Vector3(a, b, .1f), col); break;
        }
        return root;
    }

    void Box(Transform p, Vector3 pos, Vector3 size, string hex, Quaternion? rot = null) => Part(p, PrimitiveType.Cube, pos, size, hex, rot);
    void Sph(Transform p, float r, Vector3 pos, string hex) => Part(p, PrimitiveType.Sphere, pos, Vector3.one * (2f * r), hex, null);
    // Unity's primitive cylinder is 2 m tall with 1 m diameter
    void Cyl(Transform p, float r, float h, Vector3 pos, string hex, Quaternion? rot = null) => Part(p, PrimitiveType.Cylinder, pos, new Vector3(2f * r, h / 2f, 2f * r), hex, rot);

    void Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, string hex, Quaternion? rot)
    {
        var g = GameObject.CreatePrimitive(type);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = new Vector3(-pos.x, pos.y, pos.z); // editor space = prefab space with X mirrored
        g.transform.localRotation = rot ?? Quaternion.identity;
        g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>();
        if (!matCache.TryGetValue(hex, out var m))
        {
            m = new Material(fallbackMaterial != null ? fallbackMaterial : r.sharedMaterial);
            if (ColorUtility.TryParseHtmlString(hex, out var color)) m.color = color;
            matCache[hex] = m;
        }
        r.sharedMaterial = m;
    }

    // Reads the size of groundTemplate and duplicates it for every tile of the map.
    // The ground is assumed to be unrotated and lying flat; its Y position/height is kept.
    void BuildOwnGround(MapTile[] tiles, float tileSize)
    {
        if (tiles == null || tiles.Length == 0) tiles = new[] { new MapTile() };

        // scene object -> reuse it for the first tile; prefab asset -> instantiate
        bool isSceneObject = groundTemplate.scene.IsValid();
        var first = isSceneObject ? groundTemplate : Instantiate(groundTemplate, transform);

        var renderers = first.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { Debug.LogWarning("Ground template has no Renderer - cannot read its size."); return; }

        // measure the original (before any change)
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        Vector3 pivotOffset = first.transform.position - b.center;   // pivot relative to the visual centre
        Vector3 baseScale = first.transform.localScale;
        float baseY = first.transform.position.y;
        float sx = fitGroundToTileSize && b.size.x > 0.001f ? tileSize / b.size.x : 1f;
        float sz = fitGroundToTileSize && b.size.z > 0.001f ? tileSize / b.size.z : 1f;

        // make all copies first, then place them
        var copies = new List<GameObject> { first };
        for (int i = 1; i < tiles.Length; i++) copies.Add(Instantiate(first, transform));

        for (int i = 0; i < tiles.Length; i++)
        {
            var g = copies[i];
            g.name = "Ground_" + tiles[i].ix + "_" + tiles[i].iz;
            g.transform.localScale = new Vector3(baseScale.x * sx, baseScale.y, baseScale.z * sz);
            g.transform.position = new Vector3(tiles[i].x + pivotOffset.x * sx, baseY, tiles[i].z + pivotOffset.z * sz);

            if (addGroundColliders)
                foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                        mf.gameObject.AddComponent<MeshCollider>();
        }
    }

    // Destroys everything under this object (except your ground template). Keep MapLoader on its own empty GameObject.
    [ContextMenu("Clear built map")]
    public void ClearBuilt()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i).gameObject;
            if (groundTemplate != null && c == groundTemplate) continue;
            DestroyImmediate(c);
        }
        built = false;
    }

#if UNITY_EDITOR
    // Builds the whole map as normal scene objects so you can edit them in Unity, and saves the generated
    // meshes / materials / NavMesh as assets so the scene keeps working after a reload.
    [ContextMenu("Build map into scene (editable)")]
    public void BuildInEditor()
    {
        if (Application.isPlaying) { Debug.LogWarning("Build the map in Edit mode (not Play mode)."); return; }
        ClearBuilt();
        var data = LoadData();
        var placed = Generate(data);
        Physics.SyncTransforms();

        string dir = "Assets/" + bakedAssetsFolder;
        if (!UnityEditor.AssetDatabase.IsValidFolder(dir)) UnityEditor.AssetDatabase.CreateFolder("Assets", bakedAssetsFolder);
        SaveGeneratedAssets(dir);
        if (buildNavMesh) BakeNavMeshAsset(data, placed, dir);
        UnityEditor.AssetDatabase.SaveAssets();

        built = true;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("Map built into the scene (" + placed.Count + " objects). Edit them freely, then save the scene.");
    }

    // Meshes and materials created in code only exist in memory - save them as assets.
    void SaveGeneratedAssets(string dir)
    {
        int n = 0;
        foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
        {
            var m = mf.sharedMesh;
            if (m == null || !string.IsNullOrEmpty(UnityEditor.AssetDatabase.GetAssetPath(m))) continue;
            m.name = (string.IsNullOrEmpty(m.name) ? "Mesh" : m.name.Replace(" Instance", "")) + "_" + (n++);
            UnityEditor.AssetDatabase.CreateAsset(m, UnityEditor.AssetDatabase.GenerateUniqueAssetPath(dir + "/" + m.name + ".asset"));
        }
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null || !string.IsNullOrEmpty(UnityEditor.AssetDatabase.GetAssetPath(mat))) continue;
                mat.name = "Mat_" + (n++);
                UnityEditor.AssetDatabase.CreateAsset(mat, UnityEditor.AssetDatabase.GenerateUniqueAssetPath(dir + "/" + mat.name + ".mat"));
            }
        }
    }

    // Same bake as BuildNavMesh, but synchronous and saved as an asset (MapNavMeshData adds it to the scene).
    void BakeNavMeshAsset(MapData data, List<GameObject> placed, string dir)
    {
        var tiles = data.tiles != null && data.tiles.Length > 0 ? data.tiles : new[] { new MapTile() };
        float half = data.tileSize / 2f, minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var t in tiles)
        {
            minX = Mathf.Min(minX, t.x - half); maxX = Mathf.Max(maxX, t.x + half);
            minZ = Mathf.Min(minZ, t.z - half); maxZ = Mathf.Max(maxZ, t.z + half);
        }
        var bounds = new Bounds(new Vector3((minX + maxX) / 2f, 0f, (minZ + maxZ) / 2f), new Vector3(maxX - minX, 1000f, maxZ - minZ));

        var markups = new List<NavMeshBuildMarkup>();
        if (objectsBlockNavMesh)
            foreach (var go in placed)
                if (go != null) markups.Add(new NavMeshBuildMarkup { root = go.transform, overrideArea = true, area = 1, ignoreFromBuild = false });

        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(bounds, ~0, navGeometry, 0, markups, sources);
        if (sources.Count == 0) { Debug.LogWarning("NavMesh: no geometry found. Does the ground have a collider?"); return; }

        var settings = NavMesh.GetSettingsByID(0);
        settings.agentRadius = agentRadius; settings.agentHeight = agentHeight; settings.agentSlope = agentMaxSlope;
        var navData = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        if (navData == null) { Debug.LogWarning("NavMesh bake failed."); return; }

        UnityEditor.AssetDatabase.CreateAsset(navData, UnityEditor.AssetDatabase.GenerateUniqueAssetPath(dir + "/MapNavMesh.asset"));
        var holder = new GameObject("NavMesh");
        holder.transform.SetParent(transform, false);
        holder.AddComponent<MapNavMeshData>().data = navData;
    }
#endif

    // One mesh per tile. heights[] is res x res, row 0 = lowest z, column 0 = lowest x (same as the editor export).
    void BuildTile(MapTile t, float size)
    {
        bool has = t.heights != null && t.res > 1 && t.heights.Length == t.res * t.res;
        int res = has ? t.res : 2;
        float cell = size / (res - 1);
        var verts = new Vector3[res * res];
        var uvs = new Vector2[res * res];
        for (int r = 0; r < res; r++)
            for (int c = 0; c < res; c++)
            {
                int k = r * res + c;
                verts[k] = new Vector3(-size / 2f + c * cell, has ? t.heights[k] : 0f, -size / 2f + r * cell);
                uvs[k] = new Vector2(c * cell / 10f, r * cell / 10f); // texture repeats every 10 m
            }
        var tris = new int[(res - 1) * (res - 1) * 6];
        int n = 0;
        for (int r = 0; r < res - 1; r++)
            for (int c = 0; c < res - 1; c++)
            {
                int a = r * res + c, b = a + 1, cc = a + res, d = cc + 1; // diagonal a-d matches the editor
                tris[n++] = a; tris[n++] = cc; tris[n++] = d;
                tris[n++] = a; tris[n++] = d; tris[n++] = b;
            }
        var mesh = new Mesh { name = "Tile_" + t.ix + "_" + t.iz, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();

        var go = new GameObject(mesh.name);
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(t.x, 0f, t.z);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        if (groundMaterial != null) mr.sharedMaterial = groundMaterial;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    float GroundY(float x, float z) =>
        Physics.Raycast(new Vector3(x, 200f, z), Vector3.down, out var hit, 400f, groundMask) ? hit.point.y : 0f;

    // Same arc maths as the editor. Editor space = prefab space with X mirrored (the editor exports yaw + 180 and flips Z).
    // Bends every MeshFilter under 'root' (copies the meshes, so the prefab asset is untouched).
    // Needs Read/Write enabled on the imported meshes.
    static void ApplyBend(GameObject root, float deg, float dirDeg, bool lengthAxis)
    {
        var toRoot = root.transform.worldToLocalMatrix;
        var filters = new List<MeshFilter>();
        var mats = new List<Matrix4x4>();
        var all = new List<Vector3[]>();
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;

        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
            var m = toRoot * mf.transform.localToWorldMatrix;
            var v = mf.sharedMesh.vertices;
            for (int i = 0; i < v.Length; i++)
            {
                var p = m.MultiplyPoint3x4(v[i]);
                p.x = -p.x;
                v[i] = p;
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            filters.Add(mf); mats.Add(m); all.Add(v);
        }
        if (filters.Count == 0) return;

        float B = deg * Mathf.Deg2Rad, a = dirDeg * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
        float len = Mathf.Max(lengthAxis ? max.x - min.x : max.y - min.y, 0.5f);
        float R = len / B, baseY = min.y, cx = (min.x + max.x) * 0.5f;

        for (int f = 0; f < filters.Count; f++)
        {
            var v = all[f];
            for (int i = 0; i < v.Length; i++)
            {
                var p = v[i];
                if (!lengthAxis)
                {
                    float xr = p.x * ca + p.z * sa, zr = -p.x * sa + p.z * ca, s = p.y - baseY;
                    float nx = R - (R - xr) * Mathf.Cos(s / R);
                    p.y = baseY + (R - xr) * Mathf.Sin(s / R);
                    p.x = nx * ca - zr * sa;
                    p.z = nx * sa + zr * ca;
                }
                else
                {
                    float s = p.x - cx;
                    float nz = R - (R - p.z) * Mathf.Cos(s / R);
                    p.x = cx + (R - p.z) * Mathf.Sin(s / R);
                    p.z = nz;
                }
                p.x = -p.x;                                  // back to prefab space
                v[i] = mats[f].inverse.MultiplyPoint3x4(p);  // back to the mesh's own space
            }
            var mesh = Instantiate(filters[f].sharedMesh);
            mesh.vertices = v;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            filters[f].sharedMesh = mesh;
        }
    }
}