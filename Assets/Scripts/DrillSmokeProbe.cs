#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
namespace Swarm
{
    public class DrillSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var args=Environment.GetCommandLineArgs();
            if (Array.IndexOf(args,"-swarm-drill-test")>=0 || Array.IndexOf(args,"-swarm-drill-capture")>=0)
                new GameObject("Drill bowl probe").AddComponent<DrillSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60; yield return null;
            var combat=CombatPlayground.Instance; combat.AutoSpawn=combat.AutoFire=false; combat.Begin();
            var motor=FindObjectOfType<MomentumMotor>(); motor.enabled=false;
            var drill=DrillHazard.Instance; drill.enabled=false; drill.AcceptState(0,false);
            motor.SetSpawn(CanyonLayout.StationPoint(0,9.1f,-28));
            float peak=0; bool crossed=false;
            for (int i=0;i<390;i++)
            {
                motor.Simulate(Vector2.up,i>35,false,1/60f); peak=Mathf.Max(peak,motor.Velocity.magnitude);
                if (motor.transform.position.z>CanyonLayout.StationCenter.z+24 && motor.transform.position.y>8.5f) { crossed=true; break; }
                yield return null;
            }
            Debug.Log($"SWARM_DRILL_TRACE: crossing={crossed} peak={peak} position={motor.transform.position}");
            bool rim=true;
            for (int i=0;i<16;i++)
            {
                float a=-Mathf.PI/2+i*Mathf.PI/16,b=a+Mathf.PI/16;
                Vector3 from=drill.Center+new Vector3(Mathf.Cos(a)*28,8,Mathf.Sin(a)*28),to=drill.Center+new Vector3(Mathf.Cos(b)*28,8,Mathf.Sin(b)*28);
                var path=new NavMeshPath();
                rim &= NavMesh.SamplePosition(from,out var f,1,NavMesh.AllAreas) && NavMesh.SamplePosition(to,out var t,1,NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(f.position,t.position,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete;
                foreach (var corner in path.corners) rim &= Vector3.ProjectOnPlane(corner-drill.Center,Vector3.up).magnitude>23;
            }
            var accessPath=new NavMeshPath();
            bool access=NavMesh.CalculatePath(new Vector3(0,0,-15),CanyonLayout.StationPoint(0,8,-28),NavMesh.AllAreas,accessPath) && accessPath.status==NavMeshPathStatus.PathComplete;
            bool station=true;
            foreach (var point in new[] {CanyonLayout.StationPoint(-30,8,0),CanyonLayout.StationPoint(-20,12,37),CanyonLayout.StationPoint(20,12,37),CanyonLayout.StationPoint(0,12,42)})
            {
                var path=new NavMeshPath();
                station &= NavMesh.SamplePosition(point,out var target,1,NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(CanyonLayout.StationPoint(0,8,-28),target.position,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete;
            }
            // Walk the safe west approach and the direct north exit with the real motor.
            var heading=new GameObject("Station probe heading"); motor.view=heading.transform;
            bool deckWalk=true;
            foreach (float x in new[] {-20f,0f,20f})
            {
                motor.SetSpawn(CanyonLayout.StationPoint(x,9.1f,x==0 ? 26 : 18));
                for (int i=0;i<150;i++) { motor.Simulate(Vector2.up,false,false,1/60f); yield return null; }
                deckWalk &= motor.transform.position.z>CanyonLayout.StationCenter.z+37 && motor.transform.position.y>12.5f;
            }
            Debug.Log($"SWARM_STATION_TEST: navigation={station} deckWalk={deckWalk}");
            motor.SetSpawn(drill.Center+Vector3.up*1.1f); drill.AcceptState(DrillHazard.RaisedSeconds+DrillHazard.WarningSeconds+.35f,false); drill.enabled=true;
            float deadline=Time.realtimeSinceStartup+2;
            while (combat.Health==100 && Time.realtimeSinceStartup<deadline) yield return null;
            bool damage=combat.Health==70; float y=motor.transform.position.y;
            motor.Simulate(Vector2.zero,false,false,1/60f); bool knock=motor.Velocity.y>0 && motor.transform.position.y>y;
            motor.SetSpawn(drill.Center+Vector3.up*1.1f);
            yield return new WaitForSecondsRealtime(.4f);
            bool once=combat.Health==70;
            bool safe=!drill.TryStrike(-123,drill.Center+new Vector3(28,9,0),1,.45f);
            bool spawned=combat.SpawnEnemy(drill.Center+Vector3.right*.5f);
            drill.AcceptState(DrillHazard.CycleSeconds+DrillHazard.RaisedSeconds+DrillHazard.WarningSeconds+.5f,false);
            deadline=Time.realtimeSinceStartup+1;
            while (combat.DrillEnemyHits==0 && Time.realtimeSinceStartup<deadline) yield return null;
            bool enemy=spawned && combat.DrillEnemyHits==1 && combat.Kills==1;
            bool passed=station && deckWalk && crossed && peak>17 && rim && access && damage && knock && once && safe && enemy;
            Debug.Log($"SWARM_DRILL_TEST: {(passed ? "PASS" : "FAIL")} crossing={crossed} rim={rim} access={access} damage={damage} knockback={knock} once={once} safeRim={safe} enemy={enemy}");
            var args=Environment.GetCommandLineArgs(); int capture=Array.IndexOf(args,"-swarm-drill-capture");
            if (capture>=0)
            {
                string folder=args[capture+1]; Directory.CreateDirectory(folder);
                motor.SetSpawn(CanyonLayout.StationPoint(0,9.1f,-28));
                var camera=Camera.main; camera.GetComponent<OrbitCamera>().enabled=false;
                camera.transform.position=CanyonLayout.StationPoint(63,48,-34); camera.transform.LookAt(drill.Center+new Vector3(0,5,15)); camera.fieldOfView=65;
                drill.enabled=false; drill.AcceptState(5.4f,true);
                Screen.SetResolution(1280,720,false); yield return new WaitForSecondsRealtime(1);
                Screen.SetResolution(1600,900,false); yield return new WaitForSecondsRealtime(1);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,"drill-warning.png")); yield return new WaitForSecondsRealtime(.5f);
                drill.AcceptState(7.2f,true); yield return new WaitForSecondsRealtime(.5f);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,"drill-down.png")); yield return new WaitForSecondsRealtime(1);
            }
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
