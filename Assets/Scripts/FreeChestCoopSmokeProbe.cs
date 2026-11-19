#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

namespace Swarm
{
    public class FreeChestCoopSmokeProbe : MonoBehaviour
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        bool failed;
        void Check(bool ok,string label) {failed|=!ok;Debug.Log($"SWARM_FREE_CHEST_COOP_TRACE: {label}={ok}");}
        IEnumerator Wait(Func<bool> condition,string label)
        {
            float until=Time.realtimeSinceStartup+25;
            while (!condition() && Time.realtimeSinceStartup<until) yield return null;
            Check(condition(),label);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-free-chest-role")>=0)
                new GameObject("Free chest co-op probe").AddComponent<FreeChestCoopSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60;yield return null;
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-swarm-free-chest-role");
            bool host=at+1<args.Length && args[at+1]=="host";
            var session=FindObjectOfType<MultiplayerSession>();session.Begin(host);
            yield return Wait(()=>NetworkManager.Singleton.IsConnectedClient && NetworkManager.Singleton.LocalClient.PlayerObject,"connected");
            if (failed) {Finish(host);yield break;}
            var combat=CombatPlayground.Instance;var motor=NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<MomentumMotor>();motor.enabled=false;
            var type=typeof(CombatPlayground);combat.AutoSpawn=combat.AutoFire=false;
            if (host)
            {
                yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==2,"party joined");
                if (failed) {Finish(true);yield break;}
                ulong local=NetworkManager.Singleton.LocalClientId;
                var ids=NetworkManager.Singleton.ConnectedClientsIds;ulong peer=ids[0]==local ? ids[1] : ids[0];
                combat.Begin();int money=combat.SharedMoney;
                int first=combat.SpawnFreeRewardChest(new Vector3(0,0,10),CombatPlayground.UpgradeRarity.Rare);
                Check(first==0,"host spawned persistent free Rare drop");
                yield return Wait(()=>combat.FreeChestOpened(0),"client claim accepted");
                Check(combat.ChoosingUpgrade && combat.PlayerState(peer).choosing && combat.PlayerState(peer).bonus &&
                    combat.ChoiceRarity(0)==CombatPlayground.UpgradeRarity.Rare,"both players receive independent Rare choices");
                yield return Wait(()=>combat.PartyPaused,"group pause after claim");
                float clock=(float)type.GetField("elapsed",Private).GetValue(combat);
                yield return new WaitForSecondsRealtime(.35f);
                Check(Mathf.Abs((float)type.GetField("elapsed",Private).GetValue(combat)-clock)<.001f &&
                    combat.SharedMoney==money && combat.CratesPurchased==0,"free claim pauses timer without spending money");
                combat.ChooseUpgrade(0);
                yield return Wait(()=>combat.UpgradeCount==1 && combat.PlayerState(peer).upgrades==1 && !combat.PartyPaused,"one reward per member and group resume");
                yield return new WaitForSecondsRealtime(.35f);
                Check(combat.UpgradeCount==1 && combat.PlayerState(peer).upgrades==1 && !combat.ChoosingUpgrade &&
                    !combat.PlayerState(peer).choosing && combat.SharedMoney==money,"duplicate client request did not queue a second award");

                // A downed teammate must retain the guaranteed challenge award.
                type.GetMethod("SwitchContext",Private).Invoke(combat,new object[]{peer});
                type.GetProperty("Health").SetValue(combat,0f);
                type.GetMethod("SwitchContext",Private).Invoke(combat,new object[]{local});
                int second=combat.SpawnFreeRewardChest(new Vector3(5,0,10),CombatPlayground.UpgradeRarity.Epic,true);
                Check(second==1,"host spawned guaranteed Epic completion drop");
                yield return Wait(()=>
                {
                    var client=NetworkManager.Singleton.ConnectedClients[peer];
                    return client.PlayerObject && (client.PlayerObject.transform.position-new Vector3(5,3,10)).sqrMagnitude<.25f;
                },"downed client observed completion drop");
                motor.SetSpawn(combat.FreeChestPosition(second)+Vector3.up*.45f);
                Check(combat.TryFreeChest(second) && combat.PlayerState(peer).pendingTiers.Count==1 &&
                    combat.PlayerState(peer).pendingTiers[0]==(int)CombatPlayground.UpgradeRarity.Epic,"downed teammate queues exact guaranteed tier");
                type.GetMethod("ReviveParty",Private).Invoke(combat,null);
                combat.ChooseUpgrade(0);
                yield return Wait(()=>combat.UpgradeCount==2 && combat.PlayerState(peer).upgrades==2 && !combat.PartyPaused,"revived teammate receives exactly one queued Epic choice");
                Check(combat.SharedMoney==money && combat.CratesPurchased==0,"neither free reward charged the party");
                combat.StopCombat();yield return new WaitForSecondsRealtime(.5f);
            }
            else
            {
                yield return Wait(()=>combat.Running && combat.FreeChestCount==1,"drop state replicated");
                if (failed) {Finish(false);yield break;}
                Check(!combat.FreeChestOpened(0) && combat.FreeChestTier(0)==CombatPlayground.UpgradeRarity.Rare,"unopened Rare drop visible to client");
                motor.SetSpawn(combat.FreeChestPosition(0)+Vector3.up*.45f);
                // Owner movement needs to reach the host before its proximity validation.
                yield return new WaitForSecondsRealtime(.35f);
                Check(combat.TryFreeChest(0) && combat.TryFreeChest(0),"client sent duplicate claim requests");
                yield return Wait(()=>combat.ChoosingUpgrade && combat.FreeChestOpened(0),"client claim and upgrade state replicated");
                Check(combat.ChoiceRarity(0)==CombatPlayground.UpgradeRarity.Rare,"client independent Rare choice");
                yield return new WaitForSecondsRealtime(.6f);
                combat.ChooseUpgrade(0);
                yield return Wait(()=>combat.UpgradeCount==1,"client first choice resolved");
                yield return Wait(()=>combat.FreeChestCount==2 && combat.Health<=0,"downed state and guaranteed completion drop replicated");
                Check(combat.FreeChestTier(1)==CombatPlayground.UpgradeRarity.Epic && !combat.FreeChestOpened(1),"guaranteed Epic waits for explicit open");
                motor.SetSpawn(new Vector3(5,3,10));
                yield return Wait(()=>combat.Health>0 && combat.ChoosingUpgrade && combat.FreeChestOpened(1),"queued reward opens after revive");
                Check(combat.ChoiceRarity(0)==CombatPlayground.UpgradeRarity.Epic,"revived client exact Epic tier");
                combat.ChooseUpgrade(0);
                yield return Wait(()=>combat.UpgradeCount==2,"client second choice resolved");
                yield return Wait(()=>!combat.Running,"host stop replicated");
                Check(combat.FreeChestCount==0,"client archive cleared at run end");
            }
            session.Leave();yield return new WaitForSecondsRealtime(.3f);Finish(host);
        }
        void Finish(bool host)
        {
            Debug.Log($"SWARM_FREE_CHEST_COOP_TEST: {(failed ? "FAIL" : "PASS")} role={(host ? "host" : "client")}");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
#endif
