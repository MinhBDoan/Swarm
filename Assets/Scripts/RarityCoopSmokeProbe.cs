#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
namespace Swarm
{
    public class RarityCoopSmokeProbe : MonoBehaviour
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic; bool failed;
        void Check(bool ok,string text) { failed|=!ok; Debug.Log($"SWARM_RARITYCOOP_TRACE: {text}={ok}"); }
        IEnumerator Wait(Func<bool> condition)
        {
            float until=Time.realtimeSinceStartup+15; while(!condition() && Time.realtimeSinceStartup<until) yield return null;
            Check(condition(),"wait completed");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-raritycoop-role")>=0) new GameObject("Rarity co-op probe").AddComponent<RarityCoopSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60; yield return null;
            var args=Environment.GetCommandLineArgs(); bool host=args[Array.IndexOf(args,"-swarm-raritycoop-role")+1]=="host";
            var session=FindObjectOfType<MultiplayerSession>(); session.Begin(host);
            yield return Wait(()=>NetworkManager.Singleton.IsConnectedClient && NetworkManager.Singleton.LocalClient.PlayerObject);
            var motor=NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<MomentumMotor>(); motor.enabled=false;
            var combat=CombatPlayground.Instance; var type=typeof(CombatPlayground);
            if(host)
            {
                yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==2); combat.AutoSpawn=combat.AutoFire=false; combat.Begin();
                foreach(ulong id in NetworkManager.Singleton.ConnectedClientsIds)
                {
                    type.GetMethod("SwitchContext",Flags).Invoke(combat,new object[]{id});
                    type.GetMethod("OpenBonusUpgrade",Flags).Invoke(combat,null);
                    var choices=(List<BoltUpgrade>)type.GetField("choices",Flags).GetValue(combat); choices.Clear(); choices.Add(BoltUpgrade.HeavyBolts); choices.Add(BoltUpgrade.RocketLauncher); choices.Add(BoltUpgrade.Power);
                    var rarities=(List<CombatPlayground.UpgradeRarity>)type.GetField("choiceRarities",Flags).GetValue(combat); rarities.Clear();
                    rarities.AddRange(new[]{CombatPlayground.UpgradeRarity.Legendary,CombatPlayground.UpgradeRarity.Epic,CombatPlayground.UpgradeRarity.Rare});
                }
                type.GetMethod("SwitchContext",Flags).Invoke(combat,new object[]{0ul});
                yield return new WaitForSecondsRealtime(.5f); combat.ChooseUpgrade(0);
                yield return Wait(()=>combat.PlayerState(1)!=null && combat.PlayerState(1).upgrades==1);
                var remote=combat.PlayerState(1);
                Check(combat.Damage>19 && remote.ranks[2]==1 && remote.crit==.2f && remote.shockRank==0,"independent tier effects");
                Check((int)type.GetField("shockwaveRank",Flags).GetValue(combat)==1 && (float)type.GetField("criticalChance",Flags).GetValue(combat)==0,"host legendary stays local");
                type.GetMethod("ApplyTieredUpgrade",Flags).Invoke(combat,new object[]{BoltUpgrade.Power,CombatPlayground.UpgradeRarity.Epic});
                combat.AutoFire=true; combat.SpawnEnemy(new Vector3(0,0,-6));
                var enemies=(IList)type.GetField("enemies",Flags).GetValue(combat); foreach(var enemy in enemies) enemy.GetType().GetField("health").SetValue(enemy,100000f);
                foreach(var agent in FindObjectsOfType<UnityEngine.AI.NavMeshAgent>()) agent.speed=0;
                yield return Wait(()=>combat.ShockwavesTriggered>0 && combat.CriticalHits>0);
                Check(combat.PlayerState(1).shockHits==0,"secondary shock does not leak to remote player");
                yield return new WaitForSecondsRealtime(.5f); combat.StopCombat(); yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==1);
            }
            else
            {
                yield return Wait(()=>combat.ChoosingUpgrade);
                Check(combat.ChoiceRarity(0)==CombatPlayground.UpgradeRarity.Legendary && combat.ChoiceRarity(1)==CombatPlayground.UpgradeRarity.Epic && combat.ChoiceRarity(2)==CombatPlayground.UpgradeRarity.Rare,"offer rarities replicated");
                combat.ChooseUpgrade(1); yield return Wait(()=>combat.UpgradeCount==1);
                Check(combat.WeaponRank(BoltUpgrade.RocketLauncher)==1 && (float)type.GetField("criticalChance",Flags).GetValue(combat)==.2f,"remote epic choice applied authoritatively");
                Check((int)type.GetField("shockwaveRank",Flags).GetValue(combat)==0,"host legendary not applied to remote");
                yield return Wait(()=>!combat.Running);
            }
            session.Leave(); yield return new WaitForSecondsRealtime(.4f);
            Debug.Log($"SWARM_RARITYCOOP_TEST: {(failed ? "FAIL" : "PASS")} role={(host ? "host" : "client")}"); Application.Quit(failed ? 1 : 0);
        }
    }
}
#endif
