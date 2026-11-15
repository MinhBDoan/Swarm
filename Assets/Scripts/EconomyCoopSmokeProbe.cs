#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
namespace Swarm
{
    public class EconomyCoopSmokeProbe : MonoBehaviour
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;bool failed;
        void Check(bool ok,string label){failed|=!ok;Debug.Log($"SWARM_ECONOMYCOOP_TRACE: {label}={ok}");}
        IEnumerator Wait(Func<bool> condition){float until=Time.realtimeSinceStartup+20;while(!condition() && Time.realtimeSinceStartup<until)yield return null;Check(condition(),"wait completed");}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-economycoop-role")>=0)new GameObject("Economy co-op probe").AddComponent<EconomyCoopSmokeProbe>();}
        IEnumerator Start()
        {
            Application.targetFrameRate=60;yield return null;var args=Environment.GetCommandLineArgs();bool host=args[Array.IndexOf(args,"-swarm-economycoop-role")+1]=="host";
            var session=FindObjectOfType<MultiplayerSession>();session.Begin(host);yield return Wait(()=>NetworkManager.Singleton.IsConnectedClient && NetworkManager.Singleton.LocalClient.PlayerObject);
            var motor=NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<MomentumMotor>();motor.enabled=false;var combat=CombatPlayground.Instance;var type=typeof(CombatPlayground);
            if(host)
            {
                yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==6);combat.AutoSpawn=combat.AutoFire=false;combat.Begin();
                combat.SpawnEnemy(new Vector3(0,0,10));type.GetMethod("Kill",Flags).Invoke(combat,new object[]{0});ClearDrops(combat);
                for(int i=0;i<3;i++){motor.SetSpawn(CombatPlayground.SalvageFloors[i]+Vector3.up*1.1f);combat.TrySupplyInteraction(i+4);}
                yield return Wait(()=>combat.CratesPurchased==1);Check(combat.SharedMoney==5,"single shared deduction after duplicate requests");
                for(int i=0;i<combat.UpgradeChoices.Count;i++)Check(combat.ChoiceRarity(i)==CombatPlayground.UpgradeRarity.Rare,"host outside gets rare choice");combat.ChooseUpgrade(0);yield return Wait(()=>AllPicked(combat,1));
                combat.SpawnEnemy(new Vector3(-22,5.5f,31),true);type.GetMethod("Kill",Flags).Invoke(combat,new object[]{0});ClearDrops(combat);
                for(int i=3;i<6;i++){motor.SetSpawn(CombatPlayground.SalvageFloors[i]+Vector3.up*1.1f);combat.TrySupplyInteraction(i+4);}
                type.GetMethod("SwitchContext",Flags).Invoke(combat,new object[]{5ul});type.GetProperty("Health").SetValue(combat,0f);type.GetMethod("SwitchContext",Flags).Invoke(combat,new object[]{0ul});
                yield return Wait(()=>combat.CratesPurchased==2);Check(combat.SharedMoney==0 && combat.PlayerState(5).pendingTiers[0]==3,"epic purchase queues exact reward for downed teammate");
                type.GetMethod("ReviveParty",Flags).Invoke(combat,null);combat.ChooseUpgrade(0);yield return Wait(()=>AllPicked(combat,2));
                yield return new WaitForSecondsRealtime(.5f);combat.StopCombat();yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==1);
            }
            else
            {
                yield return Wait(()=>combat.Running);
                if(NetworkManager.Singleton.LocalClientId==1)
                {
                    yield return Wait(()=>combat.SharedMoney>=35);motor.SetSpawn(CombatPlayground.MoneyCrateFloors[0]+Vector3.up*1.1f);yield return new WaitForSecondsRealtime(.4f);
                    combat.TrySupplyInteraction(0);combat.TrySupplyInteraction(0);
                }
                yield return Wait(()=>combat.BonusUpgrade);Check(combat.SharedMoney==5 && combat.MoneyCrateOpened(0),"wallet and opened crate replicated");
                for(int i=0;i<combat.UpgradeChoices.Count;i++)Check(combat.ChoiceRarity(i)==CombatPlayground.UpgradeRarity.Rare,"remote gets rare choice");combat.ChooseUpgrade(0);yield return Wait(()=>combat.UpgradeCount==1);
                if(NetworkManager.Singleton.LocalClientId==1)
                {
                    yield return Wait(()=>combat.SharedMoney>=75);motor.SetSpawn(CombatPlayground.MoneyCrateFloors[3]+Vector3.up*1.1f);yield return new WaitForSecondsRealtime(1f);Debug.Log($"SWARM_ECONOMYCOOP_TRACE: buyingEpic money={combat.SharedMoney} health={combat.Health} choosing={combat.ChoosingUpgrade} position={motor.transform.position} result={combat.TrySupplyInteraction(3)}");
                }
                yield return Wait(()=>combat.BonusUpgrade);Check(combat.SharedMoney==0 && combat.MoneyCrateOpened(3),"epic purchase replicated");
                for(int i=0;i<combat.UpgradeChoices.Count;i++)Check(combat.ChoiceRarity(i)==CombatPlayground.UpgradeRarity.Epic,"remote gets epic choice");combat.ChooseUpgrade(0);yield return Wait(()=>combat.UpgradeCount==2);yield return Wait(()=>!combat.Running);
            }
            session.Leave();yield return new WaitForSecondsRealtime(1f);Debug.Log($"SWARM_ECONOMYCOOP_TEST: {(failed ? "FAIL" : "PASS")} role={(host ? "host" : "client")}");Application.Quit(failed ? 1 : 0);
        }
        void ClearDrops(CombatPlayground combat)
        {
            // Isolate wallet/purchase state from ordinary XP choice queues.
            var orbs=(IList)typeof(CombatPlayground).GetField("orbs",Flags).GetValue(combat);
            foreach(var orb in orbs)Destroy((GameObject)orb.GetType().GetField("body").GetValue(orb));orbs.Clear();
        }
        bool AllPicked(CombatPlayground combat,int count){foreach(ulong id in NetworkManager.Singleton.ConnectedClientsIds)if(combat.PlayerState(id).upgrades!=count)return false;return !combat.PartyPaused;}
    }
}
#endif
