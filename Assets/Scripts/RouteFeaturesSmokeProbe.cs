#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
namespace Swarm
{
    public class RouteFeaturesSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var args=Environment.GetCommandLineArgs();
            if (Array.IndexOf(args,"-swarm-route-features-test")>=0 || Array.IndexOf(args,"-swarm-route-features-capture")>=0)
                new GameObject("Route features probe").AddComponent<RouteFeaturesSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=120; yield return null;
            var combat=CombatPlayground.Instance; combat.AutoSpawn=combat.AutoFire=false; combat.Begin();
            var motor=FindObjectOfType<MomentumMotor>(); motor.enabled=false;
            var heading=new GameObject("Route heading"); motor.view=heading.transform;
            Vector3 across=Vector3.Cross(Vector3.up,CanyonLayout.QuarterPipeAxis),origin=CanyonLayout.QuarterPipePoint(31,0);
            Physics.Raycast(origin+Vector3.up*50,Vector3.down,out var baseHit,100,1<<0);
            motor.SetSpawn(baseHit.point+Vector3.up*1.1f); heading.transform.rotation=Quaternion.LookRotation(across);
            typeof(MomentumMotor).GetProperty("Velocity").SetValue(motor,across*18);
            float highest=motor.transform.position.y; bool returned=false;
            for (int i=0;i<360;i++)
            {
                motor.Simulate(Vector2.zero,true,false,1/60f); highest=Mathf.Max(highest,motor.transform.position.y);
                if (i>45 && highest-baseHit.point.y>2.6f && Vector3.Dot(motor.transform.position-origin,across)<5)
                { returned=true; break; }
                if (i%4==0) yield return null;
            }
            Physics.Raycast(motor.transform.position+Vector3.up*.1f,Vector3.down,out var diagnostic,3,1<<0);
            Debug.Log($"SWARM_QUARTER_SUPPORT_TRACE: distance={diagnostic.distance} normal={diagnostic.normal} grounded={motor.GetComponent<CharacterController>().isGrounded}");
            float side=Vector3.Dot(motor.transform.position-origin,across);
            bool quarter=returned && highest-baseHit.point.y>2.6f && side<10 && motor.transform.position.y> -1;
            Debug.Log($"SWARM_QUARTER_PIPE_TRACE: ride={quarter} rise={highest-baseHit.point.y-1.1f} side={side} velocity={motor.Velocity} position={motor.transform.position}");
            var bank=GameObject.Find("Narrows / banked slide turn").GetComponent<MeshCollider>();
            Vector3 tangent=Vector3.ProjectOnPlane(CanyonLayout.BankPoints[2]-CanyonLayout.BankPoints[0],Vector3.up).normalized;
            Vector3 sample=CanyonLayout.BankPoints[1]-Vector3.Cross(Vector3.up,tangent)*8.5f;
            bool surface=bank.Raycast(new Ray(sample+Vector3.up*30,Vector3.down),out var hit,50) && hit.normal.y>.6f && hit.normal.y<.999f;
            motor.SetSpawn(hit.point+Vector3.up*1.1f); heading.transform.rotation=Quaternion.LookRotation(tangent);
            typeof(MomentumMotor).GetProperty("Velocity").SetValue(motor,tangent*11);
            float peak=0;
            for (int i=0;i<180;i++)
            {
                heading.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(new Vector3(-20,2,420)-motor.transform.position,Vector3.up));
                motor.Simulate(Vector2.up,true,false,1/60f); peak=Mathf.Max(peak,Vector3.ProjectOnPlane(motor.Velocity,Vector3.up).magnitude);
                if (i%4==0) yield return null;
            }
            bool carve=surface && peak>11.5f && motor.transform.position.z>399 && motor.transform.position.y>1;
            Debug.Log($"SWARM_BANK_TRACE: surface={surface} carve={carve} peak={peak} position={motor.transform.position}");
            var zip=GameObject.Find("Narrows shortcut zip").GetComponent<MiningZipline>();
            motor.SetSpawn(zip.endpointA-Vector3.up*1.8f); heading.transform.rotation=Quaternion.LookRotation(zip.Axis);
            bool grab=motor.TryGrabZipline(zip,zip.Axis);
            for (int i=0;i<360 && motor.IsZiplining;i++) { motor.Simulate(Vector2.zero,false,false,1/60f); if (i%4==0) yield return null; }
            bool ride=grab && Vector3.Distance(motor.transform.position,zip.endpointB-Vector3.up*1.1f)<1;
            bool landing=false; heading.transform.rotation=Quaternion.LookRotation(CanyonLayout.NarrowsAxis);
            for (int i=0;i<70;i++)
            {
                motor.Simulate(Vector2.up,false,false,1/60f);
                float along=Vector3.Dot(motor.transform.position-CanyonLayout.JourneyPoints[8],CanyonLayout.NarrowsAxis);
                if (motor.GetComponent<CharacterController>().isGrounded && motor.transform.position.y>7 && along<20) { landing=ride; break; }
                yield return null;
            }
            Debug.Log($"SWARM_NARROWS_ZIP_TRACE: grab={grab} ride={ride} landing={landing} position={motor.transform.position}");
            bool jumps=true;
            for (int pair=0;pair<2;pair++)
            {
                float along=pair==0 ? 13 : 29,height=pair==0 ? 6 : 6.7f;
                motor.SetSpawn(CanyonLayout.NarrowsPoint(along,6,height+1.1f)); heading.transform.rotation=Quaternion.LookRotation(CanyonLayout.NarrowsAxis);
                for (int i=0;i<43;i++) { motor.Simulate(Vector2.up,false,false,1/60f); yield return null; }
                motor.Simulate(Vector2.up,false,true,1/60f);
                for (int i=0;i<65;i++) { motor.Simulate(Vector2.up,false,false,1/60f); yield return null; }
                float distance=Vector3.Dot(motor.transform.position-CanyonLayout.JourneyPoints[8],CanyonLayout.NarrowsAxis);
                bool jump=distance>(pair==0 ? 24 : 40) && motor.transform.position.y>(pair==0 ? 7.3f : 6.5f);
                jumps &= jump; Debug.Log($"SWARM_LEDGE_TRACE: pair={pair} jump={jump} distance={distance} position={motor.transform.position}");
            }
            motor.SetSpawn(CanyonLayout.NarrowsPoint(22,6,8));
            for (int i=0;i<120;i++) { motor.Simulate(Vector2.zero,false,false,1/60f); if (i%4==0) yield return null; }
            bool recovery=motor.transform.position.y>3 && motor.transform.position.y<4.5f;
            var path=new NavMeshPath(); bool nav=true;
            foreach (var point in new[]{CanyonLayout.NarrowsPoint(14,7,6),CanyonLayout.NarrowsPoint(46,7,5.8f)})
                nav &= NavMesh.SamplePosition(point,out var target,1,NavMesh.AllAreas) && NavMesh.CalculatePath(new Vector3(15,3,455),target.position,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete;
            bool passed=quarter && carve && ride && landing && jumps && recovery && nav;
            Debug.Log($"SWARM_ROUTE_FEATURES_TEST: {(passed ? "PASS" : "FAIL")} quarterPipe={quarter} bank={carve} zip={ride} landing={landing} jumps={jumps} recovery={recovery} enemyAccess={nav}");
            var args=Environment.GetCommandLineArgs(); int capture=Array.IndexOf(args,"-swarm-route-features-capture");
            if (capture>=0)
            {
                string folder=args[capture+1]; Directory.CreateDirectory(folder);
                combat.StopCombat(); combat.enabled=false; FindObjectOfType<MultiplayerSession>().enabled=false;
                foreach (var sign in FindObjectsOfType<CanyonLandmark>()) sign.enabled=false;
                var camera=Camera.main; camera.GetComponent<OrbitCamera>().enabled=false;
                Screen.SetResolution(1280,720,false); yield return new WaitForSecondsRealtime(.6f);
                Screen.SetResolution(1600,900,false); yield return new WaitForSecondsRealtime(.6f);
                var views=new[]{new Vector3(7,12,135),new Vector3(-31,13,365),new Vector3(15,12,449),new Vector3(23,8,519)};
                var looks=new[]{new Vector3(22,5,153),new Vector3(-39,2,393),new Vector3(23,5,484),new Vector3(3,6,540)};
                var names=new[]{"route-quarter-pipe.png","route-bank.png","route-ledges.png","route-arch.png"};
                for (int i=0;i<views.Length;i++)
                {
                    camera.transform.position=views[i]; camera.transform.LookAt(looks[i]); camera.fieldOfView=75;
                    yield return new WaitForSecondsRealtime(.3f); ScreenCapture.CaptureScreenshot(Path.Combine(folder,names[i])); yield return new WaitForSecondsRealtime(.5f);
                }
            }
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
