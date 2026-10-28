using UnityEngine;
using UnityEngine.AI;

// Adds the NavMesh that MapLoader baked into the scene (saved as an asset) while this object is active.
[ExecuteAlways]
public class MapNavMeshData : MonoBehaviour
{
    public NavMeshData data;
    NavMeshDataInstance instance;

    void OnEnable() { if (data != null) instance = NavMesh.AddNavMeshData(data); }
    void OnDisable() { instance.Remove(); }
}
