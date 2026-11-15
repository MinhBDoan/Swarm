#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
namespace Swarm
{
    public class CoopSmokeProbe : MonoBehaviour
    {
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        CombatPlayground combat;
        bool host;
        int expected=2;
        bool failed;
        void Check(bool pass,string name) { Debug.Log($"SWARM_COOP_TRACE: {name}={pass}"); failed|=!pass; }
        void Call(string name,params object[] args) => typeof(CombatPlayground).GetMethod(name,Flags).Invoke(combat,args);
        void Set(string name,object value) => typeof(CombatPlayground).GetProperty(name).SetValue(combat,value);
        IEnumerator Wait(Func<bool> condition,float seconds=12)
        {
            float until=Time.realtimeSinceStartup+seconds;
            while (!condition() && Time.realtimeSinceStartup<until) yield return null;
            if (!condition()) { failed=true; Debug.Log("SWARM_COOP_TRACE: wait timed out"); }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-coop-role")>=0)
                new GameObject("Coop Smoke Probe").AddComponent<CoopSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60;
            var args=Environment.GetCommandLineArgs(); host=args[Array.IndexOf(args,"-swarm-coop-role")+1]=="host";
            int countArg=Array.IndexOf(args,"-swarm-coop-count");
            if (countArg>=0) expected=int.Parse(args[countArg+1]);
            yield return null;
            combat=CombatPlayground.Instance;
            var session=FindObjectOfType<MultiplayerSession>(); session.Begin(host);
            yield return Wait(()=>NetworkManager.Singleton.IsConnectedClient && NetworkManager.Singleton.LocalClient.PlayerObject);
            var avatar=NetworkManager.Singleton.LocalClient.PlayerObject;
            var motor=avatar.GetComponent<MomentumMotor>(); motor.enabled=false;
            motor.SetSpawn(new Vector3(host ? -3 : 0,1.1f,-11));
            yield return new WaitForSecondsRealtime(.5f);
            if (host)
            {
                yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==expected);
                yield return new WaitForSecondsRealtime(1);
                combat.AutoSpawn=false; combat.Begin(); motor.SetSpawn(new Vector3(-3,1.1f,-11));
                // Exercise the same damage paths used by real enemy attacks.
                Call("HitPartyWithSlam",new Vector3(-1,0,-11));
                bool slam=true;
                foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds) slam &= combat.PlayerState(id).health==75;
                Check(slam,"slam damages every player in its circle");
                foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
                { Call("SwitchContext",id); Set("Health",100f); typeof(CombatPlayground).GetField("hurtClock",Flags).SetValue(combat,0f); }
                Call("SwitchContext",0ul);
                var shotType=typeof(CombatPlayground).GetNestedType("EnemyShot",BindingFlags.NonPublic);
                var shot=Activator.CreateInstance(shotType,true);
                var body=new GameObject("Probe enemy projectile"); body.transform.position=new Vector3(-2,1.1f,-11);
                shotType.GetField("body").SetValue(shot,body); shotType.GetField("velocity").SetValue(shot,Vector3.right*80);
                Call("HitPartyWithShot",shot,.05f); Destroy(body);
                int hitPlayers=0;
                foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds) if (combat.PlayerState(id).health<100) hitPlayers++;
                Check(hitPlayers==1 && combat.Health==100,"projectile damages first struck player only");
                foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
                { Call("SwitchContext",id); Set("Health",100f); typeof(CombatPlayground).GetField("hurtClock",Flags).SetValue(combat,0f); }
                Call("SwitchContext",0ul);
                foreach (var point in new[] { new Vector3(-2,0,-8),new Vector3(1,0,-8),new Vector3(3,0,-8) }) combat.SpawnEnemy(point);
                var enemies=(IList)typeof(CombatPlayground).GetField("enemies",Flags).GetValue(combat);
                foreach (var enemy in enemies) enemy.GetType().GetField("health").SetValue(enemy,1f);
                foreach (var agent in FindObjectsOfType<NavMeshAgent>()) agent.speed=0;
                yield return Wait(()=>combat.ChoosingUpgrade);
                Check(combat.Kills==3 && combat.TotalXP==9,"shared combat and collection");
                combat.ChooseUpgrade(0);
                yield return new WaitForSecondsRealtime(.5f);
                Check(combat.PartyPaused,"party waits for remote choice");
                ulong remote=1;
                foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds) if (id!=0) remote=id;
                yield return Wait(()=>
                {
                    foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
                        if (combat.PlayerState(id)==null || combat.PlayerState(id).upgrades!=1) return false;
                    return true;
                });
                yield return new WaitForSecondsRealtime(.3f);
                var state=combat.PlayerState(remote);
                Check(state.totalXP==9 && state.rerolls==1 && state.upgrades==1 && combat.UpgradeCount==1,"independent choices and remote rerolls");
                Check(state.shots>0 && combat.ShotsFired>0,"both players fire");
                Check(!combat.PartyPaused,"party resumes");
                var extra=new[] { BoltUpgrade.Boomerang,BoltUpgrade.PulseField,BoltUpgrade.RocketLauncher,BoltUpgrade.PiercingBow };
                foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
                { Call("SwitchContext",id); Call("ApplyUpgrade",extra[(int)(id%4)]); if (combat.WeaponRank(BoltUpgrade.PulseField)==0) Call("ApplyUpgrade",BoltUpgrade.PulseField); }
                Call("SwitchContext",0ul);
                combat.SpawnEnemy(new Vector3(0,0,-4));
                var visualsEnemy=(IList)typeof(CombatPlayground).GetField("enemies",Flags).GetValue(combat);
                foreach (var enemy in visualsEnemy) enemy.GetType().GetField("health").SetValue(enemy,10000f);
                foreach (var agent in FindObjectsOfType<NavMeshAgent>()) agent.speed=0;
                yield return new WaitForSecondsRealtime(5);
                var captured=(Array)typeof(CombatPlayground).GetMethod("CaptureVisuals",Flags).Invoke(combat,null);
                bool lineColors=true;
                foreach (var visual in captured)
                    if ((int)visual.GetType().GetField("shape").GetValue(visual)==3)
                        lineColors &= ((Color)visual.GetType().GetField("color").GetValue(visual)).maxColorComponent>.05f;
                Check(lineColors,"line colors survive snapshot capture");
                // Remove only the visual-test fixture; gameplay kills/XP were checked above.
                foreach (var enemy in visualsEnemy) Destroy((GameObject)enemy.GetType().GetField("body").GetValue(enemy));
                visualsEnemy.Clear();
                combat.AutoFire=false;
                foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
                    if (id!=0) { Call("SwitchContext",id); Set("Health",0f); }
                Call("SwitchContext",0ul);
                yield return new WaitForSecondsRealtime(.7f);
                // Another player's down state must not end the host's run.
                Check(combat.Running && !combat.PartyDefeated && combat.Health>0,"individual down state");
                typeof(CombatPlayground).GetField("elapsed",Flags).SetValue(combat,60f);
                combat.AutoSpawn=true;
                yield return Wait(()=>combat.Resting && combat.PlayerState(remote).health>=50 && combat.PlayerState(remote).health<55);
                Check(combat.PlayerState(remote).health>=50 && combat.PlayerState(remote).health<55,"wave break revives remote");
                yield return new WaitForSecondsRealtime(1);
                if (Array.IndexOf(args,"-swarm-coop-expect-early")>=0) Check(combat.Running && NetworkManager.Singleton.ConnectedClientsIds.Count==expected-1,"active pulse-owner disconnect is cleaned up");
                combat.StopCombat();
                yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==1);
            }
            else
            {
                yield return Wait(()=>combat.Running);
                Check(combat.ReceivedCombatSnapshots>0,"receives authoritative state");
                yield return Wait(()=>combat.ReplicatedVisualCount>0);
                Check(combat.ReplicatedVisualCount>0,"receives shared visuals");
                yield return Wait(()=>combat.ChoosingUpgrade);
                Check(combat.TotalXP==9 && combat.Level==2,"same shared XP and level");
                yield return new WaitForSecondsRealtime(1);
                combat.ChooseUpgrade(-1);
                yield return new WaitForSecondsRealtime(.3f);
                Check(combat.ChoosingUpgrade && combat.UpgradeCount==0,"invalid choice rejected");
                combat.RerollUpgrades(); yield return Wait(()=>combat.RerollsRemaining==2);
                combat.RerollUpgrades(); yield return Wait(()=>combat.RerollsRemaining==1);
                combat.ChooseUpgrade(1); combat.ChooseUpgrade(1);
                yield return Wait(()=>combat.UpgradeCount==1 && !combat.ChoosingUpgrade);
                Check(combat.UpgradeCount==1,"duplicate choice rejected");
                if (expected>=4) { yield return Wait(()=>combat.ReplicatedWeaponShapeMask==7); Check(combat.ReplicatedWeaponShapeMask==7,"distinct weapon meshes replicate"); }
                Check(combat.ReplicatedOwnRangeCount==(combat.WeaponRank(BoltUpgrade.PulseField)>0 ? 2 : 0),"persistent range belongs to local player only");
                int captureArg=Array.IndexOf(args,"-swarm-coop-capture");
                if (captureArg>=0)
                {
                    Screen.SetResolution(1280,720,false); yield return new WaitForSecondsRealtime(1);
                    ScreenCapture.CaptureScreenshot(args[captureArg+1]); yield return new WaitForSecondsRealtime(.5f);
                }
                yield return Wait(()=>combat.Health<=0);
                yield return Wait(()=>combat.Health>=50 && combat.Health<55);
                Check(combat.Health>=50 && combat.Health<55 && combat.Running,"replicated revive");
                if (Array.IndexOf(args,"-swarm-coop-early-leave")>=0)
                {
                    yield return new WaitForSecondsRealtime(.5f); session.Leave();
                    yield return new WaitForSecondsRealtime(.6f);
                    Check(!combat.Running && !NetworkManager.Singleton.IsListening,"active leave restores offline");
                    Debug.Log($"SWARM_COOP_TEST: {(failed ? "FAIL" : "PASS")} role=early-client"); Application.Quit(failed ? 1 : 0); yield break;
                }
                yield return Wait(()=>!combat.Running);
                Check(combat.ReplicatedVisualCount==0,"host stop clears client world");
            }
            session.Leave();
            yield return new WaitForSecondsRealtime(.6f);
            Check(!combat.Running && !NetworkManager.Singleton.IsListening && FindObjectOfType<MomentumMotor>()!=null,"offline restoration");
            Debug.Log($"SWARM_COOP_TEST: {(failed ? "FAIL" : "PASS")} role={(host ? "host" : "client")}");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
#endif
