using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Swarm.Editor
{
    public static class DrillBowlSetup
    {
        public static Vector3 Center => CanyonLayout.StationCenter;
        // Keep the established station template intact, then place it as one unit.
        static readonly Vector3 TemplateCenter=new Vector3(0,0,145);
        const int Segments=64;
        [MenuItem("Swarm/Add Drill Bowl Test")]
        public static void Install()
        {
            var old=GameObject.Find("POI / Drill Bowl Test"); if (old) Object.DestroyImmediate(old);
            var root=new GameObject("POI / Drill Bowl Test").transform; root.position=TemplateCenter;
            var sand=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Sand.mat");
            var rock=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Rock.mat");
            var route=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Route.mat");
            var mine=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mine.mat");
            var metal=Material("DrillSteel",new Color(.23f,.29f,.33f));
            var signal=Material("DrillSignal",new Color(1,.6f,.08f));
            signal.shader=Shader.Find("Unlit/Color");
            signal.EnableKeyword("_EMISSION"); signal.SetColor("_EmissionColor",signal.color); EditorUtility.SetDirty(signal);
            var radii=new List<float>(); var heights=new List<float>();
            for (int i=1;i<=12;i++) { float r=i*2; radii.Add(r); heights.Add(8*(r/24)*(r/24)); }
            Surface(root,"Bowl / momentum surface",SurfaceMesh(radii.ToArray(),heights.ToArray(),true),"DrillBowl",sand);
            Surface(root,"Rim / safe loop",SurfaceMesh(new[] {24f,32f},new[] {8f,8f},false),"DrillRim",route);
            Surface(root,"Outer retaining wall",SurfaceMesh(new[] {32f,32f},new[] {8f,0f},false),"DrillRetainingWall",rock);
            Surface(root,"Recovery apron",SurfaceMesh(new[] {32f,50f},new[] {0f,0f},false),"DrillApron",sand);
            foreach (Vector3 direction in new[] { Vector3.right,Vector3.left,Vector3.forward,Vector3.back })
                Ramp(root,"Outer recovery ramp",TemplateCenter+direction*44,TemplateCenter+direction*32+Vector3.up*8,6,sand);
            foreach (int x in new[] {-12,12}) foreach (int z in new[] {-12,12})
                Block(root,"Drill gantry / bounce column",TemplateCenter+new Vector3(x,16,z),new Vector3(1.5f,24,1.5f),metal);
            foreach (int side in new[] {-1,1})
            {
                Block(root,"Gantry outer frame",TemplateCenter+new Vector3(0,28,side*12),new Vector3(26,1.2f,1.2f),metal);
                Block(root,"Gantry outer frame",TemplateCenter+new Vector3(side*12,28,0),new Vector3(1.2f,1.2f,26),metal);
            }
            Block(root,"Drill gantry crossbeam",TemplateCenter+new Vector3(0,28,0),new Vector3(28,1.5f,3),mine);
            Block(root,"Drill gantry longitudinal beam",TemplateCenter+new Vector3(0,28,0),new Vector3(3,1.5f,28),metal);
            BuildStation(root,metal,route,mine);
            var hazardObj=new GameObject("Cycling drill",typeof(DrillHazard)); hazardObj.transform.SetParent(root,false);
            var hazard=hazardObj.GetComponent<DrillHazard>();
            var head=new GameObject("Moving drill head"); head.transform.SetParent(hazardObj.transform,false);
            hazard.head=head.transform; head.transform.localPosition=Vector3.up*DrillHazard.RaisedTip;
            var cone=new GameObject("Drill cone",typeof(MeshFilter),typeof(MeshRenderer)); cone.layer=2; cone.transform.SetParent(head.transform,false);
            cone.GetComponent<MeshFilter>().sharedMesh=SaveMesh(HeadMesh(),"DrillHead"); cone.GetComponent<Renderer>().sharedMaterial=metal;
            var housing=GameObject.CreatePrimitive(PrimitiveType.Cylinder); housing.name="Fixed piston housing"; housing.transform.SetParent(hazardObj.transform,false);
            housing.transform.localPosition=Vector3.up*21.5f; housing.transform.localScale=new Vector3(1.7f,6.5f,1.7f); housing.GetComponent<Renderer>().sharedMaterial=metal;
            var shaft=GameObject.CreatePrimitive(PrimitiveType.Cylinder); shaft.name="Moving piston"; shaft.layer=2; shaft.transform.SetParent(head.transform,false);
            shaft.transform.localPosition=Vector3.up*10; shaft.transform.localScale=new Vector3(1.2f,5,1.2f);
            Object.DestroyImmediate(shaft.GetComponent<Collider>()); shaft.GetComponent<Renderer>().sharedMaterial=metal;
            var ring=new GameObject("Drill danger footprint",typeof(LineRenderer)); ring.layer=2; ring.transform.SetParent(hazardObj.transform,false);
            var line=ring.GetComponent<LineRenderer>(); line.useWorldSpace=false; line.loop=true; line.positionCount=64; line.startWidth=line.endWidth=.1f; line.sharedMaterial=signal;
            for (int i=0;i<64;i++) { float a=i*Mathf.PI*2/64; float r=DrillHazard.HeadRadius+.7f; line.SetPosition(i,new Vector3(Mathf.Cos(a)*r,8*(r/24)*(r/24)+.18f,Mathf.Sin(a)*r)); }
            hazard.warning=line;
            var lamp=GameObject.CreatePrimitive(PrimitiveType.Sphere); lamp.name="Drill warning beacon"; lamp.layer=2; lamp.transform.SetParent(hazardObj.transform,false);
            lamp.transform.localPosition=Vector3.up*29; lamp.transform.localScale=Vector3.one*1.4f; Object.DestroyImmediate(lamp.GetComponent<Collider>());
            lamp.GetComponent<Renderer>().sharedMaterial=signal; hazard.beacon=lamp.GetComponent<Renderer>();
            var sign=new GameObject("Drill station landmark",typeof(CanyonLandmark)); sign.transform.SetParent(root,false); sign.transform.localPosition=new Vector3(0,12,-26);
            sign.GetComponent<CanyonLandmark>().title="DRILLING STATION"; sign.GetComponent<CanyonLandmark>().hint="West rim: safe route / east bowl: escape zip / deck: return slide";
            Vector3 offset=Center-TemplateCenter; root.position+=offset;
            foreach (var zip in root.GetComponentsInChildren<MiningZipline>())
            { zip.endpointA+=offset; zip.endpointB+=offset; }
            foreach (var worldLine in root.GetComponentsInChildren<LineRenderer>())
                if (worldLine.useWorldSpace)
                    for (int i=0;i<worldLine.positionCount;i++) worldLine.SetPosition(i,worldLine.GetPosition(i)+offset);
            CanyonJourneySetup.Install();
            CanyonLoopSetup.Install();
            DownloadSetup.Install();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
        static void BuildStation(Transform root,Material metal,Material route,Material mine)
        {
            // Both approaches remain open: the existing broad rim is the safe route;
            // the uninterrupted bowl is the shorter, timed crossing beneath the drill.
            Block(root,"Maintenance deck / main floor",new Vector3(0,11.5f,187),new Vector3(52,1,16),metal);
            foreach (int side in new[] {-1,1})
            {
                Ramp(root,"Outer route / deck approach",new Vector3(side*20,8,165),new Vector3(side*20,12,179),8,route);
                foreach (int z in new[] {182,193})
                    Block(root,"Maintenance deck support",new Vector3(side*23,5.5f,z),new Vector3(1.5f,11,1.5f),metal);
                if (side<0) Block(root,"Deck side parapet",new Vector3(-25.5f,12.6f,188),new Vector3(1,1.2f,14),mine);
                else foreach (int z in new[] {185,193})
                    Block(root,"Deck side parapet / access gaps",new Vector3(25.5f,12.6f,z),new Vector3(1,1.2f,4),mine);
                Block(root,"Deck equipment / bounce face",new Vector3(side*16,13.5f,190),new Vector3(4,3,5),mine);
                Block(root,"Deck low cover",new Vector3(side*7,12.6f,185),new Vector3(3,1.2f,2),metal);
            }
            Ramp(root,"Bowl route / direct deck exit",new Vector3(0,8,173),new Vector3(0,12,179),10,route);
            Block(root,"Maintenance deck / rear wall",new Vector3(0,14,195),new Vector3(52,4,1),mine);
            Ramp(root,"Station return slide",new Vector3(-10,8,167),new Vector3(-10,12,179),6,route);
            var zipMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zipline.mat");
            ZiplineSetup.Line(root,"Station escape zip",new Vector3(10,8,157),new Vector3(10,17,184),14,zipMaterial);
            Block(root,"Station zip bounce wall",new Vector3(7,7,158),new Vector3(.6f,8,6),metal);
            var bounceMarker=Block(root,"Station bounce face marker",new Vector3(7.32f,7.5f,158),new Vector3(.04f,.2f,4),zipMaterial);
            Object.DestroyImmediate(bounceMarker.GetComponent<Collider>());
            Block(root,"Station supply overlook",new Vector3(36,13,186),new Vector3(12,1,14),metal);
            Ramp(root,"Supply overlook / walking detour",new Vector3(23,12,181),new Vector3(30,13.5f,181),5,route);
            foreach (int z in new[] {181,191})
                Block(root,"Supply overlook support",new Vector3(40,6.5f,z),new Vector3(1.3f,13,1.3f),metal);
            Block(root,"Supply overlook / backstop",new Vector3(41.5f,15,186),new Vector3(1,2,14),mine);
            Block(root,"Supply overlook / jump lip",new Vector3(25.5f,12.1f,189),new Vector3(1,.2f,4),route);
            Sign(root,"WEST RIM / SAFE ROUTE",new Vector3(-21,10,122),metal);
            Sign(root,"DECK RETURN / SLIDE",new Vector3(-10,14.5f,180),metal);
            Sign(root,"SUPPLY OVERLOOK / OPTIONAL",new Vector3(21,14.5f,180),metal);
            // A broad resting area off the west rim gives six players room to gather.
            Block(root,"West route / staging platform",new Vector3(-30,7.5f,145),new Vector3(12,1,18),metal);
            foreach (int z in new[] {138,152})
                Block(root,"West platform / equipment",new Vector3(-34,9.5f,z),new Vector3(2,3,2),mine);
            // Low, nonblocking colored stripes make the safe route legible in greybox.
            for (int i=0;i<=12;i++)
            {
                float angle=-Mathf.PI/2-i*Mathf.PI/12;
                Vector3 point=TemplateCenter+new Vector3(Mathf.Cos(angle)*28,8.025f,Mathf.Sin(angle)*28);
                var stripe=Block(root,"West rim / route marker",point,new Vector3(3,.04f,.35f),mine);
                stripe.transform.rotation=Quaternion.Euler(0,-angle*Mathf.Rad2Deg,0);
                Object.DestroyImmediate(stripe.GetComponent<Collider>());
            }
        }
        static void Sign(Transform root,string text,Vector3 position,Material metal)
        {
            var board=Block(root,"Route sign / "+text,position,new Vector3(8,1.2f,.2f),metal);
            board.layer=2; Object.DestroyImmediate(board.GetComponent<Collider>());
            var label=new GameObject(text,typeof(TextMesh)); label.transform.SetParent(root);
            label.transform.position=position+Vector3.back*.12f;
            var mesh=label.GetComponent<TextMesh>(); mesh.text=text; mesh.anchor=TextAnchor.MiddleCenter;
            mesh.alignment=TextAlignment.Center; mesh.fontSize=64; mesh.characterSize=.1f; mesh.color=Color.white;
        }
        static Mesh SurfaceMesh(float[] radii,float[] heights,bool center)
        {
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            if (center) vertices.Add(Vector3.zero);
            int offset=center ? 1 : 0;
            for (int r=0;r<radii.Length;r++) for (int i=0;i<=Segments;i++)
            { float angle=i*Mathf.PI*2/Segments; vertices.Add(new Vector3(Mathf.Cos(angle)*radii[r],heights[r],Mathf.Sin(angle)*radii[r])); }
            if (center) for (int i=0;i<Segments;i++) triangles.AddRange(new[] {0,offset+i+1,offset+i});
            for (int r=0;r<radii.Length-1;r++) for (int i=0;i<Segments;i++)
            {
                int a=offset+r*(Segments+1)+i,b=a+Segments+1;
                triangles.AddRange(new[] {a,a+1,b,b,a+1,b+1});
            }
            var mesh=new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh HeadMesh()
        {
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            for (int i=0;i<32;i++)
            {
                float a=i*Mathf.PI*2/32,b=(i+1)*Mathf.PI*2/32; int start=vertices.Count;
                vertices.Add(Vector3.zero); vertices.Add(new Vector3(Mathf.Cos(a)*DrillHazard.HeadRadius,DrillHazard.HeadHeight,Mathf.Sin(a)*DrillHazard.HeadRadius)); vertices.Add(new Vector3(Mathf.Cos(b)*DrillHazard.HeadRadius,DrillHazard.HeadHeight,Mathf.Sin(b)*DrillHazard.HeadRadius));
                triangles.AddRange(new[] {start,start+1,start+2});
                start=vertices.Count; vertices.Add(Vector3.up*DrillHazard.HeadHeight); vertices.Add(new Vector3(Mathf.Cos(b)*DrillHazard.HeadRadius,DrillHazard.HeadHeight,Mathf.Sin(b)*DrillHazard.HeadRadius)); vertices.Add(new Vector3(Mathf.Cos(a)*DrillHazard.HeadRadius,DrillHazard.HeadHeight,Mathf.Sin(a)*DrillHazard.HeadRadius)); triangles.AddRange(new[] {start,start+1,start+2});
            }
            var mesh=new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh SaveMesh(Mesh mesh,string name)
        {
            System.IO.Directory.CreateDirectory("Assets/Geometry"); string path="Assets/Geometry/"+name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); return existing; }
            AssetDatabase.CreateAsset(mesh,path); return mesh;
        }
        static void Surface(Transform parent,string name,Mesh mesh,string asset,Material material)
        {
            var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider)); obj.transform.SetParent(parent,false);
            var saved=SaveMesh(mesh,asset); obj.GetComponent<MeshFilter>().sharedMesh=saved; obj.GetComponent<MeshCollider>().sharedMesh=saved; obj.GetComponent<Renderer>().sharedMaterial=material;
        }
        static Material Material(string name,Color color)
        {
            string path="Assets/Materials/"+name+".mat"; var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material=new Material(Shader.Find("Standard")); material.color=color; AssetDatabase.CreateAsset(material,path); }
            return material;
        }
        static GameObject Block(Transform root,string name,Vector3 position,Vector3 scale,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name; obj.transform.SetParent(root); obj.transform.position=position; obj.transform.localScale=scale;
            obj.GetComponent<Renderer>().sharedMaterial=material; return obj;
        }
        static void Ramp(Transform root,string name,Vector3 bottom,Vector3 top,float width,Material material)
        {
            var rotation=Quaternion.LookRotation((top-bottom).normalized,Vector3.up);
            var obj=Block(root,name,(top+bottom)*.5f-rotation*Vector3.up*.3f,new Vector3(width,.6f,Vector3.Distance(bottom,top)),material); obj.transform.rotation=rotation;
        }
        public static void InstallAndBuild()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/CanyonPlayground.unity"); Install();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets(); MultiplayerSetup.BuildWindows();
        }
    }
}
