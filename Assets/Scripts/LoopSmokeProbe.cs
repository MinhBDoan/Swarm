#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
namespace Swarm
{
    public class LoopSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-loop-test")>=0)
                new GameObject("Loop probe").AddComponent<LoopSmokeProbe>();
        }
        MomentumMotor motor; Transform heading; bool passed=true;
        IEnumerator Walk(string name,Vector3[] route,bool reverse)
        {
            Vector3[] points=(Vector3[])route.Clone(); if (reverse) Array.Reverse(points);
            motor.SetSpawn(points[0]+Vector3.up*1.1f); int target=1,steps=0;
            for (;steps<6000 && target<points.Length;steps++)
            {
                Vector3 delta=Vector3.ProjectOnPlane(points[target]-motor.transform.position,Vector3.up);
                if (delta.magnitude<1.3f) { target++; continue; }
                heading.rotation=Quaternion.LookRotation(delta); motor.Simulate(Vector2.up,false,false,1/60f);
                if (steps%8==0) yield return null;
            }
            bool ok=target==points.Length && Mathf.Abs(motor.transform.position.y-points[points.Length-1].y-1)<.6f;
            passed&=ok;
            Debug.Log($"SWARM_LOOP_WALK: {name} reverse={reverse} pass={ok} seconds={steps/60f} waypoint={target} position={motor.transform.position}");
        }
        bool Nav(Vector3 from,Vector3 to)
        {
            if (!NavMesh.SamplePosition(from,out var a,3,NavMesh.AllAreas) || !NavMesh.SamplePosition(to,out var b,3,NavMesh.AllAreas)) return false;
            var path=new NavMeshPath(); return NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete;
        }
        bool WallsMeetFloor(string route)
        {
            var floor=GameObject.Find(route+" / continuous floor").GetComponent<MeshCollider>();
            Vector3[] edge=floor.sharedMesh.vertices;
            bool covered=true;
            for (int segment=0;segment<edge.Length/2-1;segment++)
            for (int side=0;side<2;side++)
            {
                int start=segment*2+side;
                Vector3 a=edge[start], b=edge[start+2];
                for (float distance=1;distance<Vector3.Distance(a,b);distance+=2)
                {
                    // The central cut opens onto the existing canyon at its east mouth.
                    if (route=="Central cut" && segment==0 && distance<12) continue;
                    float t=distance/Vector3.Distance(a,b);
                    Vector3 boundary=Vector3.Lerp(a,b,t);
                    Vector3 opposite=Vector3.Lerp(edge[start^1],edge[(start+2)^1],t);
                    Vector3 outward=(boundary-opposite).normalized;
                    Vector3 origin=boundary-outward*3+Vector3.up*.65f;
                    if (Physics.Raycast(origin,outward,6,1<<0)) continue;
                    // Openings onto the flats are safe if solid ground continues beyond the old edge.
                    if (Physics.Raycast(boundary+outward*3+Vector3.up,Vector3.down,7,1<<0)) continue;
                    covered=false;
                    Debug.Log($"SWARM_LOOP_EDGE_GAP: {route} {boundary}");
                }
            }
            return covered;
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=120; yield return null;
            var combat=CombatPlayground.Instance; combat.AutoSpawn=combat.AutoFire=false; combat.Begin();
            motor=FindObjectOfType<MomentumMotor>(); motor.enabled=false; heading=new GameObject("Loop heading").transform; motor.view=heading;
            foreach (var route in new[]{CanyonLayout.StationPumpRoute,CanyonLayout.PumpBasinRoute,CanyonLayout.MiddleRoute})
            { yield return Walk("connection",route,false); yield return Walk("connection",route,true); }
            Vector3 c=CanyonLayout.PumpCenter;
            yield return Walk("yard north-south",new[]{c+new Vector3(0,0,-48),c+new Vector3(6,0,-20),c+new Vector3(6,0,20),c+new Vector3(0,0,48)},false);
            yield return Walk("deck ascent/return",new[]{c+new Vector3(-12,0,-33),c+new Vector3(-12,9,-5),c+new Vector3(-12,9,13),c+new Vector3(-12,0,44)},false);
            // Include transitions beyond corridor endpoints, not just isolated floor strips.
            yield return Walk("basin mouth",new[]{new Vector3(-40,0,20),new Vector3(-12,0,20)},false);
            yield return Walk("shelter mouth",new[]{new Vector3(-70,-.8f,390),new Vector3(-40,1,390),new Vector3(-40,1,380)},false);
            yield return Walk("station mouth",new[]{new Vector3(-65,.8f,650),new Vector3(-44,0,650)},false);
            var south = new Vector3(-117.5f,0,60);
            var north = new Vector3(-80,1.39f,650);
            var flats = new Vector3(-145,-4,375);
            var flatsRoute = new[] { south,new Vector3(-118,-.625f,105),new Vector3(-130,-3,220),
                flats,new Vector3(-123,-3,510),new Vector3(-86,0,590),north };
            yield return Walk("flats northbound",flatsRoute,false);
            yield return Walk("flats southbound",flatsRoute,true);
            yield return Walk("flats to pumping yard",new[] { flats,new Vector3(-200,0,350),new Vector3(-256,6,350) },false);
            yield return Walk("flats to narrows",new[] { flats,new Vector3(-90,-2,390),new Vector3(-40,1,390) },false);
            foreach (string name in new[]{"Pumping yard / deck transfer zip","Western ridge / ridge transfer zip","Flats / shelf transfer zip"})
            {
                var zip=GameObject.Find(name).GetComponent<MiningZipline>();
                motor.SetSpawn(zip.endpointA+zip.Axis*2-Vector3.up*1.9f);
                bool grab=motor.TryGrabZipline(zip,zip.Axis);
                for (int i=0;i<500 && motor.IsZiplining;i++) { motor.Simulate(Vector2.zero,false,false,1/60f); if (i%8==0) yield return null; }
                bool arrived=grab && Vector3.Distance(motor.transform.position,zip.endpointB-Vector3.up*1.1f)<1;
                for (int i=0;i<120;i++) motor.Simulate(Vector2.zero,false,false,1/60f);
                bool landed=Physics.Raycast(motor.transform.position,Vector3.down,2,1<<0);
                passed&=arrived && landed;
                Debug.Log($"SWARM_LOOP_ZIP: {name} arrived={arrived} landed={landed} position={motor.transform.position}");
            }
            motor.SetSpawn(new Vector3(-204,.6f,271));
            heading.rotation=Quaternion.LookRotation(new Vector3(.09f,0,1));
            for (int i=0;i<39;i++) motor.Simulate(Vector2.up,false,false,1/60f);
            motor.Simulate(Vector2.up,false,true,1/60f);
            for (int i=0;i<68;i++) motor.Simulate(Vector2.up,false,false,1/60f);
            bool terraceJump=motor.transform.position.z>285 && motor.transform.position.y>.5f;
            motor.SetSpawn(new Vector3(-203,.7f,283));
            for (int i=0;i<120;i++) motor.Simulate(Vector2.zero,false,false,1/60f);
            bool terraceRecovery=motor.transform.position.y> -3.2f && motor.transform.position.y< -2.4f;
            passed &= terraceJump && terraceRecovery;
            Debug.Log($"SWARM_FLATS_TERRACE: jump={terraceJump} safeDrop={terraceRecovery}");
            bool nav=Nav(new Vector3(0,0,-15),c+new Vector3(6,0,0)) && Nav(c+new Vector3(6,0,0),new Vector3(-44,0,650)) && Nav(new Vector3(-40,1,390),c+new Vector3(6,0,0));
            motor.SetSpawn(c+new Vector3(6,1.1f,0)); bool enemies=combat.SpawnEnemy(c+new Vector3(0,0,25));
            bool labels=CanyonLayout.Area(c).Contains("PUMPING YARD") && CanyonLayout.Area(flats)=="CENTRAL FLATS";
            nav &= Nav(flats,south) && Nav(flats,north) && Nav(flats,CanyonLayout.BossConsole);
            bool seams=WallsMeetFloor("Western ridge") & WallsMeetFloor("Return gully") & WallsMeetFloor("Central cut");
            passed&=nav && enemies && labels && seams;
            Debug.Log($"SWARM_LOOP_TEST: {(passed ? "PASS" : "FAIL")} navigation={nav} enemySpawn={enemies} areaLabels={labels} sealedEdges={seams}");
            var args=Environment.GetCommandLineArgs(); int capture=Array.IndexOf(args,"-swarm-loop-capture");
            if (capture>=0)
            {
                string folder=args[capture+1]; Directory.CreateDirectory(folder); combat.StopCombat(); combat.enabled=false;
                FindObjectOfType<MultiplayerSession>().enabled=false; foreach (var l in FindObjectsOfType<CanyonLandmark>()) l.enabled=false;
                var camera=Camera.main; camera.GetComponent<OrbitCamera>().enabled=false; camera.farClipPlane=2000;
                Screen.SetResolution(1280,720,false); yield return new WaitForSecondsRealtime(.7f); Screen.SetResolution(1600,900,false); yield return new WaitForSecondsRealtime(.7f);
                var views=new[]{new Vector3(300,740,-100),c+new Vector3(30,36,-38),new Vector3(-105,7,390),new Vector3(-147,60,199)};
                var looks=new[]{new Vector3(-140,0,340),c+new Vector3(0,6,0),new Vector3(-200,3,350),new Vector3(-147,-4,435)};
                var names=new[]{"loop-overview.png","pumping-yard.png","central-shortcut.png","central-flats.png"}; bool fog=RenderSettings.fog;
                for (int i=0;i<views.Length;i++)
                { RenderSettings.fog=i==0 ? false : fog; camera.orthographic=i==0; camera.orthographicSize=340; camera.transform.position=views[i]; camera.transform.LookAt(looks[i]); camera.fieldOfView=75;
                    yield return new WaitForSecondsRealtime(.3f); ScreenCapture.CaptureScreenshot(Path.Combine(folder,names[i])); yield return new WaitForSecondsRealtime(.5f); }
            }
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
