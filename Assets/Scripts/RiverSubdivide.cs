using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Rozdělí velké trojúhelníky řeky na malé, aby se mohla vlnit.
[RequireComponent(typeof(MeshFilter))]
public class RiverSubdivide : MonoBehaviour
{
    [Tooltip("Nejdelší hrana trojúhelníku v metrech")]
    public float maxEdge = 1.5f;

#if UNITY_EDITOR
    [ContextMenu("Subdivide river")]
    void Subdivide()
    {
        var mf = GetComponent<MeshFilter>();
        Mesh src = mf.sharedMesh;
        var verts = new List<Vector3>(src.vertices);
        var tris = new List<int>(src.triangles);
        Matrix4x4 m = transform.localToWorldMatrix;
        float limit = Mathf.Max(0.5f, maxEdge);

        for (int pass = 0; pass < 12; pass++)
        {
            var mids = new Dictionary<long, int>();
            var next = new List<int>(tris.Count);
            bool changed = false;

            // vrátí index středu hrany, nebo -1 když je hrana dost krátká
            int Mid(int a, int b)
            {
                if (m.MultiplyVector(verts[a] - verts[b]).magnitude <= limit) return -1;
                long key = ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
                if (!mids.TryGetValue(key, out int i))
                {
                    i = verts.Count;
                    verts.Add((verts[a] + verts[b]) * 0.5f);
                    mids[key] = i;
                }
                return i;
            }
            void T(int a, int b, int c) { next.Add(a); next.Add(b); next.Add(c); }

            for (int t = 0; t < tris.Count; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                int n = (ab >= 0 ? 1 : 0) + (bc >= 0 ? 1 : 0) + (ca >= 0 ? 1 : 0);
                if (n == 0) { T(a, b, c); continue; }
                changed = true;
                if (n == 3) { T(a, ab, ca); T(ab, b, bc); T(ca, bc, c); T(ab, bc, ca); continue; }

                // otočí trojúhelník tak, aby se dělila hrana a-b (a u dvou hran i b-c)
                for (int r = 0; r < 3 && !(ab >= 0 && (n == 1 || bc >= 0)); r++)
                {
                    int v = a; a = b; b = c; c = v;
                    int e = ab; ab = bc; bc = ca; ca = e;
                }
                if (n == 1) { T(a, ab, c); T(ab, b, c); }
                else { T(ab, b, bc); T(a, ab, bc); T(a, bc, c); }
            }
            tris = next;
            if (!changed) break;
            if (verts.Count > 1000000) { Debug.LogWarning("Moc vrcholů, zvyš Max Edge."); break; }
        }

        var mesh = new Mesh { name = "RiverSubdivided", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        Bounds bb = mesh.bounds;
        bb.Expand(2f / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x))); // rezerva na vlny
        mesh.bounds = bb;

        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/RiverSubdivided.asset");
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        Undo.RecordObject(mf, "Subdivide river");
        mf.sharedMesh = mesh;
        EditorUtility.SetDirty(mf);
        Debug.Log("Řeka rozdělena: " + verts.Count + " vrcholů, uloženo do " + path);
    }
#endif
}
