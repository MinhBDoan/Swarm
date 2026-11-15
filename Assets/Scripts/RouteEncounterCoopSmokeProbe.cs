#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
namespace Swarm
{
    public class RouteEncounterCoopSmokeProbe : MonoBehaviour
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic; bool failed;
        void Check(bool ok,string label) { failed|=!ok; Debug.Log($"SWARM_ROUTECOOP_TRACE: {label}={ok}"); }
        IEnumerator Wait(Func<bool> condition)
        {
            float until=Time.realtimeSinceStartup+20;
            while (!condition() && Time.realtimeSinceStartup<until) yield return null;
            Check(condition(),"condition completed");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-routecoop-role")>=0)
                new GameObject("Route co-op probe").AddComponent<RouteEncounterCoopSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60; yield return null;
            var args=Environment.GetCommandLineArgs(); bool host=args[Array.IndexOf(args,"-swarm-routecoop-role")+1]=="host";
            int count=2,arg=Array.IndexOf(args,"-swarm-routecoop-count"); if(arg>=0) count=int.Parse(args[arg+1]);
            var session=FindObjectOfType<MultiplayerSession>(); session.Begin(host);
            yield return Wait(()=>NetworkManager.Singleton.IsConnectedClient && NetworkManager.Singleton.LocalClient.PlayerObject);
            var motor=NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<MomentumMotor>(); motor.enabled=false;
            var combat=CombatPlayground.Instance;
            if (host)
            {
                yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==count);
                combat.AutoSpawn=combat.AutoFire=false; combat.Begin();
                typeof(CombatPlayground).GetField("spawnClock",Flags).SetValue(combat,10000f); combat.AutoSpawn=true;
                for (int i=0;i<2;i++)
                {
                    int index=i,quota=i==0 ? 5 : 4;
                    yield return Wait(()=>combat.RouteEncounterStage(index)==CombatPlayground.RouteStage.Warning);
                    Check(motor.transform.position.z<76,"remote explorer triggers encounter");
                    yield return Wait(()=>combat.RouteEnemiesRemaining(index)==quota);
                    Check(combat.RouteEnemiesSpawned==(i==0 ? 5 : 9),"one shared spawn set");
                    yield return new WaitForSecondsRealtime(.5f);
                    for (int n=combat.EnemyCount-1;n>=0;n--) typeof(CombatPlayground).GetMethod("Kill",Flags).Invoke(combat,new object[]{n});
                    var orbs=(IList)typeof(CombatPlayground).GetField("orbs",Flags).GetValue(combat);
                    foreach (var orb in orbs) Destroy((GameObject)orb.GetType().GetField("body").GetValue(orb)); orbs.Clear();
                    yield return Wait(()=>combat.RouteEncounterStage(index)==CombatPlayground.RouteStage.Cleared);
                }
                yield return new WaitForSecondsRealtime(1); Check(combat.RouteEnemiesSpawned==9,"encounters do not repeat");
                combat.StopCombat(); yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==1);
            }
            else
            {
                yield return Wait(()=>combat.Running);
                for (int i=0;i<2;i++)
                {
                    int index=i; motor.SetSpawn(CombatPlayground.RouteTriggers[i]+Vector3.left*(i==0 ? 10 : 0)+Vector3.up*1.1f);
                    yield return Wait(()=>combat.RouteEncounterStage(index)==CombatPlayground.RouteStage.Warning);
                    yield return Wait(()=>combat.RouteEnemiesRemaining(index)==(index==0 ? 5 : 4));
                    Check(combat.RouteEnemiesSpawned==(i==0 ? 5 : 9),"replicated shared totals");
                    yield return Wait(()=>combat.RouteEncounterStage(index)==CombatPlayground.RouteStage.Cleared);
                }
                motor.SetSpawn(CombatPlayground.RouteTriggers[0]+Vector3.left*10+Vector3.up*1.1f);
                yield return new WaitForSecondsRealtime(.5f); Check(combat.RouteEnemiesSpawned==9,"revisit stays cleared");
                yield return Wait(()=>!combat.Running);
            }
            session.Leave(); yield return new WaitForSecondsRealtime(.4f);
            Debug.Log($"SWARM_ROUTECOOP_TEST: {(failed ? "FAIL" : "PASS")} role={(host ? "host" : "client")}"); Application.Quit(failed ? 1 : 0);
        }
    }
}
#endif
