#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Swarm
{
    public class PerchSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-perch-test") >= 0)
                new GameObject("Perch Smoke Probe").AddComponent<PerchSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate = 60; yield return null;
            var combat = CombatPlayground.Instance;
            combat.AutoSpawn = combat.AutoFire = false; combat.Begin();
            var motor = FindObjectOfType<MomentumMotor>(); motor.enabled = false;
            var heading = new GameObject("Test heading"); motor.view = heading.transform;
            var lift = GameObject.Find("Hoist lift line").GetComponent<MiningZipline>();
            var crossing = GameObject.Find("Basin crossing line").GetComponent<MiningZipline>();
            heading.transform.rotation = Quaternion.LookRotation(new Vector3(1,0,-1));
            motor.SetSpawn(new Vector3(-11.7f,9.8f,22));
            bool launch = motor.TryGrabZipline(lift,Vector3.up); motor.ReleaseZipline(true);
            for (int i=0; i<43; i++)
            { motor.Simulate(Vector2.up,false,false,.02f); yield return new WaitForFixedUpdate(); }
            Vector3 landed = motor.transform.position;
            bool landing = launch && landed.x > -10 && landed.x < -4 && landed.z > 15 && landed.z < 21 && Mathf.Abs(landed.y-11.08f)<.3f;
            Debug.Log($"SWARM_PERCH_TRACE: landing={landing} position={landed}");

            motor.SetSpawn(new Vector3(-9,11.1f,20.5f));
            heading.transform.rotation = Quaternion.LookRotation(new Vector3(-1,0,1));
            motor.Simulate(Vector2.zero,false,false,.02f);
            motor.Simulate(Vector2.zero,false,true,.02f);
            bool transfer = false;
            for (int i=0; i<80; i++)
            {
                motor.Simulate(Vector2.up,false,false,.02f);
                if (motor.TryGrabZipline(crossing,crossing.Axis)) { transfer = true; break; }
                yield return new WaitForFixedUpdate();
            }
            Debug.Log($"SWARM_PERCH_TRACE: transfer={transfer} position={motor.transform.position}");

            motor.SetSpawn(new Vector3(-7,11.1f,18)); heading.transform.rotation = Quaternion.Euler(0,180,0);
            for (int i=0; i<10; i++)
            { motor.Simulate(Vector2.up,false,false,.02f); yield return new WaitForFixedUpdate(); }
            bool slide = false;
            for (int i=0; i<200; i++)
            {
                motor.Simulate(Vector2.up,true,false,.02f);
                if (motor.transform.position.z < -10.5f && motor.transform.position.y < 2)
                { slide = motor.Velocity.z < -12; break; }
                yield return new WaitForFixedUpdate();
            }
            var path = new NavMeshPath();
            bool nav = NavMesh.SamplePosition(new Vector3(-7,10,18),out var target,2,NavMesh.AllAreas) &&
                NavMesh.SamplePosition(new Vector3(0,0,-15),out var source,2,NavMesh.AllAreas) &&
                NavMesh.CalculatePath(source.position,target.position,NavMesh.AllAreas,path) && path.status == NavMeshPathStatus.PathComplete;
            bool passed = landing && transfer && slide && nav;
            Debug.Log($"SWARM_PERCH_TEST: {(passed ? "PASS" : "FAIL")} liftLanding={landing} zipTransfer={transfer} returnSlide={slide} enemyAccess={nav} position={motor.transform.position}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
