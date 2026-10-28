// Plain data classes for the map JSON. Keep this file separate from MapLoader.cs.
[System.Serializable] public class MapObj { public string prefab; public float x, y, z, yaw, pitch, roll, scale = 1f, bend, bendDir; public string bendAxis; }
[System.Serializable] public class MapPt { public float x, z; }
[System.Serializable] public class MapSpline { public string kind; public float width; public bool closed; public MapPt[] points; }
[System.Serializable] public class MapTile { public float x, z; public int ix, iz, res; public float cell; public float[] heights; }
[System.Serializable] public class MapData
{
    public float tileSize = 600f;
    public MapTile[] tiles;
    public MapObj[] objects;
    public MapSpline[] splines;
}
