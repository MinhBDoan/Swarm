#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Swarm
{
    public class FreeChestSmokeProbe : MonoBehaviour
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        bool failed;
        void Check(bool ok,string label) {failed|=!ok;Debug.Log($"SWARM_FREE_CHEST_TRACE: {label}={ok}");}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-free-chest-test")>=0)
                new GameObject("Free chest probe").AddComponent<FreeChestSmokeProbe>();
        }
        IEnumerator Start()
        {
            yield return null;
            var combat=CombatPlayground.Instance;combat.AutoSpawn=combat.AutoFire=false;combat.Begin();combat.enabled=false;
            var motor=FindObjectOfType<MomentumMotor>();motor.enabled=false;
            var type=typeof(CombatPlayground);
            int money=combat.SharedMoney,purchases=combat.CratesPurchased;
            Vector3 floor=new Vector3(0,0,10);
            int first=combat.SpawnFreeRewardChest(floor,CombatPlayground.UpgradeRarity.Rare);
            Check(first==0 && combat.FreeChestCount==1 && !combat.FreeChestOpened(first),"free chest persists unopened");
            motor.SetSpawn(new Vector3(0,1.1f,-15));
            Check(!combat.TryFreeChest(first),"distant open rejected");
            motor.SetSpawn(combat.FreeChestPosition(first)+Vector3.up*.45f);
            Check(combat.TryFreeChest(first) && combat.FreeChestOpened(first) && combat.ChoosingUpgrade,"nearby open queues upgrade");
            bool rarity=true;
            for (int i=0;i<combat.UpgradeChoices.Count;i++) rarity &= combat.ChoiceRarity(i)==CombatPlayground.UpgradeRarity.Rare;
            Check(rarity && combat.SharedMoney==money && combat.CratesPurchased==purchases,"exact tier with no wallet charge or paid purchase count");
            int rewards=combat.UpgradeCount;
            combat.SkipUpgrade();
            Check(!combat.TryFreeChest(first) && combat.UpgradeCount==rewards && !combat.ChoosingUpgrade,"duplicate free claim rejected");
            int epic=combat.SpawnFreeRewardChest(floor+Vector3.right*5,CombatPlayground.UpgradeRarity.Epic,true);
            motor.SetSpawn(combat.FreeChestPosition(epic)+Vector3.up*.45f);
            Check(combat.TryFreeChest(epic),"guaranteed reward can be claimed");
            rarity=true;for(int i=0;i<combat.UpgradeChoices.Count;i++)rarity &= combat.ChoiceRarity(i)==CombatPlayground.UpgradeRarity.Epic;
            Check(rarity,"challenge chest exact Epic reward");combat.SkipUpgrade();

            // Fill ordinary active capacity, then prove guaranteed rewards still fit.
            combat.ResetFreeChests();
            bool activeCap=true;
            for (int i=0;i<32;i++) activeCap &= combat.SpawnFreeRewardChest(floor,CombatPlayground.UpgradeRarity.Rare)==i;
            activeCap &= combat.SpawnFreeRewardChest(floor,CombatPlayground.UpgradeRarity.Rare)==-1;
            bool reserve=true;
            for (int i=0;i<3;i++) reserve &= combat.SpawnFreeRewardChest(floor,CombatPlayground.UpgradeRarity.Epic,true)==32+i;
            reserve &= combat.SpawnFreeRewardChest(floor,CombatPlayground.UpgradeRarity.Epic,true)==-1;
            Check(activeCap && reserve && combat.FreeChestCount==35,"ordinary cap preserves three challenge slots");
            motor.SetSpawn(combat.FreeChestPosition(0)+Vector3.up*.45f);combat.TryFreeChest(0);combat.SkipUpgrade();
            int next=combat.SpawnFreeRewardChest(floor,CombatPlayground.UpgradeRarity.Rare);
            Check(next==35 && !combat.TryFreeChest(0),"opened IDs never reused when active slot frees");
            var packet=combat.BuildFreeChestSnapshot();
            var copy=JsonUtility.FromJson<CombatPlayground.FreeChestSnapshot>(JsonUtility.ToJson(packet));
            Check(copy.positions.Length==36 && copy.tiers.Length==36 && copy.opened.Length==36 && copy.opened[0] &&
                copy.tiers[32]==(int)CombatPlayground.UpgradeRarity.Epic,"snapshot preserves positions tiers and claim state");

            combat.ResetFreeChests();
            var list=type.GetField("enemies",Private);var kill=type.GetMethod("Kill",Private);
            bool KillVariant(bool boss,bool challenge)
            {
                if (!combat.SpawnEnemy(floor)) return false;
                var enemies=(IList)list.GetValue(combat);var enemy=enemies[enemies.Count-1];var et=enemy.GetType();
                et.GetField("eliteVariant").SetValue(enemy,true);et.GetField("elite").SetValue(enemy,boss);et.GetField("challengeElite").SetValue(enemy,challenge);
                kill.Invoke(combat,new object[]{enemies.Count-1});return true;
            }
            combat.EliteChestDropChance=0;
            Check(KillVariant(false,false) && combat.FreeChestCount==0,"zero drop chance does not award every elite");
            combat.EliteChestDropChance=1;
            Check(KillVariant(false,false) && combat.FreeChestCount==1 && combat.FreeChestTier(0)==CombatPlayground.UpgradeRarity.Rare,"elite death hook produces free Rare chest at forced chance");
            Check(KillVariant(true,false) && KillVariant(false,true) && combat.FreeChestCount==1,"boss and challenge guards do not roll ordinary chest drops");
            combat.EliteChestDropChance=.2f;
            combat.Begin();combat.enabled=false;
            Check(combat.FreeChestCount==0,"new run resets reward chest archive");
            int fresh=combat.SpawnFreeRewardChest(floor,CombatPlayground.UpgradeRarity.Rare);
            Check(fresh==0,"new run starts fresh IDs under new run command revision");
            combat.StopCombat();
            Check(combat.FreeChestCount==0,"stop clears chest state");
            Debug.Log($"SWARM_FREE_CHEST_TEST: {(failed ? "FAIL" : "PASS")}");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
#endif
