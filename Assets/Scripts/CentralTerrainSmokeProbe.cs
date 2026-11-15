#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Swarm
{
    // Exercises the authored surfaces with the real controller and runtime navmesh.
    public class CentralTerrainSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-central-terrain-test")>=0)
                new GameObject("Central terrain probe").AddComponent<CentralTerrainSmokeProbe>();
        }

        IEnumerator Start()
        {
            Application.targetFrameRate=120;
            yield return null;
            var combat=CombatPlayground.Instance;
            combat.AutoSpawn=combat.AutoFire=false;
            combat.Begin();
            var motor=FindObjectOfType<MomentumMotor>();
            motor.enabled=false;
            var heading=new GameObject("Terrain probe heading");
            motor.view=heading.transform;
            bool passed=true;
            var centers=new[] {new Vector3(-150,0,240),new Vector3(-152,0,322),new Vector3(-158,0,485)};
            var names=new[] {"South crossing / rolling ridge","Middle junction / sweeping bank","North shelf / rolling approach"};
            var yaws=new[] {15f,-18f,0f};
            var halfLengths=new[] {34f,29f,31f};
            var crests=new Vector3[3];
            for (int patch=0;patch<centers.Length;patch++)
            {
                var obj=GameObject.Find(names[patch]);
                var collider=obj ? obj.GetComponent<MeshCollider>() : null;
                Quaternion rotation=Quaternion.Euler(0,yaws[patch],0);
                Vector3 forward=rotation*Vector3.forward;
                bool surface=collider && collider.Raycast(new Ray(centers[patch]+Vector3.up*30,Vector3.down),out _,50);
                if (!surface)
                {
                    passed=false;
                    Debug.Log($"SWARM_CENTRAL_TERRAIN_TRACE: patch={patch} missingSurface=true");
                    continue;
                }
                Vector3 start=centers[patch]-forward*(halfLengths[patch]+2);
                Physics.Raycast(start+Vector3.up*30,Vector3.down,out var startHit,50,1<<0);
                Physics.Raycast(centers[patch]+Vector3.up*30,Vector3.down,out var crestHit,50,1<<0);
                crests[patch]=crestHit.point;
                // Ordinary movement must cross the fold without a jump or a special item.
                motor.SetSpawn(startHit.point+Vector3.up*1.1f);
                heading.transform.rotation=Quaternion.LookRotation(forward);
                float highest=motor.transform.position.y;
                for (int i=0;i<480;i++)
                {
                    motor.Simulate(Vector2.up,false,false,1/60f);
                    highest=Mathf.Max(highest,motor.transform.position.y);
                    if (Vector3.Dot(motor.transform.position-centers[patch],forward)>halfLengths[patch]+1) break;
                    if (i%4==0) yield return null;
                }
                float along=Vector3.Dot(motor.transform.position-centers[patch],forward);
                bool walked=along>halfLengths[patch] && highest>crestHit.point.y+.7f;
                // The downhill half must reward momentum; no movement input supplies acceleration.
                Vector3 downhillStart=centers[patch]+forward*5;
                Physics.Raycast(downhillStart+Vector3.up*30,Vector3.down,out var downHit,50,1<<0);
                motor.SetSpawn(downHit.point+Vector3.up*1.1f);
                typeof(MomentumMotor).GetProperty("Velocity").SetValue(motor,forward*7);
                float peak=0;
                for (int i=0;i<180;i++)
                {
                    motor.Simulate(Vector2.zero,true,false,1/60f);
                    peak=Mathf.Max(peak,Vector3.ProjectOnPlane(motor.Velocity,Vector3.up).magnitude);
                    if (i%4==0) yield return null;
                }
                bool slide=peak>8.5f && Vector3.Dot(motor.transform.position-downhillStart,forward)>12 && motor.transform.position.y> -10;
                passed &= walked && slide;
                Debug.Log($"SWARM_CENTRAL_TERRAIN_TRACE: patch={patch} walk={walked} along={along:0.0} rise={highest-startHit.point.y-1.1f:0.0} slide={slide} peak={peak:0.0}");
            }
            // Enemies need a complete path onto all crests, not just the old floor beneath them.
            var path=new NavMeshPath();
            bool nav=NavMesh.SamplePosition(new Vector3(-118,-1,120),out var entry,4,NavMesh.AllAreas);
            foreach (var crest in crests)
                nav &= NavMesh.SamplePosition(crest,out var target,1.5f,NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(entry.position,target.position,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete;
            passed &= nav;
            Debug.Log($"SWARM_CENTRAL_TERRAIN_TEST: {(passed ? "PASS" : "FAIL")} enemyAccess={nav}");
            combat.StopCombat();
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
