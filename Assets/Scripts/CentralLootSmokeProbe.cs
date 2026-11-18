#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Swarm
{
    public class CentralLootSmokeProbe : MonoBehaviour
    {
        bool passed=true;
        CombatPlayground combat;
        MomentumMotor motor;
        static readonly int[] CentralCrates={4,5,6};
        void Check(bool ok,string label) { passed &= ok; Debug.Log($"SWARM_CENTRALLOOT_STEP: {label}={ok}"); }
        IEnumerator Wait(Func<bool> ready,string label)
        {
            float until=Time.realtimeSinceStartup+20;
            while (!ready() && Time.realtimeSinceStartup<until) yield return null;
            Check(ready(),label);
        }
        void CheckTier(int index)
        {
            Check(combat.ChoosingUpgrade && combat.BonusUpgrade,"bonus choice opened "+index);
            for(int i=0;i<combat.UpgradeChoices.Count;i++)
                Check(combat.ChoiceRarity(i)==CombatPlayground.MoneyCrateTier(index),"matching tier "+index);
        }
        void CheckPlacements()
        {
            Vector3 previous=Vector3.zero;
            foreach(int index in CentralCrates)
            {
                Vector3 floor=CombatPlayground.MoneyCrateFloors[index];
                bool ground=Physics.Raycast(floor+Vector3.up*10,out var hit,15,1<<0);
                Check(ground && Mathf.Abs(hit.point.y-floor.y)<.15f,"authored chest floor "+index);
                bool nav=NavMesh.SamplePosition(floor,out var navHit,2,NavMesh.AllAreas);
                Check(nav && Vector3.Distance(navHit.position,floor)<1.5f,"reachable chest floor "+index);
                if(index!=CentralCrates[0] && nav)
                {
                    var path=new NavMeshPath();
                    Check(NavMesh.CalculatePath(previous,navHit.position,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete,"chest connection "+index);
                }
                if(nav) previous=navHit.position;
            }
        }
        bool AllPicked(int count)
        {
            foreach(ulong id in NetworkManager.Singleton.ConnectedClientsIds)
                if(combat.PlayerState(id)==null || combat.PlayerState(id).upgrades!=count) return false;
            return !combat.PartyPaused;
        }
        void CheckReset()
        {
            Check(combat.SharedMoney==0 && combat.CratesPurchased==0,"wallet and purchases reset");
            for(int i=0;i<CombatPlayground.MoneyCrateCount;i++) Check(!combat.MoneyCrateOpened(i),"crate reset "+i);
            for(int i=0;i<CombatPlayground.SalvageCount;i++) Check(!combat.SalvageBroken(i),"salvage reset "+i);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"-swarm-central-loot-test")>=0 || Array.IndexOf(args,"-swarm-central-loot-role")>=0)
                new GameObject("Central loot probe").AddComponent<CentralLootSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60;
            yield return null;
            var args=Environment.GetCommandLineArgs();
            int role=Array.IndexOf(args,"-swarm-central-loot-role");
            combat=CombatPlayground.Instance;
            Check(CombatPlayground.MoneyCrateCount==7 && CombatPlayground.SalvageCount==6,"expanded supply counts");
            for(int i=0;i<4;i++) Check(CombatPlayground.MoneyCrateCost(i)==(i<2 ? 30 : 75),"old price preserved "+i);
            if(role<0)
            {
                combat.AutoSpawn=combat.AutoFire=false;
                combat.Begin();combat.enabled=false;
                motor=FindObjectOfType<MomentumMotor>();motor.enabled=false;
                CheckPlacements();
                Check(!combat.TrySupplyInteraction(CombatPlayground.SupplyCount),"out of range supply rejected");
                motor.SetSpawn(CombatPlayground.MoneyCrateFloors[4]+Vector3.up*1.1f);
                combat.TrySupplyInteraction(4);
                Check(combat.SharedMoney==0 && !combat.MoneyCrateOpened(4) && !combat.ChoosingUpgrade,"central insufficient funds");
                typeof(CombatPlayground).GetProperty("SharedMoney").SetValue(combat,135);
                int count=0;
                foreach(int index in CentralCrates)
                {
                    motor.SetSpawn(CombatPlayground.MoneyCrateFloors[index]+Vector3.up*1.1f);
                    int before=combat.SharedMoney;
                    Check(combat.TrySupplyInteraction(index) && combat.MoneyCrateOpened(index),"central purchase "+index);
                    Check(combat.SharedMoney==before-CombatPlayground.MoneyCrateCost(index) && combat.CratesPurchased==++count,"single deduction "+index);
                    CheckTier(index);combat.SkipUpgrade();
                    Check(!combat.TrySupplyInteraction(index) && combat.CratesPurchased==count,"duplicate rejected "+index);
                }
                Check(combat.SharedMoney==0,"all central prices charged");
                combat.StopCombat();CheckReset();
                combat.Begin();CheckReset();combat.StopCombat();
            }
            else
            {
                bool host=args[role+1]=="host";
                var session=FindObjectOfType<MultiplayerSession>();session.Begin(host);
                yield return Wait(()=>NetworkManager.Singleton.IsConnectedClient && NetworkManager.Singleton.LocalClient.PlayerObject,"connected");
                motor=NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<MomentumMotor>();motor.enabled=false;
                if(host)
                {
                    yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==2,"two players");
                    combat.AutoSpawn=combat.AutoFire=false;combat.Begin();CheckPlacements();
                    typeof(CombatPlayground).GetProperty("SharedMoney").SetValue(combat,135);
                    int expectedMoney=135,count=0;
                    foreach(int index in CentralCrates)
                    {
                        yield return Wait(()=>combat.CratesPurchased==count+1,"remote central purchase "+index);
                        expectedMoney-=CombatPlayground.MoneyCrateCost(index);
                        Check(combat.SharedMoney==expectedMoney && combat.MoneyCrateOpened(index),"remote single shared deduction "+index);
                        CheckTier(index);combat.ChooseUpgrade(0);count++;
                        yield return Wait(()=>AllPicked(count),"party choices resolved "+index);
                    }
                    combat.StopCombat();yield return new WaitForSecondsRealtime(.6f);
                    combat.Begin();CheckReset();
                    yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==1,"client checked reset and left");
                    combat.StopCombat();
                }
                else
                {
                    yield return Wait(()=>combat.Running && combat.SharedMoney==135,"funded run replicated");
                    int count=0;
                    foreach(int index in CentralCrates)
                    {
                        motor.SetSpawn(CombatPlayground.MoneyCrateFloors[index]+Vector3.up*1.1f);
                        yield return new WaitForSecondsRealtime(.5f);
                        Check(combat.TrySupplyInteraction(index),"send central purchase "+index);
                        combat.TrySupplyInteraction(index);
                        yield return Wait(()=>combat.ChoosingUpgrade,"party choice received "+index);
                        Check(combat.MoneyCrateOpened(index) && combat.CratesPurchased==count+1,"expanded opened flags replicated "+index);
                        CheckTier(index);combat.ChooseUpgrade(0);count++;
                        yield return Wait(()=>combat.UpgradeCount==count && !combat.PartyPaused,"party resumed "+index);
                    }
                    yield return Wait(()=>!combat.Running,"run stopped");
                    yield return Wait(()=>combat.Running,"new run replicated");
                    CheckReset();
                }
                session.Leave();
            }
            Debug.Log($"SWARM_CENTRALLOOT_TEST: {(passed ? "PASS" : "FAIL")} role={(role<0 ? "solo" : args[role+1])}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
