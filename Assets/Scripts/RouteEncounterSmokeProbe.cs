#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using System.IO;
using UnityEngine;
namespace Swarm
{
    public class RouteEncounterSmokeProbe : MonoBehaviour
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-route-encounter-test")>=0)
                new GameObject("Route encounter probe").AddComponent<RouteEncounterSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=120; yield return null;
            var combat=CombatPlayground.Instance; combat.AutoSpawn=false; combat.AutoFire=true; combat.Begin();
            var motor=FindObjectOfType<MomentumMotor>(); motor.enabled=false;
            typeof(CombatPlayground).GetField("spawnClock",Flags).SetValue(combat,10000f);
            typeof(CombatPlayground).GetProperty("Damage").SetValue(combat,500f);
            typeof(CombatPlayground).GetProperty("FireInterval").SetValue(combat,.03f);
            var args=Environment.GetCommandLineArgs(); int capture=Array.IndexOf(args,"-swarm-route-encounter-capture"); bool captured=false;
            if (capture>=0)
            {
                combat.AutoFire=false;
                Screen.SetResolution(1280,720,false); yield return new WaitForSecondsRealtime(.6f);
                Screen.SetResolution(1600,900,false); yield return new WaitForSecondsRealtime(.6f);
            }
            motor.SetSpawn(new Vector3(0,1.1f,-15)); combat.AutoSpawn=true;
            yield return new WaitForSecondsRealtime(.15f);
            bool outside=combat.RouteEnemiesSpawned==0;
            motor.SetSpawn(CombatPlayground.RouteTriggers[0]+Vector3.left*10+Vector3.up*1.1f);
            yield return new WaitForSecondsRealtime(.1f);
            bool warning=combat.RouteEncounterStage(0)==CombatPlayground.RouteStage.Warning && combat.RouteEnemiesSpawned==0;
            typeof(CombatPlayground).GetProperty("ChoosingUpgrade").SetValue(combat,true);
            yield return new WaitForSecondsRealtime(1.8f);
            bool paused=combat.RouteEncounterStage(0)==CombatPlayground.RouteStage.Warning && combat.RouteEnemiesSpawned==0;
            typeof(CombatPlayground).GetProperty("ChoosingUpgrade").SetValue(combat,false);
            bool cleared=true; int[] quotas={5,9};
            for (int encounter=0;encounter<2;encounter++)
            {
                if (encounter==1) motor.SetSpawn(CombatPlayground.RouteTriggers[1]+Vector3.up*1.1f);
                float deadline=Time.realtimeSinceStartup+24;
                while (Time.realtimeSinceStartup<deadline && combat.RouteEncounterStage(encounter)!=CombatPlayground.RouteStage.Cleared)
                {
                    if (combat.ChoosingUpgrade) combat.ChooseUpgrade(0);
                    if (capture>=0 && !captured && combat.RouteEnemiesSpawned>=3)
                    {
                        captured=true; string folder=args[capture+1]; Directory.CreateDirectory(folder);
                        var camera=Camera.main; camera.GetComponent<OrbitCamera>().enabled=false;
                        camera.transform.position=new Vector3(30,12,210); camera.transform.LookAt(new Vector3(8,0,253)); camera.fieldOfView=75;
                        yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(folder,"route-ambush.png")); yield return new WaitForSecondsRealtime(.3f);
                        combat.AutoFire=true;
                    }
                    yield return null;
                }
                while (combat.ChoosingUpgrade) combat.ChooseUpgrade(0);
                cleared &= combat.RouteEncounterStage(encounter)==CombatPlayground.RouteStage.Cleared && combat.RouteEnemiesSpawned==quotas[encounter];
                var drops=(IList)typeof(CombatPlayground).GetField("orbs",Flags).GetValue(combat);
                if (drops.Count>0)
                {
                    var orb=(GameObject)drops[0].GetType().GetField("body").GetValue(drops[0]);
                    motor.SetSpawn(orb.transform.position+Vector3.up*.6f); yield return new WaitForSecondsRealtime(.15f);
                    while (combat.ChoosingUpgrade) combat.ChooseUpgrade(0);
                }
                Debug.Log($"SWARM_ROUTE_ENCOUNTER_TRACE: index={encounter} state={combat.RouteEncounterStage(encounter)} spawned={combat.RouteEnemiesSpawned} kills={combat.Kills}");
            }
            int spawned=combat.RouteEnemiesSpawned; motor.SetSpawn(CombatPlayground.RouteTriggers[0]+Vector3.left*10+Vector3.up*1.1f);
            yield return new WaitForSecondsRealtime(.25f); bool once=combat.RouteEnemiesSpawned==spawned;
            // Sentinel should use the nearby fallback on the passage instead of the basin.
            motor.SetSpawn(new Vector3(0,9.1f,622));
            bool sentinel=(bool)typeof(CombatPlayground).GetMethod("SpawnAroundPlayer",Flags).Invoke(combat,new object[]{true});
            var boss=GameObject.Find("Canyon Sentinel"); sentinel &= boss && boss.transform.position.z>600;
            bool xp=combat.TotalXP>0;
            combat.StopCombat(); bool cleanup=combat.RouteEnemiesSpawned==0 && combat.EnemyCount==0;
            combat.AutoSpawn=false; combat.Begin(); bool reset=combat.RouteEncounterStage(0)==CombatPlayground.RouteStage.Waiting && combat.RouteEncounterStage(1)==CombatPlayground.RouteStage.Waiting;
            combat.StopCombat();
            bool passed=outside && warning && paused && cleared && once && xp && sentinel && cleanup && reset;
            Debug.Log($"SWARM_ROUTE_ENCOUNTER_TEST: {(passed ? "PASS" : "FAIL")} outside={outside} warning={warning} pause={paused} clears={cleared} once={once} normalXP={xp} sentinelNearParty={sentinel} cleanup={cleanup} reset={reset}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
