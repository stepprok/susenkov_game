using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Put this file in Assets/Editor/
[CustomEditor(typeof(MapLoader))]
public class MapLoaderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var l = (MapLoader)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Edit mode: builds the whole map as normal scene objects (prefabs stay linked; generated meshes, materials and the NavMesh are saved in Assets/" +
            l.bakedAssetsFolder + ") so you can edit it. Keep MapLoader on its own empty GameObject - everything under it is cleared on rebuild.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Build map into scene (editable)")) l.BuildInEditor();
            if (GUILayout.Button("Build + save as new scene")) { l.BuildInEditor(); SaveCopy(l); }
            if (GUILayout.Button("Save current scene as copy")) SaveCopy(l);
            if (GUILayout.Button("Clear built map"))
            {
                l.ClearBuilt();
                EditorUtility.SetDirty(l);
                EditorSceneManager.MarkSceneDirty(l.gameObject.scene);
            }
        }
    }

    static void SaveCopy(MapLoader l)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/" + l.bakedSceneName + ".unity");
        if (EditorSceneManager.SaveScene(l.gameObject.scene, path, true))
        {
            AssetDatabase.Refresh();
            Debug.Log("Scene saved as " + path);
        }
    }

    // "Bake Scene On Play": build + save a scene copy right before entering Play mode (only if not built yet).
    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s != PlayModeStateChange.ExitingEditMode) return;
            foreach (var l in Object.FindObjectsOfType<MapLoader>())
                if (l.bakeSceneOnPlay && !l.IsBuilt) { l.BuildInEditor(); SaveCopy(l); }
        };
    }
}
