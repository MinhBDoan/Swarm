using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Swarm.Editor
{
    public static class CanyonCenterSetup
    {
        // South to north. The center is wide, with short flares where it meets the rim.
        static readonly float[,] Rows =
        {
            {60,-128,-107,0}, {80,-134,-104,0}, {120,-151,-69,-1},
            {200,-199,-61,-3}, {290,-243,-63,-4}, {330,-249,-62,-4},
            {375,-251,-63,-4}, {415,-240,-62,-4}, {515,-185,-58,-3},
            {590,-128,-53,0}, {638,-94,-62,1}, {650,-94,-62,1}
        };

        public static void Install(Transform root, Material sand, Material rock, Material route)
        {
            var center = new GameObject("Central flats").transform;
            center.SetParent(root);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < Rows.GetLength(0); i++)
            {
                for (int side = 0; side < 2; side++)
                {
                    float x = Rows[i,1+side], z = Rows[i,0];
                    float y = i >= Rows.GetLength(0)-2 ? (-x-46)*2/49 : Rows[i,3];
                    vertices.Add(new Vector3(x,y,z));
                }
                if (i == 0) continue;
                int a = (i-1)*2;
                triangles.AddRange(new[] { a,a+2,a+1,a+1,a+2,a+3 });
            }
            var mesh = new Mesh { name = "Central Flats Floor" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();
            const string path = "Assets/Geometry/CentralFlatsFloor.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved)
            {
                EditorUtility.CopySerialized(mesh,saved);
                Object.DestroyImmediate(mesh);
            }
            else
            {
                AssetDatabase.CreateAsset(mesh,path);
                saved = mesh;
            }
            var ground = new GameObject("Central flats floor",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
            ground.transform.SetParent(center);
            ground.GetComponent<MeshFilter>().sharedMesh = saved;
            ground.GetComponent<MeshCollider>().sharedMesh = saved;
            ground.GetComponent<MeshRenderer>().sharedMaterial = sand;

            for (int segment = 0; segment < Rows.GetLength(0)-1; segment++)
            for (int side = 0; side < 2; side++)
            {
                // Leave the ends open. The existing cut crosses above these low banks.
                if (segment == 0 || segment == Rows.GetLength(0)-2) continue;
                Vector3 a = vertices[segment*2+side], b = vertices[(segment+1)*2+side];
                Edge(center,a,b,rock);
            }

            // Small cover islands leave several clear lanes across the open floor.
            Block(center,"West rock island",new Vector3(-177,-1.5f,272),new Vector3(18,5,12),rock,24);
            Block(center,"East rock island",new Vector3(-89,-1.8f,315),new Vector3(12,4.4f,18),rock,-20);
            Block(center,"North rock island",new Vector3(-144,-1,455),new Vector3(18,5,10),rock,18);
            Block(center,"South rock island",new Vector3(-113,-1,204),new Vector3(14,4,11),rock,-20);

            // Broad terrain folds create speed decisions without subdividing the flats into rooms.
            // Their edges taper exactly to the existing floor, so every side remains recoverable.
            TerrainFold(center,"South crossing / rolling ridge",new Vector3(-150,0,240),50,68,4.6f,15,sand);
            TerrainFold(center,"Middle junction / sweeping bank",new Vector3(-152,0,322),56,58,3.8f,-18,sand);
            TerrainFold(center,"North shelf / rolling approach",new Vector3(-158,0,485),34,62,3.4f,0,sand);
            Block(center,"South crossing / offset bounce face",new Vector3(-122,-1.1f,251),new Vector3(2,5,13),rock,15);
            Block(center,"Middle junction / navigation landmark",new Vector3(-181,-.8f,329),new Vector3(7,6.4f,9),rock,-18);
            // The center cut crosses just north of the bank. Its existing walking route stays open.
            Sign(center,"SOUTH RIDGE / SLIDE CROSSING",new Vector3(-150,2,201),Vector3.forward,route);
            Sign(center,"MIDDLE JUNCTION / WEST: PUMPING YARD",new Vector3(-163,2,354),Vector3.back,route);
            Block(center,"North shelf",new Vector3(-101,-2.9f,468),new Vector3(15,4,24),rock);
            Ramp(center,"North shelf / ascent",new Vector3(-101,-3.5f,436),new Vector3(-101,-.9f,456),10,route);
            Ramp(center,"North shelf / slide",new Vector3(-101,-.9f,480),new Vector3(-101,-3.2f,499),10,route);
            Ramp(center,"North shelf / west walking ascent",new Vector3(-131,FloorHeight(-131,459)+.04f,459),new Vector3(-108.5f,-.9f,459),6,route);
            Block(center,"North shelf / bounce face",new Vector3(-108,-.1f,469),new Vector3(1.5f,2.2f,10),rock);

            // Traversal routes leave the middle lane clear for combat.
            var zipMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zipline.mat");
            ZiplineSetup.Line(center,"Flats / shelf transfer zip",
                new Vector3(-105,-.4f,416),new Vector3(-102,3,470),15,zipMaterial);

            Block(center,"West terrace / first landing",new Vector3(-204,-2.25f,275),new Vector3(9,3.5f,12),rock);
            Block(center,"West terrace / second landing",new Vector3(-202,-2.15f,291),new Vector3(9,3.8f,12),rock);
            Ramp(center,"West terrace / ascent",new Vector3(-204,-3.65f,255),new Vector3(-204,-.5f,269),8,route);
            Ramp(center,"West terrace / return slide",new Vector3(-202,-.25f,297),new Vector3(-202,-4,312),8,route);
            Block(center,"West terrace / bounce face",new Vector3(-208,-.1f,277),new Vector3(1.4f,5,7),rock);

            Sign(center,"FLATS CACHE / E",new Vector3(-82,.6f,459),Vector3.forward,route);
            Sign(center,"WEST TERRACE / JUMP GAP",new Vector3(-204,-.4f,252),Vector3.forward,route);

            var marker = new GameObject("Central flats landmark",typeof(CanyonLandmark));
            marker.transform.SetParent(center);
            marker.transform.position = new Vector3(-143,5,305);
            var landmark = marker.GetComponent<CanyonLandmark>();
            landmark.title = "CENTRAL FLATS";
            landmark.hint = "North: drill station / west: pumping yard / south: mining basin";
            Sign(center,"CENTRAL FLATS / DRILL STATION",new Vector3(-81,3,619),Vector3.back,route);
            Sign(center,"CENTRAL FLATS / MINING BASIN",new Vector3(-118,2,88),Vector3.forward,route);
            Sign(center,"CENTRAL FLATS / PUMPING YARD",new Vector3(-231,4,350),Vector3.right,route);
        }

        static float FloorHeight(float x,float z)
        {
            for (int i=0;i<Rows.GetLength(0)-1;i++)
            {
                if (z<Rows[i,0] || z>Rows[i+1,0]) continue;
                float t=Mathf.InverseLerp(Rows[i,0],Rows[i+1,0],z);
                // All folds lie below the northern mouth, where each floor row is level across X.
                return Mathf.Lerp(Rows[i,3],Rows[i+1,3],t);
            }
            return 0;
        }

        static void TerrainFold(Transform root,string name,Vector3 origin,float width,float length,float rise,float yaw,Material material)
        {
            const int columns=12,rows=16;
            var vertices=new List<Vector3>();
            var triangles=new List<int>();
            Quaternion rotation=Quaternion.Euler(0,yaw,0);
            for (int row=0;row<=rows;row++)
            for (int col=0;col<=columns;col++)
            {
                float u=(float)col/columns,v=(float)row/rows;
                Vector3 point=origin+rotation*new Vector3((u-.5f)*width,0,(v-.5f)*length);
                float across=Mathf.Sin(u*Mathf.PI),along=Mathf.Sin(v*Mathf.PI);
                point.y=FloorHeight(point.x,point.z)+.025f+rise*across*across*along*along;
                vertices.Add(point);
                if (row==0 || col==0) continue;
                int a=(row-1)*(columns+1)+col-1,b=a+columns+1;
                triangles.AddRange(new[] {a,b,a+1,a+1,b,b+1});
            }
            var mesh=new Mesh {name=name};
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string path="Assets/Geometry/"+name.Replace(" / ","_").Replace(" ","")+".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved) {EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);}
            else {AssetDatabase.CreateAsset(mesh,path);saved=mesh;}
            var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
            obj.transform.SetParent(root);
            obj.GetComponent<MeshFilter>().sharedMesh=saved;
            obj.GetComponent<MeshRenderer>().sharedMaterial=material;
            obj.GetComponent<MeshCollider>().sharedMesh=saved;
        }

        static void Ramp(Transform root,string name,Vector3 from,Vector3 to,float width,Material material)
        {
            Quaternion rotation = Quaternion.LookRotation(to-from);
            var ramp = Block(root,name,(from+to)*.5f-rotation*Vector3.up*.3f,
                new Vector3(width,.6f,Vector3.Distance(from,to)),material);
            ramp.transform.rotation = rotation;
        }

        static void Edge(Transform root, Vector3 a, Vector3 b, Material rock)
        {
            Vector3 axis = Vector3.ProjectOnPlane(b-a,Vector3.up);
            Vector3 position = (a+b)*.5f;
            float bottom = Mathf.Min(a.y,b.y)-2, top = Mathf.Max(a.y,b.y)+2.5f;
            position.y = (bottom+top)*.5f;
            var barrier = Block(root,"Outer rock bank",position,new Vector3(3,top-bottom,axis.magnitude+3),rock);
            barrier.transform.rotation = Quaternion.LookRotation(axis);
        }

        static GameObject Block(Transform root,string name,Vector3 position,Vector3 size,Material material,float rotation=0)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(root);
            obj.transform.position = position;
            obj.transform.localScale = size;
            obj.transform.rotation = Quaternion.Euler(0,rotation,0);
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }

        static void Sign(Transform root,string title,Vector3 position,Vector3 heading,Material material)
        {
            var obj = Block(root,title,position,new Vector3(13,1.25f,.2f),material);
            obj.transform.rotation = Quaternion.LookRotation(heading);
            Object.DestroyImmediate(obj.GetComponent<Collider>());
            foreach (int side in new[] {-1,1})
            {
                var label = new GameObject(title,typeof(TextMesh));
                label.transform.SetParent(root);
                label.transform.position = position+heading*(side*.12f);
                label.transform.rotation = Quaternion.LookRotation(-heading*side);
                var text = label.GetComponent<TextMesh>();
                text.text = title;
                text.fontSize = 64;
                text.characterSize = .095f;
                text.anchor = TextAnchor.MiddleCenter;
            }
        }
    }
}
