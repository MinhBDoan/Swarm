using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swarm.Editor
{
    [InitializeOnLoad]
    public static class PrototypeStartup
    {
        const string ScenePath = "Assets/Scenes/CanyonPlayground.unity";

        static PrototypeStartup()
        {
            EditorApplication.delayCall += Configure;
        }

        static void Configure()
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (scene == null) return;
            // Play the prototype even if the editor initially opens an untitled scene.
            EditorSceneManager.playModeStartScene = scene;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var current = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            // Only replace an untouched startup scene, never unsaved user work.
            if (string.IsNullOrEmpty(current.path) && !current.isDirty)
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Swarm/Open Canyon Playground")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
