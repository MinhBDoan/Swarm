using UnityEngine;
namespace Swarm
{
    // Fixed traversal lines; run encounter randomization does not move terrain.
    public class CentralGrindRails : MonoBehaviour
    {
        Material material;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install(){if(GameObject.Find("Central flats grind rails"))return;new GameObject("Central flats grind rails").AddComponent<CentralGrindRails>();}
        void Start(){Physics.SyncTransforms();Build();}
        void Build()
        {
            material=new Material(Shader.Find("Standard"));material.color=new Color(.18f,.85f,.92f);
            Add("South straight",new[]{new Vector3(-115,0,150),new Vector3(-115,0,190)});
            Add("South diagonal",new[]{new Vector3(-205,0,225),new Vector3(-175,0,255)});
            Add("Middle straight",new[]{new Vector3(-120,0,300),new Vector3(-120,0,345)});
            Add("Middle curve",new[]{new Vector3(-210,0,400),new Vector3(-205,0,413),new Vector3(-195,0,425),new Vector3(-180,0,432),new Vector3(-165,0,435)});
            Add("North diagonal",new[]{new Vector3(-120,0,470),new Vector3(-150,0,500)});
            Add("North curve",new[]{new Vector3(-145,0,540),new Vector3(-135,0,550),new Vector3(-120,0,555),new Vector3(-105,0,550),new Vector3(-95,0,540)});
        }
        void Add(string title,Vector3[] anchors)
        {
            // Sample terrain along the entire line, so slopes carry naturally.
            var points=new System.Collections.Generic.List<Vector3>();
            for(int i=1;i<anchors.Length;i++)
            {
                int steps=Mathf.CeilToInt(Vector3.Distance(anchors[i-1],anchors[i])/3);
                for(int j=i==1 ? 0 : 1;j<=steps;j++)
                {
                    Vector3 p=Vector3.Lerp(anchors[i-1],anchors[i],j/(float)steps);
                    if(!Physics.Raycast(new Vector3(p.x,45,p.z),Vector3.down,out var hit,90,1)){Debug.LogWarning($"Rail {title} has no floor at {p}");return;}
                    points.Add(hit.point+Vector3.up*.7f);
                }
            }
            var railObject=new GameObject(title+" grind rail");railObject.transform.SetParent(transform);
            var rail=railObject.AddComponent<GrindRail>();rail.points=points.ToArray();
            var line=railObject.AddComponent<LineRenderer>();line.useWorldSpace=true;line.positionCount=points.Count;
            line.SetPositions(rail.points);line.startWidth=line.endWidth=.18f;line.sharedMaterial=material;
            for(int i=0;i<points.Count;i+=3)
            {
                var post=GameObject.CreatePrimitive(PrimitiveType.Cylinder);post.name="Rail support";post.layer=2;Destroy(post.GetComponent<Collider>());
                post.transform.SetParent(railObject.transform);post.transform.position=points[i]-Vector3.up*.35f;post.transform.localScale=new Vector3(.13f,.35f,.13f);post.GetComponent<Renderer>().sharedMaterial=material;
            }
        }
        void OnDestroy(){if(material)Destroy(material);}
    }
}
