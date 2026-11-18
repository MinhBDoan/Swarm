using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Swarm.Editor
{
    // Reproducible build/capture entry points for the first parallel work batch.
    public static class ProductionBatchBuild
    {
        static string captureFolder="Docs/Playtest/FirstBatch";
        public static void CaptureBaseline()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/CanyonPlayground.unity");
            Capture("baseline");
        }

        public static void BuildIntegrated()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/CanyonPlayground.unity");
            CanyonLoopSetup.Install();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            MultiplayerSetup.Prepare();
            Capture("updated");
            BuildCurrent();
        }

        public static void BuildCurrent()
        {
            BuildTo("Builds/Swarm-TerrainTimerPause/Swarm.exe");
        }

        public static void BuildGroundedCenter()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/CanyonPlayground.unity");
            CanyonLoopSetup.Install();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            captureFolder="Docs/Playtest/CentralSlopesLoot";
            Capture("updated");
            CaptureView("grounded-path",new Vector3(-178,28,290),new Vector3(-182,-1,365),65);
            BuildTo("Builds/Swarm-CentralSlopesLoot/Swarm.exe");
        }

        static void BuildTo(string destination)
        {
            var result=BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/CanyonPlayground.unity"},
                destination,BuildTarget.StandaloneWindows64,BuildOptions.Development);
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Integrated playtest build failed: "+result.summary.result);
            Debug.Log("SWARM_BATCH_BUILD: PASS");
        }

        static void Capture(string stage)
        {
            Directory.CreateDirectory(captureFolder);
            bool fog=RenderSettings.fog; RenderSettings.fog=false;
            CaptureView(stage+"-flats",new Vector3(-430,420,-80),new Vector3(-145,-2,345),330);
            CaptureView(stage+"-south",new Vector3(-215,58,165),new Vector3(-150,-2,245),62);
            CaptureView(stage+"-junction",new Vector3(-210,62,285),new Vector3(-160,-3,356),64);
            CaptureView(stage+"-north",new Vector3(-175,55,400),new Vector3(-110,-2,469),62);
            RenderSettings.fog=fog;
        }

        static void CaptureView(string name,Vector3 position,Vector3 target,float size)
        {
            var obj=new GameObject("Batch documentation camera",typeof(Camera));
            var camera=obj.GetComponent<Camera>(); camera.transform.position=position; camera.transform.LookAt(target);
            camera.orthographic=true; camera.orthographicSize=size; camera.farClipPlane=2000;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.11f,.15f,.19f);
            var rt=new RenderTexture(1440,900,24); var previous=RenderTexture.active;
            camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
            var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1440,900),0,0); texture.Apply();
            File.WriteAllBytes(captureFolder+"/"+name+".png",texture.EncodeToPNG());
            camera.targetTexture=null; RenderTexture.active=previous;
            Object.DestroyImmediate(texture); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(obj);
        }
    }
}
