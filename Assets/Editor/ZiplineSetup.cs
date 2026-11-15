using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Swarm.Editor
{
    public static class ZiplineSetup
    {
        public static void AddLinesToScene()
        {
            var old = GameObject.Find("Mining Ziplines");
            if (old) Object.DestroyImmediate(old);
            var root = new GameObject("Mining Ziplines").transform;
            const string materialPath = "Assets/Materials/Zipline.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                material = new Material(Shader.Find("Standard"));
                material.color = new Color(.15f,.9f,1);
                material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",material.color*.7f);
                AssetDatabase.CreateAsset(material,materialPath);
            }
            Line(root,"Hoist lift line",new Vector3(-11,1.3f,22),new Vector3(-11,12,22),9,material);
            Line(root,"Basin crossing line",new Vector3(-22,8.2f,14),new Vector3(23,10,44),14,material);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Hoist bounce wall"; wall.transform.SetParent(root);
            wall.transform.position = new Vector3(-14,4,24);
            wall.transform.localScale = new Vector3(.6f,8,8);
            wall.transform.rotation = Quaternion.Euler(0,12,0);
            wall.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Rock.mat");
            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "Suggested bounce face"; stripe.layer = 2; stripe.transform.SetParent(wall.transform,false);
            stripe.transform.localPosition = new Vector3(.515f,.1f,0);
            stripe.transform.localScale = new Vector3(.03f,.025f,.7f);
            Object.DestroyImmediate(stripe.GetComponent<Collider>()); stripe.GetComponent<Renderer>().sharedMaterial = material;
            var deck = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Route.mat");
            var rock = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Rock.mat");
            Block(root,"Hoist landing perch",new Vector3(-7,9.5f,18),new Vector3(6,1,6),deck);
            foreach (int x in new[] {-9,-5})
                Block(root,"Perch support",new Vector3(x,4.5f,19),new Vector3(.6f,9,.6f),rock);
            Vector3 top = new Vector3(-7,10,15), bottom = new Vector3(-7,0,-10);
            Quaternion slopeRotation = Quaternion.LookRotation((bottom-top).normalized,Vector3.up);
            var slope = Block(root,"Perch return slide",(top+bottom)*.5f-slopeRotation*Vector3.up*.25f,
                new Vector3(5,.5f,Vector3.Distance(top,bottom)),deck);
            slope.transform.rotation = slopeRotation;
            foreach (var sign in Object.FindObjectsOfType<CanyonLandmark>())
            {
                if (sign.title == "ABANDONED CAMP") { sign.title = "MINING ENTRY YARD"; sign.hint = "Regroup / basin ahead"; }
                if (sign.title == "BROKEN AQUEDUCT") { sign.title = "BROKEN ORE CONVEYOR"; sign.hint = "Upper routes / optional supply cache"; }
            }
        }
        static GameObject Block(Transform root, string title, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = title; obj.transform.SetParent(root); obj.transform.position = position;
            obj.transform.localScale = scale; obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }
        public static void Line(Transform root, string title, Vector3 start, Vector3 end, float speed, Material material)
        {
            var obj = new GameObject(title,typeof(MiningZipline),typeof(LineRenderer)); obj.transform.SetParent(root);
            var zip = obj.GetComponent<MiningZipline>(); zip.endpointA = start; zip.endpointB = end; zip.travelSpeed = speed;
            var line = obj.GetComponent<LineRenderer>(); line.positionCount = 2; line.SetPosition(0,start); line.SetPosition(1,end);
            line.startWidth = line.endWidth = .14f; line.sharedMaterial = material;
            foreach (var position in new[] { start,end })
            {
                var anchor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                anchor.name = "Line anchor"; anchor.layer = 2; anchor.transform.SetParent(obj.transform);
                anchor.transform.position = position; anchor.transform.localScale = Vector3.one*.4f;
                Object.DestroyImmediate(anchor.GetComponent<Collider>()); anchor.GetComponent<Renderer>().sharedMaterial = material;
            }
        }
        public static void InstallAndBuild()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/CanyonPlayground.unity");
            AddLinesToScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            MultiplayerSetup.BuildWindows();
        }
    }
}
