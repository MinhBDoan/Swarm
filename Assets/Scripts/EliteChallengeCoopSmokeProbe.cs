#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Swarm
{
    public class EliteChallengeCoopSmokeProbe : MonoBehaviour
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        const string ProbeMessage="swarm.elite-test.phase";
        CombatPlayground combat;
        MomentumMotor motor;
        bool passed=true,invalidRequestSent;
        void Check(bool ok,string text){passed &= ok;Debug.Log($"SWARM_ELITECHALLENGECOOP_STEP: {text}={ok}");}
        IEnumerator Wait(Func<bool> ready,string label)
        {
            float until=Time.realtimeSinceStartup+25;
            while(!ready() && Time.realtimeSinceStartup<until)yield return null;
            Check(ready(),label);
        }
        float Elapsed=>(float)typeof(CombatPlayground).GetField("elapsed",Private).GetValue(combat);
        void Phase(string phase)=>typeof(CombatPlayground).GetProperty("Notice").SetValue(combat,phase);
        void SendCombat(string action,int index)=>typeof(CombatPlayground).GetMethod("SendCombatCommand",Private).Invoke(combat,new object[]{action,index});
        void KillChallengeGuards()
        {
            var enemies=(IList)typeof(CombatPlayground).GetField("enemies",Private).GetValue(combat);
            for(int i=enemies.Count-1;i>=0;i--)
            {
                var enemy=enemies[i];var type=enemy.GetType();
                if(!(bool)type.GetField("challengeElite").GetValue(enemy))continue;
                Check((bool)type.GetField("eliteVariant").GetValue(enemy) && !(bool)type.GetField("elite").GetValue(enemy),"replicated encounter uses ordinary elite variants");
                typeof(CombatPlayground).GetMethod("Kill",Private).Invoke(combat,new object[]{i});
            }
        }
        void SignalInvalidSent()
        {
            using(var writer=new FastBufferWriter(sizeof(int),Allocator.Temp))
            {
                writer.WriteValueSafe(1);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(ProbeMessage,NetworkManager.ServerClientId,writer,NetworkDelivery.ReliableFragmentedSequenced);
            }
        }
        void ReceiveProbeSignal(ulong sender,FastBufferReader reader)
        {
            if(sender!=1)return;
            reader.ReadValueSafe(out int phase);invalidRequestSent=phase==1;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-elite-challenge-role")>=0)
                new GameObject("Elite challenge co-op probe").AddComponent<EliteChallengeCoopSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60;yield return null;
            var args=Environment.GetCommandLineArgs();bool host=args[Array.IndexOf(args,"-swarm-elite-challenge-role")+1]=="host";
            var session=FindObjectOfType<MultiplayerSession>();session.Begin(host);
            yield return Wait(()=>NetworkManager.Singleton.IsConnectedClient && NetworkManager.Singleton.LocalClient.PlayerObject,"connected");
            combat=CombatPlayground.Instance;motor=NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<MomentumMotor>();motor.enabled=false;
            if(host)
            {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(ProbeMessage,ReceiveProbeSignal);
                yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==2,"two players");
                combat.AutoSpawn=combat.AutoFire=false;combat.Begin();Phase("elite-probe invalid-range");
                yield return Wait(()=>invalidRequestSent,"remote invalid request delivered");
                yield return new WaitForSecondsRealtime(.4f);
                Check(combat.ActiveEliteChallenge<0 && combat.EliteChallengeSpawned(0)==0,"host rejects out-of-range command");
                Phase("elite-probe approach");
                yield return Wait(()=>combat.ActiveEliteChallenge==0,"remote valid activation accepted");
                Check(combat.EliteChallengeQuota(0)==12,"two-player quota is twelve");
                float elapsed=Elapsed;int initialChests=combat.FreeChestCount;
                float until=Time.realtimeSinceStartup+35;
                bool sawEmptyWhileArriving=false,noEarlyChest=true;
                while(combat.ActiveEliteChallenge==0 && Time.realtimeSinceStartup<until)
                {
                    KillChallengeGuards();
                    if(combat.EliteChallengeSpawned(0)>0 && combat.EliteChallengeSpawned(0)<12 && combat.EliteChallengeRemaining(0)==0)
                    {
                        sawEmptyWhileArriving=true;
                        noEarlyChest &= combat.FreeChestCount==initialChests;
                    }
                    yield return null;
                }
                Check(sawEmptyWhileArriving,"empty early pack did not finish encounter");
                Check(noEarlyChest,"no chest while later packs pending");
                Check(combat.EliteChallengeStateAt(0)==CombatPlayground.EliteChallengePhase.Completed && combat.EliteChallengeSpawned(0)==12,"full remote challenge completed");
                Check(Elapsed>elapsed+5,"area clock advances through reinforcement encounter");
                Check(combat.FreeChestCount==initialChests+1 && combat.EliteChallengeRewardsSpawned==1,"exactly one guaranteed reward");
                Check(combat.FreeChestTier(initialChests)==CombatPlayground.UpgradeRarity.Epic,"reward tier is Epic");
                yield return Wait(()=>combat.ChoosingUpgrade && combat.FreeChestOpened(initialChests),"remote chest claim pauses party for reward");
                Check(combat.UpgradeChoices.Count==3,"host gets own offer list");
                for(int i=0;i<combat.UpgradeChoices.Count;i++)Check(combat.ChoiceRarity(i)==CombatPlayground.UpgradeRarity.Epic,"host reward tier");
                combat.ChooseUpgrade(0);
                yield return Wait(()=>combat.PlayerState(0).upgrades==1 && combat.PlayerState(1).upgrades==1 && !combat.PartyPaused,"both independent selections resolved");
                yield return new WaitForSecondsRealtime(.5f);
                Check(combat.FreeChestCount==initialChests+1 && combat.EliteChallengeRewardsSpawned==1,"duplicate claim grants no extra chest");
                combat.StopCombat();
                yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==1,"client validates stop and leaves");
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(ProbeMessage);
            }
            else
            {
                yield return Wait(()=>combat.Running && combat.Notice=="elite-probe invalid-range","run and layout received");
                motor.SetSpawn(new Vector3(5000,2,5000));yield return new WaitForSecondsRealtime(.5f);
                Check(!combat.TryEliteChallenge(0),"client rejects out-of-range interaction");
                SendCombat("elite-challenge",0);SignalInvalidSent();
                yield return Wait(()=>combat.Notice=="elite-probe approach","server rejected invalid attempt");
                motor.SetSpawn(CombatPlayground.ChallengeLocations[0]+Vector3.up*1.1f);
                yield return new WaitForSecondsRealtime(.6f);
                float until=Time.realtimeSinceStartup+8;
                while(combat.ActiveEliteChallenge<0 && Time.realtimeSinceStartup<until)
                {
                    combat.TryEliteChallenge(0);
                    yield return new WaitForSecondsRealtime(.4f);
                }
                Check(combat.ActiveEliteChallenge==0 && combat.EliteChallengeQuota(0)==12,"host challenge state and quota replicated");
                SendCombat("elite-challenge",0); // Deliberately bypass local duplicate filtering.
                yield return Wait(()=>combat.EliteChallengeStateAt(0)==CombatPlayground.EliteChallengePhase.Completed,"completion replicated");
                Check(combat.EliteChallengeSpawned(0)==12 && combat.EliteChallengeRemaining(0)==0,"spawned and remaining counts replicated");
                yield return Wait(()=>combat.FreeChestCount==1,"free chest state replicated");
                Check(combat.EliteChallengeRewardsSpawned==1 && combat.FreeChestTier(0)==CombatPlayground.UpgradeRarity.Epic,"single Epic reward replicated");
                motor.SetSpawn(combat.FreeChestPosition(0)+Vector3.up*.45f);
                yield return new WaitForSecondsRealtime(.6f);
                Check(combat.TryFreeChest(0),"client claims free party chest");combat.TryFreeChest(0);
                yield return Wait(()=>combat.ChoosingUpgrade,"client receives own upgrade selection");
                for(int i=0;i<combat.UpgradeChoices.Count;i++)Check(combat.ChoiceRarity(i)==CombatPlayground.UpgradeRarity.Epic,"client reward tier");
                combat.ChooseUpgrade(Mathf.Min(1,combat.UpgradeChoices.Count-1));
                yield return Wait(()=>combat.UpgradeCount==1 && !combat.PartyPaused,"client personal progression replicated");
                Check(combat.FreeChestCount==1 && combat.FreeChestOpened(0),"opened reward remains single-use");
                yield return Wait(()=>!combat.Running,"stop received");
            }
            session.Leave();
            Debug.Log($"SWARM_ELITECHALLENGECOOP_TEST: {(passed ? "PASS" : "FAIL")} role={(host ? "host" : "client")}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
