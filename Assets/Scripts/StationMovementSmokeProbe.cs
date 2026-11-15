#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
namespace Swarm
{
    public class StationMovementSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-station-movement-test")>=0)
                new GameObject("Station movement probe").AddComponent<StationMovementSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60; yield return null;
            var motor=FindObjectOfType<MomentumMotor>(); motor.enabled=false;
            var heading=new GameObject("Station heading"); motor.view=heading.transform;
            var drill=DrillHazard.Instance; drill.enabled=false; drill.AcceptState(0,true);
            var line=GameObject.Find("Station escape zip").GetComponent<MiningZipline>();
            var wall=GameObject.Find("Station zip bounce wall").GetComponent<Collider>();
            wall.Raycast(new Ray(CanyonLayout.StationPoint(10,7,13),Vector3.left),out var hit,5);
            motor.SetSpawn(hit.point+hit.normal*.6f); heading.transform.rotation=Quaternion.LookRotation(-hit.normal);
            typeof(MomentumMotor).GetProperty("Velocity").SetValue(motor,-hit.normal*8);
            motor.Simulate(Vector2.zero,false,false,.08f); motor.Simulate(Vector2.zero,false,true,.02f);
            bool bounce=Vector3.Dot(motor.Velocity,hit.normal)>8;
            motor.Simulate(Vector2.zero,false,false,.06f);
            bool grab=motor.TryGrabZipline(line,line.Axis);
            for (int i=0;i<180 && motor.IsZiplining;i++) { motor.Simulate(Vector2.zero,false,false,1/60f); yield return null; }
            bool ride=motor.transform.position.z>CanyonLayout.StationCenter.z+38;
            heading.transform.rotation=Quaternion.identity;
            for (int i=0;i<55;i++) { motor.Simulate(Vector2.zero,false,false,1/60f); yield return null; }
            bool land=motor.transform.position.z>CanyonLayout.StationCenter.z+34 && motor.transform.position.y>12.5f;
            // Midline jump retains travel momentum; a drop adds no jump impulse.
            motor.SetSpawn(line.endpointA+line.Axis*7);
            bool jumpGrab=motor.TryGrabZipline(line,line.Axis); Vector3 before=motor.Velocity;
            motor.ReleaseZipline(true); bool jump=jumpGrab && Vector3.Dot(motor.Velocity,line.Axis)>=Vector3.Dot(before,line.Axis);
            yield return new WaitForSeconds(.2f);
            motor.SetSpawn(line.endpointA+line.Axis*7);
            bool dropGrab=motor.TryGrabZipline(line,-line.Axis); before=motor.Velocity;
            motor.ReleaseZipline(false); bool drop=dropGrab && (motor.Velocity-before).sqrMagnitude<.001f && !motor.IsZiplining;
            motor.SetSpawn(CanyonLayout.StationPoint(-10,13.1f,37)); heading.transform.rotation=Quaternion.Euler(0,180,0);
            float peak=0;
            for (int i=0;i<220;i++)
            {
                motor.Simulate(Vector2.up,i>15,false,1/60f); peak=Mathf.Max(peak,Vector3.ProjectOnPlane(motor.Velocity,Vector3.up).magnitude);
                if (motor.transform.position.z<CanyonLayout.StationCenter.z+6) break;
                yield return null;
            }
            bool slide=motor.transform.position.z<CanyonLayout.StationCenter.z+6 && peak>14;
            motor.SetSpawn(CanyonLayout.StationPoint(23,13.1f,36)); heading.transform.rotation=Quaternion.Euler(0,90,0);
            for (int i=0;i<100;i++) { motor.Simulate(Vector2.up,false,false,1/60f); yield return null; }
            Debug.Log($"SWARM_OVERLOOK_WALK_TRACE: position={motor.transform.position}");
            bool overlookWalk=motor.transform.position.x>33 && motor.transform.position.y>14;
            motor.SetSpawn(CanyonLayout.StationPoint(20,13.1f,44));
            for (int i=0;i<42;i++) { motor.Simulate(Vector2.up,false,false,1/60f); yield return null; }
            motor.Simulate(Vector2.up,false,true,1/60f);
            for (int i=0;i<75;i++) { motor.Simulate(Vector2.up,false,false,1/60f); yield return null; }
            bool overlookJump=motor.transform.position.x>31 && motor.transform.position.y>14;
            Debug.Log($"SWARM_OVERLOOK_TEST: walk={overlookWalk} gapJump={overlookJump}");
            bool passed=overlookWalk && overlookJump && bounce && grab && ride && land && jump && drop && slide;
            Debug.Log($"SWARM_STATION_MOVEMENT_TEST: {(passed ? "PASS" : "FAIL")} bounce={bounce} grab={grab} ride={ride} land={land} jump={jump} drop={drop} returnSlide={slide} peak={peak} position={motor.transform.position}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
