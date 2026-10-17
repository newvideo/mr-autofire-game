using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Polyus.Editor
{
    // Opens the supplied scene, never overwrites the user's scene.
    [InitializeOnLoad]
    public static class PolyusPrototypeBuilder
    {
        public const string ScenePath = "Assets/Polyus/Scenes/PolyusDemo.unity";
        static PolyusPrototypeBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(ScenePath)) return;
                EnsureArtImported();
                string key = "Polyus.Demo." + PolyusRunRules.DemoVersion + ".Welcome:" + Application.dataPath;
                if (!EditorPrefs.GetBool(key, false))
                {
                    EditorPrefs.SetBool(key, true);
                    PolyusWelcome.ShowWindow();
                }
            };
        }
        [MenuItem("Polyus/Открыть демоверсию")]
        [MenuItem("Polyus/Create or open prototype")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Сначала останови Play."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureArtImported();
            EditorSceneManager.OpenScene(ScenePath);
            Selection.activeGameObject = GameObject.Find("POLYUS Demo");
        }
        private static void EnsureArtImported()
        {
            foreach (string name in new[] { "background", "hero", "drone", "boss" })
            {
                string path = "Assets/Polyus/Resources/Polyus/Art/" + name + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || importer.textureType == TextureImporterType.Sprite) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }
    }

    public sealed class PolyusWelcome : EditorWindow
    {
        public static void ShowWindow()
        {
            var window = GetWindow<PolyusWelcome>(true, "ПОЛЮС · демоверсия " + PolyusRunRules.DemoVersion, true);
            window.minSize = new Vector2(540, 340);
        }
        private void OnGUI()
        {
            GUILayout.Space(22);
            GUILayout.Label("ПОЛЮС", new GUIStyle(EditorStyles.boldLabel) { fontSize = 36, alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(18);
            GUILayout.Label("3 сектора · смена гравитации · усиления · босс", new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(12);
            EditorGUILayout.HelpBox("Нажми «Открыть игру», затем ▶ Play сверху редактора.\n\nA / D — движение · Пробел — прыжок · G — гравитация.\nРеклама и покупки работают в тестовом режиме.", MessageType.Info);
            GUILayout.Space(18);
            if (GUILayout.Button("ОТКРЫТЬ ИГРУ", GUILayout.Height(56))) { PolyusPrototypeBuilder.Create(); Close(); }
            GUILayout.Space(8);
            GUILayout.Label("Твои сцены и настройки проекта не заменяются.", new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });
        }
    }

    public sealed class PolyusArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Polyus/Resources/Polyus/Art/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
        }
    }
}
