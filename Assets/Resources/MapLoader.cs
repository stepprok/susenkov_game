using System.Collections.Generic;
using UnityEngine;

public class MapLoader : MonoBehaviour
{
    public string resourceName = "map";
    public Material roadMaterial;

    [Header("Ground tiles (from the editor's 'New tile' tool)")]
    [Tooltip("Creates one ground mesh (with the sculpted terrain heights + MeshCollider) per tile. Leave off if you already have your own ground/terrain.")]
    public bool generateGround = false;
    public Material groundMaterial;
    public LayerMask groundMask = ~0;

    void Start()
    {
        var data = JsonUtility.FromJson<MapData>(Resources.Load<TextAsset>(resourceName).text);
        if (data.objects == null) data.objects = new MapObj[0];
        if (data.splines == null) data.splines = new MapSpline[0];

        // 1) ground tiles
        if (generateGround && data.tiles != null)
        {
            foreach (var t in data.tiles) BuildTile(t, data.tileSize);
            Physics.SyncTransforms();
        }

        // 2) sample ground heights BEFORE spawning anything, so props don't land on other props
        var objY = new float[data.objects.Length];
        for (int i = 0; i < objY.Length; i++) objY[i] = GroundY(data.objects[i].x, data.objects[i].z);

        // 3) objects
        for (int i = 0; i < data.objects.Length; i++)
        {
            var o = data.objects[i];
            var prefab = Resources.Load<GameObject>("MapPrefabs/" + o.prefab);
            if (prefab == null) { Debug.LogWarning("Missing prefab: " + o.prefab); continue; }

            var go = Instantiate(prefab, transform.position, Quaternion.identity, transform);
            if (Mathf.Abs(o.bend) > 0.01f) ApplyBend(go, o.bend, o.bendDir, o.bendAxis == "length");

            go.transform.SetPositionAndRotation(
                new Vector3(o.x, objY[i] + o.y, o.z),
                Quaternion.Euler(o.pitch, o.yaw, o.roll));
            go.transform.localScale *= o.scale;
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
    }

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
