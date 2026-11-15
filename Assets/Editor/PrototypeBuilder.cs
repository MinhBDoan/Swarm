using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Swarm.Editor
{
    public static class PrototypeBuilder
    {
        static Transform zone;
        [MenuItem("Swarm/Build Canyon Playground")]
        public static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sand = Material("Sand", new Color(.65f,.39f,.22f));
            var rock = Material("Rock", new Color(.36f,.23f,.19f));
            var route = Material("Route", new Color(.16f,.58f,.63f));
            var camp = Material("Camp",new Color(.38f,.52f,.35f));
            var mine = Material("Mine",new Color(.85f,.62f,.18f));
            zone = new GameObject("World / Canyon").transform;
            Block("Canyon floor", new Vector3(0,-1,25), new Vector3(70,2,110), sand);
            Block("West canyon", new Vector3(-32,8,25), new Vector3(4,18,110), rock);
            Block("East canyon", new Vector3(32,8,25), new Vector3(4,18,110), rock);
            Block("North wall", new Vector3(0,8,78), new Vector3(64,18,4), rock);
            Block("South wall", new Vector3(0,8,-28), new Vector3(64,18,4), rock);
            zone = new GameObject("POI / Abandoned Camp").transform;
            for (int side=-1; side<=1; side+=2)
            {
                Block("Camp shelter",new Vector3(side*15,3.3f,-20),new Vector3(9,.5f,7),camp);
                Block("Shelter post",new Vector3(side*19,1.5f,-22),new Vector3(.5f,3,.5f),rock);
                Block("Supply crates",new Vector3(side*14,.7f,-22),new Vector3(3,1.4f,2),camp);
            }
            Mark("Camp gathering patch",new Vector3(0,.025f,-20),new Vector3(9,.05f,7),camp);
            Landmark("ABANDONED CAMP","Regroup / ground route ahead",new Vector3(0,4,-23));

            zone = new GameObject("POI / Mining Basin").transform;
            Block("Central mesa",new Vector3(0,3,28),new Vector3(10,6,18),rock);
            Block("Mine hoist west",new Vector3(-8,6,28),new Vector3(1,12,2),mine);
            Block("Mine hoist east",new Vector3(8,6,28),new Vector3(1,12,2),mine);
            Block("Mine hoist crossbeam",new Vector3(0,12,28),new Vector3(18,1,2),mine);
            for (int z=-4; z<=12; z+=4)
                Mark("Ground route",new Vector3(0,.025f,z),new Vector3(4,.05f,2),mine);
            foreach (int x in new[] {-12,12})
                for (int z=16; z<=40; z+=6)
                    Mark("Basin loop",new Vector3(x,.025f,z),new Vector3(3,.05f,2),mine);
            Landmark("MINING BASIN","Combat loop / routes around the mesa",new Vector3(0,14,28));

            zone = new GameObject("POI / Broken Aqueduct").transform;
            Ramp("West ascent",new Vector3(-22,0,-6),new Vector3(-22,5.5f,14),9,sand);
            Block("West upper route",new Vector3(-22,5,26.5f),new Vector3(9,1,25),route);
            Block("West wall bounce face",new Vector3(-27,8.5f,25),new Vector3(1,6,12),rock);
            Block("Inner wall bounce face",new Vector3(-17,8.5f,34),new Vector3(1,6,8),rock);
            Ramp("West aqueduct join",new Vector3(-22,5.5f,39),new Vector3(-22,7,48),8,route);
            Ramp("East long ascent and return slide",new Vector3(23,0,5),new Vector3(23,7,34),10,sand);
            Block("Aqueduct cache terrace",new Vector3(23,6.5f,45.5f),new Vector3(12,1,23),route);
            // Five-meter optional jump; the east ramp is a continuous alternate route.
            Block("Broken aqueduct west span",new Vector3(-15.5f,6.5f,48),new Vector3(21,1,6),route);
            Block("Broken aqueduct east span",new Vector3(14,6.5f,48),new Vector3(28,1,6),route);
            foreach (int x in new[] {-20,-10,6,16})
                Block("Aqueduct support",new Vector3(x,3.25f,48),new Vector3(1.5f,6.5f,2),rock);
            Block("Aqueduct landmark tower",new Vector3(27,10,61),new Vector3(3,20,3),rock);
            Block("Aqueduct tower cap",new Vector3(27,20.5f,61),new Vector3(5,1,5),route);
            for (int i=0;i<3;i++)
                Block("Return bounce pillar",new Vector3(-10+i*10,2,62),new Vector3(2,4,2),rock);
            Landmark("BROKEN AQUEDUCT","Upper loop / optional supply cache",new Vector3(12,12,49));
            zone = null;
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player"; player.layer = 2; player.transform.position = new Vector3(0,2,-15);
            Object.DestroyImmediate(player.GetComponent<Collider>());
            player.GetComponent<Renderer>().sharedMaterial = Material("Player", new Color(.9f,.8f,.2f));
            var controller = player.AddComponent<CharacterController>();
            controller.height = 2; controller.radius = .45f; controller.stepOffset = .3f;
            var motor = player.AddComponent<MomentumMotor>();
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(OrbitCamera));
            cameraObject.tag = "MainCamera";
            cameraObject.GetComponent<Camera>().fieldOfView = 85;
            cameraObject.GetComponent<OrbitCamera>().target = player.transform;
            motor.view = cameraObject.transform;
            var sun = new GameObject("Sun", typeof(Light));
            sun.GetComponent<Light>().type = LightType.Directional;
            sun.GetComponent<Light>().intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(45,-30,0);
            RenderSettings.ambientLight = new Color(.55f,.62f,.7f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.64f,.72f,.78f); RenderSettings.fogDensity = .006f;
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            ZiplineSetup.AddLinesToScene();
            DrillBowlSetup.Install();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), "Assets/Scenes/CanyonPlayground.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/CanyonPlayground.unity", true) };
            Debug.Log("SWARM: Canyon playground generated successfully.");
        }
        public static void BuildForPlaytest() { Build(); MultiplayerSetup.BuildWindows(); }
        public static void BuildAndCapture()
        {
            MultiplayerSetup.BuildWindows();
            CaptureOverview();
        }
        public static void CaptureOverview()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/CanyonPlayground.unity");
            // Cut away only the foreground walls in this documentation render.
            var hidden = new System.Collections.Generic.List<Renderer>();
            foreach (string wall in new[] { "East canyon", "South wall" })
            { var renderer = GameObject.Find(wall).GetComponent<Renderer>(); renderer.enabled = false; hidden.Add(renderer); }
            var cameraObject = new GameObject("Overview camera",typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(80,115,-80);
            camera.transform.LookAt(new Vector3(0,3,25));
            camera.orthographic = true; camera.orthographicSize = 52; camera.aspect = 1.6f;
            camera.farClipPlane = 500; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.09f,.13f,.18f);
            var target = new RenderTexture(1600,1000,24);
            var previous = RenderTexture.active;
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            var texture = new Texture2D(1600,1000,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1600,1000),0,0); texture.Apply();
            System.IO.Directory.CreateDirectory("Docs");
            System.IO.File.WriteAllBytes("Docs/CanyonOverview.png",texture.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = previous;
            Object.DestroyImmediate(texture); target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(cameraObject);
            foreach (var renderer in hidden) renderer.enabled = true;
            Debug.Log("SWARM: Canyon overview rendered.");
        }
        static void Ramp(string name, Vector3 start, Vector3 end, float width, Material material)
        {
            Quaternion rotation = Quaternion.LookRotation((end-start).normalized,Vector3.up);
            var ramp = Block(name,(start+end)*.5f-rotation*Vector3.up*.25f,
                new Vector3(width,.5f,Vector3.Distance(start,end)),material);
            ramp.transform.rotation = rotation;
        }
        static void Mark(string name, Vector3 position, Vector3 scale, Material material)
        {
            var mark = Block(name,position,scale,material);
            mark.layer = 2; Object.DestroyImmediate(mark.GetComponent<Collider>());
        }
        static void Landmark(string name, string hint, Vector3 position)
        {
            var sign = new GameObject(name,typeof(CanyonLandmark)); sign.transform.position = position;
            sign.transform.SetParent(zone); sign.GetComponent<CanyonLandmark>().title = name;
            sign.GetComponent<CanyonLandmark>().hint = hint;
        }
        static Material Material(string name, Color color)
        {
            System.IO.Directory.CreateDirectory("Assets/Materials");
            string path = "Assets/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat,path); }
            mat.color = color; return mat;
        }
        static GameObject Block(string name, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name; obj.transform.position = position; obj.transform.localScale = scale;
            if (zone) obj.transform.SetParent(zone);
            obj.GetComponent<Renderer>().sharedMaterial = material; return obj;
        }
    }
}
