#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Swarm
{
    public class WallZipSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-wallzip-test") >= 0)
                new GameObject("Wall Zip Smoke Probe").AddComponent<WallZipSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate = 60; yield return null;
            var motor = FindObjectOfType<MomentumMotor>(); motor.enabled = false;
            var wall = GameObject.Find("Hoist bounce wall").GetComponent<Collider>();
            var line = GameObject.Find("Hoist lift line").GetComponent<MiningZipline>();
            var heading = new GameObject("Test heading"); motor.view = heading.transform;
            wall.Raycast(new Ray(new Vector3(-11,3,22),Vector3.left),out var hit,10);
            Vector3 normal = hit.normal;
            heading.transform.rotation = Quaternion.LookRotation(-normal);
            motor.SetSpawn(hit.point+normal*.6f);
            typeof(MomentumMotor).GetProperty("Velocity").SetValue(motor,-normal*8);
            motor.Simulate(Vector2.zero,false,false,.08f);
            motor.Simulate(Vector2.zero,false,true,.02f);
            bool bounced = Vector3.Dot(motor.Velocity,normal)>8;
            motor.Simulate(Vector2.zero,false,false,.06f);
            bool caught = motor.TryGrabZipline(line,Vector3.up);
            bool clearance = motor.IsZiplining;
            motor.ReleaseZipline(true);
            bool reachedWall = false;
            for (int i=0; i<80; i++)
            {
                motor.Simulate(Vector2.zero,false,false,.02f);
                yield return new WaitForFixedUpdate();
                if (Vector3.Distance(motor.transform.position,wall.ClosestPoint(motor.transform.position))<.6f && Vector3.Dot(motor.Velocity,normal)>-.1f)
                { reachedWall = true; break; }
            }
            motor.Simulate(Vector2.zero,false,true,.02f);
            bool bouncedBack = reachedWall && Vector3.Dot(motor.Velocity,normal)>8;
            motor.Simulate(Vector2.zero,false,false,.06f);
            bool recaught = motor.TryGrabZipline(line,Vector3.up);
            motor.ReleaseZipline(false);
            bool floor = Physics.Raycast(new Vector3(-11,2,22),Vector3.down,out var ground,5,1 << 0) && Mathf.Abs(ground.point.y)<.1f;
            bool passed = bounced && caught && clearance && reachedWall && bouncedBack && recaught && floor;
            Debug.Log($"SWARM_WALLZIP_TEST: {(passed ? "PASS" : "FAIL")} bounce={bounced} catch={caught} clearance={clearance} wallReturn={reachedWall} bounceBack={bouncedBack} recatch={recaught} recoverableFloor={floor}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
