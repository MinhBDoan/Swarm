#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
namespace Swarm
{
    public class JourneySmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var args=Environment.GetCommandLineArgs();
            if (Array.IndexOf(args,"-swarm-journey-test")>=0 || Array.IndexOf(args,"-swarm-journey-capture")>=0)
                new GameObject("Journey probe").AddComponent<JourneySmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=120; yield return null;
            var combat=CombatPlayground.Instance; combat.AutoSpawn=combat.AutoFire=false; combat.Begin();
            var motor=FindObjectOfType<MomentumMotor>(); motor.enabled=false;
            var heading=new GameObject("Journey heading"); motor.view=heading.transform;
            var drill=DrillHazard.Instance; drill.enabled=false; drill.AcceptState(0,true);
            var path=new NavMeshPath();
            bool nav=NavMesh.CalculatePath(new Vector3(0,0,-15),CanyonLayout.StationPoint(0,8,-28),NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete;
            var safe=new[]{new Vector3(0,6,110),new Vector3(0,6,125),new Vector3(18,-2,185),new Vector3(18,-2,240),new Vector3(-17,-2,300),new Vector3(-17,-2,333),new Vector3(-25,-2,345),new Vector3(-40,1,390),new Vector3(15,3,455),new Vector3(25,2,510),new Vector3(-15,0,565),new Vector3(0,0,600),CanyonLayout.StationPoint(0,8,-31)};
            motor.SetSpawn(new Vector3(0,1.1f,77)); int target=0,steps=0;
            for (;steps<4800 && target<safe.Length;steps++)
            {
                Vector3 direction=Vector3.ProjectOnPlane(safe[target]-motor.transform.position,Vector3.up);
                if (direction.magnitude<1.4f) { target++; continue; }
                heading.transform.rotation=Quaternion.LookRotation(direction);
                motor.Simulate(Vector2.up,false,false,1/60f);
                if (steps%4==0) yield return null;
            }
            float seconds=steps/60f;
            bool walk=target==safe.Length && motor.transform.position.y>8.5f && seconds>=45 && seconds<70;
            Debug.Log($"SWARM_JOURNEY_TRACE: navigation={nav} walk={walk} travelSeconds={seconds} waypoint={target} position={motor.transform.position}");
            motor.SetSpawn(new Vector3(0,7.1f,123)); float peak=0;
            for (int i=0;i<300;i++)
            {
                heading.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(new Vector3(28,-2,185)-motor.transform.position,Vector3.up));
                motor.Simulate(Vector2.up,i>25,false,1/60f); peak=Mathf.Max(peak,Vector3.ProjectOnPlane(motor.Velocity,Vector3.up).magnitude);
                if (motor.transform.position.z>180) break;
                if (i%4==0) yield return null;
            }
            bool downhill=peak>14;
            Vector3 axis=(CanyonLayout.BridgeEnd-CanyonLayout.BridgeStart).normalized,mid=(CanyonLayout.BridgeEnd+CanyonLayout.BridgeStart)*.5f;
            heading.transform.rotation=Quaternion.LookRotation(axis); motor.SetSpawn(mid-axis*10+Vector3.up*1.1f);
            for (int i=0;i<40;i++) { motor.Simulate(Vector2.up,false,false,1/60f); yield return null; }
            motor.Simulate(Vector2.up,false,true,1/60f);
            for (int i=0;i<70;i++) { motor.Simulate(Vector2.up,false,false,1/60f); yield return null; }
            bool jump=Vector3.Dot(motor.transform.position-mid,axis)>CanyonLayout.BridgeGap*.5f && motor.transform.position.y>8.5f;
            motor.SetSpawn(mid+Vector3.up*1.1f);
            for (int i=0;i<100;i++) { motor.Simulate(Vector2.zero,false,false,1/60f); yield return null; }
            bool recovery=motor.transform.position.y> -1.3f && motor.transform.position.y<0;
            var zip=GameObject.Find("Bridge transfer zip").GetComponent<MiningZipline>();
            motor.SetSpawn(zip.endpointA+zip.Axis*3-Vector3.up*1.9f);
            bool grab=motor.TryGrabZipline(zip,zip.Axis);
            for (int i=0;i<360 && motor.IsZiplining;i++) { motor.Simulate(Vector2.zero,false,false,1/60f); if (i%4==0) yield return null; }
            Debug.Log($"SWARM_JOURNEY_ZIP_TRACE: grab={grab} position={motor.transform.position} end={zip.endpointB}");
            bool transfer=grab && Vector3.Distance(motor.transform.position,zip.endpointB-Vector3.up*1.1f)<1;
            Vector3 lookout=new Vector3(25,4,510),reveal=CanyonLayout.StationPoint(0,10,-32),beacon=CanyonLayout.StationPoint(0,29,0);
            bool hidden=Physics.Linecast(lookout,beacon,out var hiddenHit,1<<0) && hiddenHit.collider.name=="Canyon rock face";
            bool visible=!Physics.Linecast(reveal,beacon,out var revealHit,1<<0) || revealHit.collider.name!="Canyon rock face";
            bool passed=nav && walk && downhill && jump && recovery && transfer && hidden && visible;
            Debug.Log($"SWARM_JOURNEY_TEST: {(passed ? "PASS" : "FAIL")} nav={nav} safeWalk={walk} seconds={seconds} downhill={downhill} peak={peak} bridgeJump={jump} recovery={recovery} zipTransfer={transfer} hidden={hidden} reveal={visible}");
            var args=Environment.GetCommandLineArgs(); int capture=Array.IndexOf(args,"-swarm-journey-capture");
            if (capture>=0)
            {
                string folder=args[capture+1]; Directory.CreateDirectory(folder);
                combat.StopCombat(); combat.enabled=false; FindObjectOfType<MultiplayerSession>().enabled=false;
                foreach (var landmark in FindObjectsOfType<CanyonLandmark>()) landmark.enabled=false;
                motor.SetSpawn(new Vector3(20,-.9f,220));
                var camera=Camera.main; camera.GetComponent<OrbitCamera>().enabled=false; camera.farClipPlane=1500;
                Screen.SetResolution(1280,720,false); yield return new WaitForSecondsRealtime(.7f);
                Screen.SetResolution(1600,900,false); yield return new WaitForSecondsRealtime(.7f);
                var views=new[]{new Vector3(250,440,250),new Vector3(24,17,221),new Vector3(0,14,610)};
                var looks=new[]{new Vector3(0,0,360),new Vector3(-10,7,275),CanyonLayout.StationPoint(0,10,0)};
                var names=new[]{"journey-overview.png","journey-bridge.png","journey-reveal.png"};
                bool fog=RenderSettings.fog;
                for (int i=0;i<views.Length;i++)
                {
                    RenderSettings.fog=i==0 ? false : fog;
                    camera.transform.position=views[i]; camera.transform.LookAt(looks[i]); camera.fieldOfView=i==0 ? 75 : 70;
                    yield return new WaitForSecondsRealtime(.3f); ScreenCapture.CaptureScreenshot(Path.Combine(folder,names[i])); yield return new WaitForSecondsRealtime(.5f);
                }
            }
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
