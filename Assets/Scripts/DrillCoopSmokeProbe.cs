#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
namespace Swarm
{
    public class DrillCoopSmokeProbe : MonoBehaviour
    {
        bool failed;
        void Check(bool condition,string label) { failed|=!condition; Debug.Log($"SWARM_DRILLCOOP_TRACE: {label}={condition}"); }
        IEnumerator Wait(Func<bool> condition)
        {
            float deadline=Time.realtimeSinceStartup+12;
            while (!condition() && Time.realtimeSinceStartup<deadline) yield return null;
            Check(condition(),"condition completed");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-drillcoop-role")>=0)
                new GameObject("Drill co-op probe").AddComponent<DrillCoopSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60; yield return null;
            var args=Environment.GetCommandLineArgs(); bool host=args[Array.IndexOf(args,"-swarm-drillcoop-role")+1]=="host";
            var session=FindObjectOfType<MultiplayerSession>(); session.Begin(host);
            yield return Wait(()=>NetworkManager.Singleton.IsConnectedClient && NetworkManager.Singleton.LocalClient.PlayerObject);
            var motor=NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<MomentumMotor>(); motor.enabled=false;
            var drill=DrillHazard.Instance; motor.SetSpawn(drill.Center+Vector3.up*1.1f);
            var combat=CombatPlayground.Instance;
            if (host)
            {
                yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==2); yield return new WaitForSecondsRealtime(.7f);
                drill.AcceptState(6.7f,false); yield return Wait(()=>combat.DrillPracticeHealth==70);
                Check(motor.Velocity.y>0,"host practice knockback");
                yield return new WaitForSecondsRealtime(.7f);
                combat.AutoSpawn=combat.AutoFire=false; combat.Begin();
                drill.AcceptState(DrillHazard.CycleSeconds+6.7f,false);
                yield return Wait(()=>combat.Health==70 && combat.PlayerState(1)!=null && combat.PlayerState(1).health==70);
                Check(combat.Health==70,"host-authoritative combat damage");
                typeof(CombatPlayground).GetMethod("GivePartyXP",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(combat,new object[] {8});
                yield return Wait(()=>combat.PartyPaused); float time=drill.CycleTime;
                yield return new WaitForSecondsRealtime(.4f); Check(Mathf.Abs(drill.CycleTime-time)<.01f,"drill pauses for upgrades");
                combat.ChooseUpgrade(0); yield return Wait(()=>!combat.PartyPaused);
                float resumed=drill.CycleTime; yield return new WaitForSecondsRealtime(.3f); Check(drill.CycleTime>resumed+.1f,"drill resumes");
                combat.StopCombat(); yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==1);
            }
            else
            {
                yield return Wait(()=>combat.DrillPracticeHealth==70);
                Check(combat.ReceivedDrillStates>0 && motor.Velocity.y>0,"replicated practice damage and impulse");
                yield return Wait(()=>combat.Running && combat.Health==70);
                Check(combat.Health==70,"replicated combat damage");
                yield return Wait(()=>combat.ChoosingUpgrade);
                yield return new WaitForSecondsRealtime(.8f); Check(drill.Paused,"replicated paused cycle");
                combat.ChooseUpgrade(0); yield return Wait(()=>!combat.ChoosingUpgrade);
                yield return Wait(()=>!combat.Running);
            }
            session.Leave(); yield return new WaitForSecondsRealtime(.5f);
            Debug.Log($"SWARM_DRILLCOOP_TEST: {(failed ? "FAIL" : "PASS")} role={(host ? "host" : "client")}"); Application.Quit(failed ? 1 : 0);
        }
    }
}
#endif
