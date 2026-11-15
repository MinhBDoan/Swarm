using System.Collections.Generic;
using UnityEngine;

namespace Swarm
{
    public class MiningZipline : MonoBehaviour
    {
        public Vector3 endpointA, endpointB;
        public float travelSpeed = 14, excessSpeedDrag = 2;
        public static readonly List<MiningZipline> Lines = new List<MiningZipline>();
        public Vector3 Axis => (endpointB-endpointA).normalized;
        public float Length => Vector3.Distance(endpointA,endpointB);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetRegistry() { Lines.Clear(); }
        void OnEnable() { if (!Lines.Contains(this)) Lines.Add(this); }
        void OnDisable() { Lines.Remove(this); }
        public Vector3 ClosestPoint(Vector3 position)
        {
            return endpointA+Axis*Mathf.Clamp(Vector3.Dot(position-endpointA,Axis),0,Length);
        }
        public int Direction(Vector3 look, Vector3 movement)
        {
            float alignment = Vector3.Dot(look,Axis);
            if (Mathf.Abs(alignment) < .08f) alignment = Vector3.Dot(movement,Axis);
            // A perpendicular, stationary grab consistently uses A -> B.
            return alignment < 0 ? -1 : 1;
        }
        void OnDrawGizmos() { Gizmos.color = Color.cyan; Gizmos.DrawLine(endpointA,endpointB); }
    }
}
