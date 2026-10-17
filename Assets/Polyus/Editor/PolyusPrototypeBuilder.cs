using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Polyus.Editor
{
    public static class PolyusPrototypeBuilder
    {
        [MenuItem("Polyus/Create or open prototype")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string path = "Assets/Scenes/PolyusPrototype.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                EditorSceneManager.OpenScene(path);
                return;
            }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Polyus Prototype").AddComponent<PolyusPrototype>();
            EditorSceneManager.SaveScene(scene, path);
            Selection.activeGameObject = GameObject.Find("Polyus Prototype");
            Debug.Log("Prototype created. Press Play. Your original Game scene is unchanged.");
        }
    }
}
