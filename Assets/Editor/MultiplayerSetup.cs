using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.UIElements;

namespace Swarm.Editor
{
    [InitializeOnLoad]
    public static class MultiplayerSetup
    {
        const string PrefabPath = "Assets/Resources/NetworkPlayer.prefab";
        static MultiplayerSetup() { EditorApplication.delayCall += EnsurePrefab; }

        static void EnsurePrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)) Prepare();
        }

        [MenuItem("Swarm/Prepare Multiplayer")]
        public static void Prepare()
        {
            System.IO.Directory.CreateDirectory("Assets/Resources");
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "NetworkPlayer"; player.layer = 2;
            Object.DestroyImmediate(player.GetComponent<Collider>());
            var controller = player.AddComponent<CharacterController>();
            controller.height = 2; controller.radius = .45f; controller.stepOffset = .3f;
            controller.enabled = false;
            player.AddComponent<MomentumMotor>().enabled = false;
            player.AddComponent<NetworkObject>();
            var sync = player.AddComponent<OwnerNetworkTransform>();
            sync.SyncScaleX = sync.SyncScaleY = sync.SyncScaleZ = false;
            player.AddComponent<NetworkAvatar>();
            PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);
            Object.DestroyImmediate(player);
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Debug.Log("SWARM_NET: network player prefab prepared.");
        }

        static void EnsureCombatHudPanel()
        {
            const string path = "Assets/Resources/CombatHudPanel.asset";
            if (AssetDatabase.LoadAssetAtPath<PanelSettings>(path)) return;
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/Resources/CombatHudTheme.tss");
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.clearColor = false;
            panel.clearDepthStencil = false;
            AssetDatabase.CreateAsset(panel,path);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Swarm/Build Windows Playtest")]
        public static void BuildWindows()
        {
            EnsurePrefab();
            EnsureCombatHudPanel();
            var report = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/CanyonPlayground.unity" },
                "Builds/Swarm/Swarm.exe", BuildTarget.StandaloneWindows64, BuildOptions.Development);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Windows build failed: " + report.summary.result);
        }
    }
}
