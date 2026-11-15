using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace Swarm.Editor
{
    public static class CanyonJourneySetup
    {
        public const float HalfWidth=14;
        public static void Install()
        {
            var old=GameObject.Find("Journey / Canyon Passage"); if (old) Object.DestroyImmediate(old);
            var root=new GameObject("Journey / Canyon Passage").transform;
            var sand=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Sand.mat");
            var rock=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Rock.mat");
            var route=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Route.mat");
            var mine=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mine.mat");
            var points=CanyonLayout.JourneyPoints; var left=new Vector3[points.Length]; var right=new Vector3[points.Length];
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            for (int i=0;i<points.Length;i++)
            {
                Vector3 before=Flat(points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(0,i-1)]);
                Vector3 normal=Vector3.Cross(Vector3.up,before).normalized;
                Vector3 outgoing=Flat(points[Mathf.Min(i+1,points.Length-1)]-points[i]);
                if (outgoing.sqrMagnitude<.1f) outgoing=before;
                float width=(i>=3 && i<=6 ? 22 : HalfWidth)/Mathf.Max(.65f,Vector3.Dot(normal,Vector3.Cross(Vector3.up,outgoing).normalized));
                left[i]=points[i]-normal*width; right[i]=points[i]+normal*width;
                vertices.Add(left[i]); vertices.Add(right[i]);
                if (i==0) continue;
                int a=(i-1)*2; triangles.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});
            }
            var mesh=new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            const string meshPath="Assets/Geometry/CanyonPassageFloor.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (saved) { EditorUtility.CopySerialized(mesh,saved); Object.DestroyImmediate(mesh); }
            else { AssetDatabase.CreateAsset(mesh,meshPath); saved=mesh; }
            var floor=new GameObject("Continuous lower trail",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider)); floor.transform.SetParent(root);
            floor.GetComponent<MeshFilter>().sharedMesh=saved; floor.GetComponent<MeshCollider>().sharedMesh=saved; floor.GetComponent<Renderer>().sharedMaterial=sand;
            for (int i=0;i<points.Length-1;i++)
            {
                // Broad shelter mouth for the cross-map connection.
                Vector3 westFrom=left[i],westTo=left[i+1];
                if (i==6) westTo=Vector3.MoveTowards(westTo,westFrom,22);
                if (i==7) westFrom=Vector3.MoveTowards(westFrom,westTo,22);
                Cliff(root,westFrom,westTo,34+i%3*5,rock); Cliff(root,right[i],right[i+1],38+i%3*4,rock);
                Vector3 heading=Flat(points[i+1]-points[i]); float length=Vector3.Distance(points[i],points[i+1]);
                for (float d=8;d<length;d+=14)
                {
                    var mark=Block(root,"Trail direction marker",Vector3.Lerp(points[i],points[i+1],d/length)+Vector3.up*.03f,new Vector3(2,.05f,.6f),mine);
                    mark.transform.rotation=Quaternion.LookRotation(heading); mark.layer=2; Object.DestroyImmediate(mark.GetComponent<Collider>());
                }
            }
            // The basin exit stays in the basin; moving the station must not move these walls.
            var north=GameObject.Find("North wall"); if (north) Object.DestroyImmediate(north);
            foreach (int side in new[]{-1,1}) Block(root,"North opening wall",new Vector3(side*21,8,78),new Vector3(26,18,4),rock);
            Vector3 start=CanyonLayout.BridgeStart,end=CanyonLayout.BridgeEnd,axis=(end-start).normalized;
            float span=Vector3.Distance(start,end),halfGap=CanyonLayout.BridgeGap*.5f;
            Span(root,"Broken bridge / south span",start,start+axis*(span*.5f-halfGap),7,route);
            Span(root,"Broken bridge / north span",start+axis*(span*.5f+halfGap),end,7,route);
            Ramp(root,"Bridge upper approach",new Vector3(28,-2,190),start,8,route);
            Ramp(root,"Bridge north descent",end,new Vector3(-25,-2,330),8,route);
            Vector3 sideward=Vector3.Cross(Vector3.up,axis);
            foreach (float t in new[]{.08f,.3f,.7f,.92f}) foreach (int side in new[]{-1,1})
            {
                Vector3 p=Vector3.Lerp(start,end,t)+sideward*side*3.2f;
                Block(root,"Bridge trestle",new Vector3(p.x,2.5f,p.z),new Vector3(1,11,1),rock);
                Block(root,"Bridge edge post",p+Vector3.up*.7f,new Vector3(.35f,1.4f,.35f),mine);
            }
            var zip=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zipline.mat");
            ZiplineSetup.Line(root,"Bridge transfer zip",Vector3.Lerp(start,end,.25f)+Vector3.up*3,Vector3.Lerp(start,end,.96f)+Vector3.up*3,16,zip);
            var wall=Block(root,"Bridge upper bounce wall",Vector3.Lerp(start,end,.3f)+sideward*4.3f+Vector3.up*3,new Vector3(.8f,6,10),rock);
            wall.transform.rotation=Quaternion.LookRotation(axis);
            var pocket=Block(root,"Trail shelter / regroup marker",new Vector3(-36,-1.97f,342),new Vector3(10,.04f,12),mine);
            pocket.layer=2; Object.DestroyImmediate(pocket.GetComponent<Collider>());
            Block(root,"Trail shelter / canopy",new Vector3(-39,3.5f,342),new Vector3(8,.5f,14),rock);
            foreach (int z in new[]{336,348})
                Block(root,"Trail shelter / canopy post",new Vector3(-42,.75f,z),new Vector3(.6f,5.5f,.6f),rock);
            foreach (int z in new[]{338,346})
                Block(root,"Trail shelter / low cover",new Vector3(-41,-1.3f,z),new Vector3(2,1.4f,2),rock);
            Sign(root,"TRAIL SHELTER / REGROUP",new Vector3(-36,1,334),Vector3.forward,rock);
            QuarterPipe(root,floor.GetComponent<MeshCollider>(),route,rock);
            MovementFeatures(root,floor.GetComponent<MeshCollider>(),route,rock,mine,zip);
            Sign(root,"DRILL STATION / CANYON PASS",new Vector3(8,3,87),Vector3.forward,rock);
            Sign(root,"BRIDGE ABOVE / GULLY BELOW",new Vector3(35,1,196),Vector3.forward,rock);
            Sign(root,"DRILL STATION / FINAL APPROACH",new Vector3(-7,3,586),new Vector3(15,0,35),rock);
        }
        static void QuarterPipe(Transform root,MeshCollider floor,Material route,Material rock)
        {
            Physics.SyncTransforms();
            const int rows=8,columns=12;
            const float radius=7.247f;
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            for (int row=0;row<=rows;row++)
            {
                float along=18+row*26f/rows;
                // Taper the two ends into ordinary terrain; the middle is a full curve.
                float taper=Mathf.Min(1,Mathf.Min(row,rows-row)/2f);
                for (int col=0;col<=columns;col++)
                {
                    float angle=col*75f/columns*Mathf.Deg2Rad;
                    Vector3 p=CanyonLayout.QuarterPipePoint(along,5+radius*Mathf.Sin(angle));
                    p.y=FloorHeight(floor,p)+radius*(1-Mathf.Cos(angle))*1.2f*taper+.03f; vertices.Add(p);
                    if (row==0 || col==0) continue;
                    int a=(row-1)*(columns+1)+col-1,b=a+columns+1;
                    triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});
                }
            }
            var mesh=new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            const string path="Assets/Geometry/CanyonQuarterPipe.asset"; var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved) { EditorUtility.CopySerialized(mesh,saved); Object.DestroyImmediate(mesh); }
            else { AssetDatabase.CreateAsset(mesh,path); saved=mesh; }
            var pipe=new GameObject("Downhill / mountainside quarter pipe",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider)); pipe.transform.SetParent(root);
            pipe.GetComponent<MeshFilter>().sharedMesh=saved; pipe.GetComponent<MeshCollider>().sharedMesh=saved; pipe.GetComponent<Renderer>().sharedMaterial=route;
            Vector3 backing=CanyonLayout.QuarterPipePoint(31,15); backing.y=FloorHeight(floor,backing)+3;
            var mountain=Block(root,"Quarter pipe / mountain backing",backing,new Vector3(6,12,30),rock); mountain.transform.rotation=Quaternion.LookRotation(CanyonLayout.QuarterPipeAxis);
            Sign(root,"MOUNTAINSIDE QUARTER PIPE",CanyonLayout.QuarterPipePoint(15,-6)+Vector3.down*1.5f,CanyonLayout.QuarterPipeAxis,rock);
        }
        static void MovementFeatures(Transform root,MeshCollider floor,Material route,Material rock,Material mine,Material zip)
        {
            Physics.SyncTransforms();
            var knots=CanyonLayout.BankPoints; var vertices=new List<Vector3>(); var triangles=new List<int>();
            for (int i=0;i<knots.Length;i++)
            {
                Vector3 direction=Flat(knots[Mathf.Min(i+1,knots.Length-1)]-knots[Mathf.Max(0,i-1)]);
                Vector3 side=Vector3.Cross(Vector3.up,direction);
                Vector3 inner=knots[i]-side*5,outer=knots[i]-side*12;
                inner.y=FloorHeight(floor,inner)+.035f;
                outer.y=FloorHeight(floor,outer)+(i==0 || i==knots.Length-1 ? .035f : 3.2f);
                vertices.Add(inner); vertices.Add(outer);
                if (i==0) continue;
                int a=(i-1)*2; triangles.AddRange(new[]{a,a+1,a+2,a+1,a+3,a+2});
            }
            var mesh=new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            const string path="Assets/Geometry/CanyonBankedTurn.asset"; var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved) { EditorUtility.CopySerialized(mesh,saved); Object.DestroyImmediate(mesh); }
            else { AssetDatabase.CreateAsset(mesh,path); saved=mesh; }
            var bank=new GameObject("Narrows / banked slide turn",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider)); bank.transform.SetParent(root);
            bank.GetComponent<MeshFilter>().sharedMesh=saved; bank.GetComponent<MeshCollider>().sharedMesh=saved; bank.GetComponent<Renderer>().sharedMaterial=route;
            float[] distances={14,30,46},sides={7,5,7},heights={6,6.7f,5.8f};
            for (int i=0;i<3;i++)
            {
                Vector3 p=CanyonLayout.NarrowsPoint(distances[i],sides[i],heights[i]);
                float ground=FloorHeight(floor,p),height=p.y-ground;
                var ledge=Block(root,"Narrows / staggered ledge "+(i+1),p-Vector3.up*height*.5f,new Vector3(6,height,12),rock);
                ledge.transform.rotation=Quaternion.LookRotation(CanyonLayout.NarrowsAxis);
                var cap=Block(root,"Ledge route marker",p+Vector3.up*.025f,new Vector3(4,.04f,10),route);
                cap.transform.rotation=ledge.transform.rotation; cap.layer=2; Object.DestroyImmediate(cap.GetComponent<Collider>());
            }
            Vector3 entry=CanyonLayout.NarrowsPoint(2,7,3),exit=CanyonLayout.NarrowsPoint(60,7,2);
            entry.y=FloorHeight(floor,entry); exit.y=FloorHeight(floor,exit);
            Ramp(root,"Narrows ledges / ascent",entry,CanyonLayout.NarrowsPoint(8,7,6),6,route);
            Ramp(root,"Narrows ledges / return slope",CanyonLayout.NarrowsPoint(52,7,5.8f),exit,6,route);
            ZiplineSetup.Line(root,"Narrows shortcut zip",new Vector3(-23,4.5f,412),CanyonLayout.NarrowsPoint(10,5,8.8f),18,zip);
            Vector3 forward=Flat(CanyonLayout.JourneyPoints[10]-CanyonLayout.JourneyPoints[9]);
            Vector3 right=Vector3.Cross(Vector3.up,forward),arch=Vector3.Lerp(CanyonLayout.JourneyPoints[9],CanyonLayout.JourneyPoints[10],.48f);
            foreach (int side in new[]{-1,1})
            {
                var pillar=Block(root,"Final passage / split rock arch",arch+right*side*10+Vector3.up*5,new Vector3(3,10,4),rock);
                pillar.transform.rotation=Quaternion.LookRotation(forward);
            }
            var lintel=Block(root,"Final passage / arch crown",arch+Vector3.up*11,new Vector3(23,2.5f,4),rock); lintel.transform.rotation=Quaternion.LookRotation(forward);
            Sign(root,"BANKED TURN / KEEP YOUR SPEED",new Vector3(-30,3,352),new Vector3(-15,0,50),rock);
            Sign(root,"ROCK LEDGES / LOWER BYPASS",CanyonLayout.NarrowsPoint(0,-7,5),CanyonLayout.NarrowsAxis,rock);
        }
        static float FloorHeight(MeshCollider floor,Vector3 point)
        {
            return floor.Raycast(new Ray(new Vector3(point.x,100,point.z),Vector3.down),out var hit,150) ? hit.point.y : point.y;
        }
        static Vector3 Flat(Vector3 value) => Vector3.ProjectOnPlane(value,Vector3.up).normalized;
        static void Cliff(Transform root,Vector3 from,Vector3 to,float height,Material material)
        {
            float bottom=Mathf.Min(from.y,to.y)-3,top=Mathf.Max(from.y,to.y)+height;
            var obj=Block(root,"Canyon rock face",new Vector3((from.x+to.x)*.5f,(bottom+top)*.5f,(from.z+to.z)*.5f),new Vector3(4,top-bottom,FlatDistance(from,to)+2),material);
            obj.transform.rotation=Quaternion.LookRotation(Flat(to-from));
        }
        static float FlatDistance(Vector3 a,Vector3 b) => Vector3.ProjectOnPlane(b-a,Vector3.up).magnitude;
        static GameObject Block(Transform root,string name,Vector3 position,Vector3 scale,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name; obj.transform.SetParent(root); obj.transform.position=position; obj.transform.localScale=scale; obj.GetComponent<Renderer>().sharedMaterial=material; return obj;
        }
        static void Span(Transform root,string name,Vector3 from,Vector3 to,float width,Material material)
        {
            var obj=Block(root,name,(from+to)*.5f-Vector3.up*.5f,new Vector3(width,1,Vector3.Distance(from,to)),material); obj.transform.rotation=Quaternion.LookRotation(to-from);
        }
        static void Ramp(Transform root,string name,Vector3 from,Vector3 to,float width,Material material)
        {
            Quaternion rotation=Quaternion.LookRotation(to-from);
            var obj=Block(root,name,(from+to)*.5f-rotation*Vector3.up*.3f,new Vector3(width,.6f,Vector3.Distance(from,to)),material); obj.transform.rotation=rotation;
        }
        static void Sign(Transform root,string title,Vector3 position,Vector3 direction,Material material)
        {
            Quaternion rotation=Quaternion.LookRotation(direction);
            var board=Block(root,"Journey sign / "+title,position,new Vector3(10,1.3f,.2f),material); board.transform.rotation=rotation;
            board.layer=2; Object.DestroyImmediate(board.GetComponent<Collider>());
            var text=new GameObject(title,typeof(TextMesh)); text.transform.SetParent(board.transform,false); text.transform.localPosition=Vector3.back*.6f;
            // Counter board scale so text stays uniformly sized in world space.
            text.transform.localScale=new Vector3(.1f,1/1.3f,5);
            var mesh=text.GetComponent<TextMesh>(); mesh.text=title; mesh.fontSize=64; mesh.characterSize=.1f; mesh.anchor=TextAnchor.MiddleCenter; mesh.color=Color.white;
        }
    }
}
