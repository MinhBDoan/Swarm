#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace Swarm
{
    public class EliteChallengeSmokeProbe : MonoBehaviour
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        CombatPlayground combat;
        MomentumMotor motor;
        bool passed=true;
        void Check(bool ok,string text){passed &= ok;Debug.Log($"SWARM_ELITECHALLENGE_STEP: {text}={ok}");}
        void Tick(float dt)=>typeof(CombatPlayground).GetMethod("TickEliteChallenges",Private).Invoke(combat,new object[]{dt});
        IList Enemies=>(IList)typeof(CombatPlayground).GetField("enemies",Private).GetValue(combat);
        int KillAllGuards()
        {
            var enemies=Enemies;int killed=0;
            for(int i=enemies.Count-1;i>=0;i--)
            {
                object enemy=enemies[i];var type=enemy.GetType();
                if(!(bool)type.GetField("challengeElite").GetValue(enemy))continue;
                Check((bool)type.GetField("eliteVariant").GetValue(enemy) && !(bool)type.GetField("elite").GetValue(enemy),"challenge guard is elite variant, not boss");
                typeof(CombatPlayground).GetMethod("Kill",Private).Invoke(combat,new object[]{i});killed++;
            }
            return killed;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-elite-challenge-test")>=0)
                new GameObject("Elite challenge probe").AddComponent<EliteChallengeSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60;yield return null;
            combat=CombatPlayground.Instance;combat.AutoSpawn=combat.AutoFire=false;combat.Begin();combat.enabled=false;
            motor=FindObjectOfType<MomentumMotor>();motor.enabled=false;
            for(int i=0;i<3;i++)
            {
                Vector3 floor=CombatPlayground.ChallengeLocations[i];
                bool grounded=Physics.Raycast(floor+Vector3.up*10,Vector3.down,out var hit,15,1<<0);
                Check(grounded && Mathf.Abs(hit.point.y-floor.y)<.15f,"challenge pedestal grounded "+i);
                Check(NavMesh.SamplePosition(floor,out var nav,2,NavMesh.AllAreas) && Vector3.Distance(nav.position,floor)<1.5f,"challenge floor navigable "+i);
            }
            motor.SetSpawn(new Vector3(5000,2,5000));Check(!combat.TryEliteChallenge(0),"out of range rejected");
            motor.SetSpawn(CombatPlayground.ChallengeLocations[0]+Vector3.up*1.1f);
            typeof(CombatPlayground).GetProperty("PartyPaused").SetValue(combat,true);
            Check(!combat.TryEliteChallenge(0),"paused activation rejected");
            typeof(CombatPlayground).GetProperty("PartyPaused").SetValue(combat,false);
            Check(combat.TryEliteChallenge(0) && combat.ActiveEliteChallenge==0 && combat.EliteChallengeQuota(0)==10,"activate ten solo elites");
            Check(!combat.TryEliteChallenge(0),"duplicate activation rejected");
            int initialChests=combat.FreeChestCount;
            Tick(.5f);Check(combat.EliteChallengeSpawned(0)>0 && combat.EliteChallengeSpawned(0)<=2,"first pack paced to two");
            KillAllGuards();Tick(.1f);
            Check(combat.EliteChallengeRemaining(0)==0 && combat.ActiveEliteChallenge==0 && combat.FreeChestCount==initialChests,"no early completion before quota");
            motor.SetSpawn(CombatPlayground.ChallengeLocations[1]+Vector3.up*1.1f);
            Check(!combat.TryEliteChallenge(1),"second spot blocked while first active");
            motor.SetSpawn(CombatPlayground.ChallengeLocations[0]+Vector3.up*1.1f);
            typeof(CombatPlayground).GetProperty("PartyPaused").SetValue(combat,true);
            int before=combat.EliteChallengeSpawned(0);Tick(10);
            Check(combat.EliteChallengeSpawned(0)==before,"pause freezes reinforcement schedule");
            typeof(CombatPlayground).GetProperty("PartyPaused").SetValue(combat,false);
            for(int step=0;step<20 && combat.EliteChallengeSpawned(0)<combat.EliteChallengeQuota(0);step++)Tick(2);
            Check(combat.EliteChallengeSpawned(0)==10 && combat.EliteChallengeRemaining(0)>0,"full quota arrived without instant reward");
            Check(combat.FreeChestCount==initialChests,"living guards prevent reward");
            KillAllGuards();Tick(.1f);
            Check(combat.EliteChallengeStateAt(0)==CombatPlayground.EliteChallengePhase.Completed && combat.ActiveEliteChallenge<0,"all guards cleared completes challenge");
            Check(combat.FreeChestCount==initialChests+1 && combat.EliteChallengeRewardsSpawned==1,"exactly one free reward chest");
            Check(combat.FreeChestTier(initialChests)==CombatPlayground.UpgradeRarity.Epic && !combat.FreeChestOpened(initialChests),"guaranteed Epic chest unopened");
            Tick(30);Tick(30);Check(combat.FreeChestCount==initialChests+1 && !combat.TryEliteChallenge(0),"completed spot cannot repeat reward");
            Check(!combat.ChoosingUpgrade && !combat.Won,"chest awaits interaction and does not clear map");
            var snapshot=(CombatPlayground.EliteChallengeSnapshot)typeof(CombatPlayground).GetMethod("BuildChallengeSnapshot",Private).Invoke(combat,null);
            Check(snapshot.states.Length==3 && snapshot.spawned[0]==10 && snapshot.remaining[0]==0 && snapshot.rewards==1,"snapshot retains challenge progress");
            motor.SetSpawn(CombatPlayground.ChallengeLocations[1]+Vector3.up*1.1f);
            Check(combat.TryEliteChallenge(1),"another spot available after completion");
            combat.StopCombat();combat.Begin();combat.enabled=false;
            Check(combat.ActiveEliteChallenge<0 && combat.EliteChallengeRewardsSpawned==0,"fresh run clears active challenge and rewards");
            for(int i=0;i<3;i++)Check(combat.EliteChallengeStateAt(i)==CombatPlayground.EliteChallengePhase.Available && combat.EliteChallengeSpawned(i)==0 && combat.EliteChallengeRemaining(i)==0,"fresh spot reset "+i);
            combat.StopCombat();
            Debug.Log($"SWARM_ELITECHALLENGE_TEST: {(passed ? "PASS" : "FAIL")}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
