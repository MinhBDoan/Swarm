#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Swarm
{
    public class GroundedPathSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-grounded-path-test")>=0)
                new GameObject("Grounded path probe").AddComponent<GroundedPathSmokeProbe>();
        }

        IEnumerator Start()
        {
            Application.targetFrameRate=120; yield return null;
            var combat=CombatPlayground.Instance;combat.AutoSpawn=combat.AutoFire=false;combat.Begin();
            var motor=FindObjectOfType<MomentumMotor>();motor.enabled=false;
            var heading=new GameObject("Grounded path heading");motor.view=heading.transform;
            var obj=GameObject.Find("Central cut / continuous floor");var terrain=obj ? obj.GetComponent<MeshCollider>() : null;
            bool passed=terrain;
            var centers=new[] {new Vector3(-115,0,383.18f),new Vector3(-188,0,355.45f),new Vector3(-219,0,350)};
            var approaches=new[] {36f,42f,52f};
            var navPath=new NavMeshPath();
            for(int section=0;section<centers.Length;section++)
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 center=centers[section];
                if(!terrain || !terrain.Raycast(new Ray(center+Vector3.up*35,Vector3.down),out var crest,70))
                {passed=false;Debug.Log($"SWARM_GROUNDED_PATH_TRACE: section={section} missingCrest=true");continue;}
                Vector3 start=center+Vector3.forward*(side*approaches[section]);
                bool foundFloor=Physics.Raycast(start+Vector3.up*35,Vector3.down,out var ground,70,1<<0);
                // Continuous side slopes should provide a visible mid-height surface, not a bare floor under a bridge.
                Vector3 middle=Vector3.Lerp(start,center,.7f);
                bool hasSlope=terrain.Raycast(new Ray(middle+Vector3.up*35,Vector3.down),out var slope,70) && slope.point.y>ground.point.y+.1f && slope.normal.y>.7f;
                bool nav=foundFloor && NavMesh.SamplePosition(ground.point,out var from,1.5f,NavMesh.AllAreas) &&
                    NavMesh.SamplePosition(crest.point,out var to,1.5f,NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,navPath) && navPath.status==NavMeshPathStatus.PathComplete;
                motor.SetSpawn(ground.point+Vector3.up*1.1f);
                Vector3 forward=Vector3.back*side;heading.transform.rotation=Quaternion.LookRotation(forward);
                float highest=motor.transform.position.y;
                for(int step=0;step<480;step++)
                {
                    motor.Simulate(Vector2.up,false,false,1/60f);highest=Mathf.Max(highest,motor.transform.position.y);
                    if(Vector3.Dot(center-motor.transform.position,forward)<.5f)break;
                    if(step%4==0)yield return null;
                }
                bool walk=foundFloor && Mathf.Abs(motor.transform.position.z-center.z)<2 &&
                    motor.transform.position.y>crest.point.y+.7f && motor.transform.position.y<crest.point.y+1.6f;
                passed &= hasSlope && nav && walk;
                Debug.Log($"SWARM_GROUNDED_PATH_TRACE: section={section} side={side} slope={hasSlope} walk={walk} nav={nav} crest={crest.point.y:0.00} highest={highest:0.00} end={motor.transform.position}");
            }
            bool walls=false;
            foreach(var collider in FindObjectsOfType<Collider>())
                if(collider.name.StartsWith("Central cut / rock face"))walls=true;
            passed &= !walls;
            Debug.Log($"SWARM_GROUNDED_PATH_TEST: {(passed ? "PASS" : "FAIL")} noMiddleCliffs={!walls}");
            combat.StopCombat();Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
