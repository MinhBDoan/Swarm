using System.Collections.Generic;
using UnityEngine;
namespace Swarm
{
    public class GrindRail : MonoBehaviour
    {
        public Vector3[] points;
        public static readonly List<GrindRail> Rails=new List<GrindRail>();
        public float Length {get {float n=0;for(int i=1;i<points.Length;i++)n+=Vector3.Distance(points[i-1],points[i]);return n;}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void ResetRegistry(){Rails.Clear();}
        void OnEnable(){if(!Rails.Contains(this))Rails.Add(this);}
        void OnDisable(){Rails.Remove(this);}
        public Vector3 At(float distance,out Vector3 tangent)
        {
            tangent=Vector3.forward;
            if(points==null || points.Length<2)return transform.position;
            for(int i=1;i<points.Length;i++)
            {
                Vector3 delta=points[i]-points[i-1];float length=delta.magnitude;
                if(length<.001f)continue;
                tangent=delta/length;
                if(distance<=length || i==points.Length-1)return points[i-1]+tangent*Mathf.Clamp(distance,0,length);
                distance-=length;
            }
            return points[points.Length-1];
        }
        public float ClosestDistance(Vector3 position)
        {
            float best=float.MaxValue,result=0,travel=0;
            for(int i=1;i<points.Length;i++)
            {
                Vector3 delta=points[i]-points[i-1];float length=delta.magnitude;
                if(length<.001f)continue;
                float along=Mathf.Clamp(Vector3.Dot(position-points[i-1],delta/length),0,length);
                float square=(position-(points[i-1]+delta/length*along)).sqrMagnitude;
                if(square<best){best=square;result=travel+along;}travel+=length;
            }
            return result;
        }
    }
}
