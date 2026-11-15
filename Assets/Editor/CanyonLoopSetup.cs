using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Swarm.Editor
{
    public static class CanyonLoopSetup
    {
        public static void Install()
        {
            var old=GameObject.Find("Journey / Western Loop"); if (old) Object.DestroyImmediate(old);
            var root=new GameObject("Journey / Western Loop").transform;
            var sand=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Sand.mat");
            var rock=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Rock.mat");
            var route=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Route.mat");
            var mine=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mine.mat");
            var metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DrillSteel.mat");
            var zip=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zipline.mat");
            Corridor(root,"Western ridge",CanyonLayout.StationPumpRoute,24,24,38,sand,rock,mine);
            Corridor(root,"Return gully",CanyonLayout.PumpBasinRoute,24,38,24,sand,rock,mine);
            Corridor(root,"Central cut",CanyonLayout.MiddleRoute,18,40,46,sand,rock,route);

            CanyonCenterSetup.Install(root,sand,rock,route);

            // Replace only the basin's boundary: preserve its existing upper routes.
            var west=GameObject.Find("West canyon"); if (west) Object.DestroyImmediate(west);
            Block(root,"Basin west boundary / south",new Vector3(-32,8,-10),new Vector3(4,18,40),rock);
            Block(root,"Basin west boundary / north",new Vector3(-32,8,55),new Vector3(4,18,50),rock);
            Vector3 c=CanyonLayout.PumpCenter;
            Block(root,"Pumping yard / main floor",c-Vector3.up,new Vector3(92,2,104),sand);
            // Open north/south/east mouths; enclosing rock keeps this a distinct place.
            Block(root,"Pumping yard / west mountain",c+new Vector3(-48,17,0),new Vector3(6,38,110),rock);
            foreach (int side in new[]{-1,1})
            {
                Block(root,"Pumping yard / north shoulder",c+new Vector3(side*32,14,53),new Vector3(28,32,6),rock);
                Block(root,"Pumping yard / south shoulder",c+new Vector3(side*32,14,-53),new Vector3(28,32,6),rock);
                Block(root,"Pumping yard / east shoulder",c+new Vector3(47,14,side*37),new Vector3(6,32,30),rock);
            }
            Block(root,"Relay tower / base",c+new Vector3(-12,4,4),new Vector3(14,8,14),metal);
            Block(root,"Relay tower / upper deck",c+new Vector3(-12,8.5f,4),new Vector3(20,1,20),route);
            Block(root,"Relay tower / mast",c+new Vector3(-20,19,4),new Vector3(2,20,2),mine);
            Block(root,"Relay tower / beacon",c+new Vector3(-20,29.5f,4),new Vector3(5,1,5),route);
            Ramp(root,"Relay deck / walking ascent",c+new Vector3(-12,0,-32),c+new Vector3(-12,9,-6),8,route);
            Ramp(root,"Relay deck / broad return slide",c+new Vector3(-12,9,14),c+new Vector3(-12,0,43),10,route);
            foreach (int z in new[]{-20,20})
            {
                Block(root,"Pumping yard / equipment bounce wall",c+new Vector3(20,4,z),new Vector3(1,8,12),metal);
                Block(root,"Pumping yard / pump housing",c+new Vector3(27,2,z),new Vector3(9,4,10),mine);
            }
            var console=Block(root,"Sentinel activation console",CanyonLayout.BossConsole+Vector3.up*.65f,new Vector3(1.8f,1.3f,1.2f),mine);
            console.layer=2;
            Object.DestroyImmediate(console.GetComponent<Collider>());
            var ringObject = new GameObject("Sentinel interaction ring",typeof(LineRenderer));
            ringObject.transform.SetParent(root);
            ringObject.layer = 2;
            var ring = ringObject.GetComponent<LineRenderer>();
            ring.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DrillSignal.mat");
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.startWidth = ring.endWidth = .14f;
            ring.loop = true;
            ring.positionCount = 96;
            for (int i=0; i<ring.positionCount; i++)
            {
                float angle = i*Mathf.PI*2/ring.positionCount;
                var offset = new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*CombatPlayground.BossInteractRange;
                ring.SetPosition(i,CanyonLayout.BossConsole+offset+Vector3.up*.09f);
            }
            Sign(root,"SENTINEL CONSOLE / E TO START",CanyonLayout.BossConsole+Vector3.up*2.4f,Vector3.forward,metal);
            ZiplineSetup.Line(root,"Pumping yard / deck transfer zip",c+new Vector3(16,4,-30),c+new Vector3(-7,15,8),16,zip);
            // A raised ridge rewards movement without obstructing the ordinary route.
            ZiplineSetup.Line(root,"Western ridge / ridge transfer zip",new Vector3(-150,14,610),new Vector3(-210,16,540),18,zip);
            var landmark=new GameObject("Pumping yard landmark",typeof(CanyonLandmark)); landmark.transform.SetParent(root);
            landmark.transform.position=c+new Vector3(0,15,0);
            landmark.GetComponent<CanyonLandmark>().title="PUMPING YARD";
            landmark.GetComponent<CanyonLandmark>().hint="North: drill station / east: shelter shortcut / south: mining basin";
            Sign(root,"PUMPING YARD / WESTERN RIDGE",new Vector3(-52,4,642),Vector3.left,metal);
            Sign(root,"PUMPING YARD / RETURN GULLY",new Vector3(-39,3,12),Vector3.left,metal);
            Sign(root,"PUMPING YARD / CENTRAL SHORTCUT",new Vector3(-49,4,383),Vector3.left,metal);
            Sign(root,"NORTH: DRILL / EAST: SHELTER / SOUTH: BASIN",c+new Vector3(30,4,9),Vector3.left,metal);
        }

        static void Corridor(Transform root,string title,Vector3[] points,float width,float startWidth,float endWidth,Material sand,Material rock,Material marker)
        {
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            var left=new Vector3[points.Length]; var right=new Vector3[points.Length];
            for (int i=0;i<points.Length;i++)
            {
                Vector3 direction=Flat(points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(0,i-1)]);
                Vector3 side=Vector3.Cross(Vector3.up,direction);
                Vector3 outgoing=i+1<points.Length ? Flat(points[i+1]-points[i]) : direction;
                float half=(i==0 ? startWidth : i==points.Length-1 ? endWidth : width)*.5f/Mathf.Max(.7f,Vector3.Dot(side,Vector3.Cross(Vector3.up,outgoing)));
                left[i]=points[i]-side*half; right[i]=points[i]+side*half;
                vertices.Add(left[i]); vertices.Add(right[i]);
                if (i>0) { int a=(i-1)*2; triangles.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3}); }
            }
            var mesh=new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string path="Assets/Geometry/"+title.Replace(" ","")+"Floor.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved) { EditorUtility.CopySerialized(mesh,saved); Object.DestroyImmediate(mesh); }
            else { AssetDatabase.CreateAsset(mesh,path); saved=mesh; }
            var floor=new GameObject(title+" / continuous floor",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider)); floor.transform.SetParent(root);
            floor.GetComponent<MeshFilter>().sharedMesh=saved; floor.GetComponent<MeshCollider>().sharedMesh=saved; floor.GetComponent<Renderer>().sharedMaterial=sand;
            for (int i=0;i<points.Length-1;i++)
            {
                // Walls stop short at mouths so merging routes do not fence one another off.
                for (int side=0;side<2;side++)
                {
                    var edge=side==0 ? left : right;
                    Vector3 a=edge[i],b=edge[i+1];
                    // Open only the inward side where the flats meet the rim.
                    if (title=="Western ridge" && i==0 && side==0)
                        b=Vector3.Lerp(a,b,.29f);
                    if (title=="Return gully" && i==4 && side==0)
                    {
                        Wall(root,title,a,Vector3.Lerp(a,b,.24f),32,rock);
                        a=Vector3.Lerp(a,b,.53f);
                    }
                    if (title=="Central cut")
                    {
                        if (i==0) a=Vector3.MoveTowards(a,b,12);
                        if (i==1 || i==2) continue;
                        if (i==3) a=Vector3.Lerp(a,b,.28f);
                    }
                    float height=title=="Central cut" ? 26 : 32;
                    Wall(root,title,a,b,height,rock);
                }
                float length=Vector3.Distance(points[i],points[i+1]);
                for (float d=10;d<length;d+=18)
                {
                    var stripe=Block(root,title+" / route stripe",Vector3.Lerp(points[i],points[i+1],d/length)+Vector3.up*.035f,new Vector3(2,.04f,.6f),marker);
                    stripe.transform.rotation=Quaternion.LookRotation(Flat(points[i+1]-points[i]));
                    stripe.layer=2; Object.DestroyImmediate(stripe.GetComponent<Collider>());
                }
            }
        }
        static void Wall(Transform root,string title,Vector3 a,Vector3 b,float height,Material rock)
        {
            Vector3 middle=(a+b)*.5f;
            float bottom=Mathf.Min(a.y,b.y)-3;
            float top=Mathf.Max(a.y,b.y)+height;
            middle.y=(bottom+top)*.5f;
            var wall=Block(root,title+" / rock face",middle,new Vector3(4,top-bottom,Vector3.ProjectOnPlane(b-a,Vector3.up).magnitude+4),rock);
            wall.transform.rotation=Quaternion.LookRotation(Flat(b-a));
        }
        static Vector3 Flat(Vector3 v) => Vector3.ProjectOnPlane(v,Vector3.up).normalized;
        static GameObject Block(Transform root,string name,Vector3 p,Vector3 size,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name; obj.transform.SetParent(root); obj.transform.position=p; obj.transform.localScale=size; obj.GetComponent<Renderer>().sharedMaterial=material; return obj;
        }
        static void Ramp(Transform root,string name,Vector3 from,Vector3 to,float width,Material material)
        {
            Quaternion rotation=Quaternion.LookRotation(to-from);
            var obj=Block(root,name,(from+to)*.5f-rotation*Vector3.up*.3f,new Vector3(width,.6f,Vector3.Distance(from,to)),material); obj.transform.rotation=rotation;
        }
        static void Sign(Transform root,string title,Vector3 position,Vector3 forward,Material material)
        {
            var board=Block(root,"Loop sign / "+title,position,new Vector3(14,1.3f,.2f),material); board.transform.rotation=Quaternion.LookRotation(forward);
            board.layer=2; Object.DestroyImmediate(board.GetComponent<Collider>());
            var label=new GameObject(title,typeof(TextMesh)); label.transform.SetParent(root); label.transform.position=position-forward*.13f; label.transform.rotation=board.transform.rotation;
            var text=label.GetComponent<TextMesh>(); text.text=title; text.anchor=TextAnchor.MiddleCenter; text.fontSize=64; text.characterSize=.095f; text.color=Color.white;
        }
    }
}
