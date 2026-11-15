#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEngine;
namespace Swarm
{
    public class RaritySmokeProbe : MonoBehaviour
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-rarity-test")>=0) new GameObject("Rarity probe").AddComponent<RaritySmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=120; yield return null;
            var combat=CombatPlayground.Instance; combat.AutoSpawn=combat.AutoFire=false; combat.Begin(); combat.enabled=false;
            var motor=FindObjectOfType<MomentumMotor>(); motor.enabled=false;
            var type=typeof(CombatPlayground); var tier=type.GetMethod("ApplyTieredUpgrade",Flags);
            bool stats=true;
            for(int i=0;i<5;i++)
            {
                combat.Begin(); tier.Invoke(combat,new object[]{BoltUpgrade.HeavyBolts,(CombatPlayground.UpgradeRarity)i});
                stats &= Mathf.Abs(combat.Damage-15*(1+CombatPlayground.RarityStatBonus((CombatPlayground.UpgradeRarity)i)))<.001f;
            }
            combat.Begin(); tier.Invoke(combat,new object[]{BoltUpgrade.RocketLauncher,CombatPlayground.UpgradeRarity.Rare});
            tier.Invoke(combat,new object[]{BoltUpgrade.RocketLauncher,CombatPlayground.UpgradeRarity.Common});
            var power=(float[])type.GetField("rarityWeaponPower",Flags).GetValue(combat);
            bool levels=combat.WeaponRank(BoltUpgrade.RocketLauncher)==2 && Mathf.Abs(power[2]-1.375f)<.001f;
            combat.Begin(); tier.Invoke(combat,new object[]{BoltUpgrade.Power,CombatPlayground.UpgradeRarity.Epic});
            bool epic=Mathf.Abs((float)type.GetField("criticalChance",Flags).GetValue(combat)-.2f)<.001f;
            motor.SetSpawn(new Vector3(0,1.1f,-15)); combat.SpawnEnemy(new Vector3(0,0,-9));
            var enemies=(IList)type.GetField("enemies",Flags).GetValue(combat); var enemy=enemies[0];
            enemy.GetType().GetField("health").SetValue(enemy,1000f); type.GetField("criticalChance",Flags).SetValue(combat,1f);
            type.GetMethod("WeaponHit",Flags).Invoke(combat,new object[]{enemy,10f});
            epic &= combat.CriticalHits==1 && (float)enemy.GetType().GetField("health").GetValue(enemy)==980;
            combat.Begin(); motor.SetSpawn(new Vector3(0,1.1f,-15)); tier.Invoke(combat,new object[]{BoltUpgrade.HeavyBolts,CombatPlayground.UpgradeRarity.Legendary});
            combat.SpawnEnemy(new Vector3(0,0,-9)); combat.SpawnEnemy(new Vector3(1,0,-9));
            enemies=(IList)type.GetField("enemies",Flags).GetValue(combat); enemy=enemies[0]; var secondary=enemies[1];
            foreach(var e in enemies) e.GetType().GetField("health").SetValue(e,1000f);
            for(int i=0;i<5;i++) type.GetMethod("WeaponHit",Flags).Invoke(combat,new object[]{enemy,10f});
            bool legendary=combat.ShockwavesTriggered==0;
            type.GetMethod("WeaponHit",Flags).Invoke(combat,new object[]{enemy,10f});
            legendary &= combat.ShockwavesTriggered==1 && (float)secondary.GetType().GetField("health").GetValue(secondary)==995;
            combat.Begin(); type.GetMethod("OpenBonusUpgrade",Flags).Invoke(combat,null);
            bool offers=true; int[] totals=new int[5];
            for(int n=0;n<1000;n++)
            {
                type.GetMethod("RollUpgradeChoices",Flags).Invoke(combat,null);
                offers &= combat.UpgradeChoices.Count==3;
                for(int i=0;i<3;i++) { int rarity=(int)combat.ChoiceRarity(i); offers &= rarity>=0 && rarity<5; totals[rarity]++; }
            }
            foreach(int total in totals) offers &= total>0;
            var rarities=(List<CombatPlayground.UpgradeRarity>)type.GetField("choiceRarities",Flags).GetValue(combat);
            rarities.Clear(); rarities.AddRange(new[]{CombatPlayground.UpgradeRarity.Common,CombatPlayground.UpgradeRarity.Epic,CombatPlayground.UpgradeRarity.Legendary});
            var args=Environment.GetCommandLineArgs(); int capture=Array.IndexOf(args,"-swarm-rarity-capture");
            if(capture>=0)
            {
                var choices=(List<BoltUpgrade>)type.GetField("choices",Flags).GetValue(combat); choices.Clear(); choices.AddRange(new[]{BoltUpgrade.HeavyBolts,BoltUpgrade.RocketLauncher,BoltUpgrade.PiercingBow});
                Screen.SetResolution(1280,720,false); yield return new WaitForSecondsRealtime(.5f);
                Screen.SetResolution(1600,900,false); yield return new WaitForSecondsRealtime(.5f);
                combat.enabled=true; Directory.CreateDirectory(args[capture+1]);
                yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(args[capture+1],"rarity-upgrades.png")); yield return new WaitForSecondsRealtime(.5f); combat.enabled=false;
            }
            float before=combat.Damage; var selected=combat.UpgradeChoices[0]; var picked=combat.ChoiceRarity(0); combat.ChooseUpgrade(0);
            bool choice=combat.UpgradeCount==1;
            if(selected==BoltUpgrade.HeavyBolts) choice &= Mathf.Abs(combat.Damage-before*(1+CombatPlayground.RarityStatBonus(picked)))<.001f;
            combat.Begin(); bool reset=combat.CriticalHits==0 && combat.ShockwavesTriggered==0 && (float)type.GetField("criticalChance",Flags).GetValue(combat)==0;
            combat.StopCombat(); bool passed=stats && levels && epic && legendary && offers && choice && reset;
            Debug.Log($"SWARM_RARITY_TEST: {(passed ? "PASS" : "FAIL")} stats={stats} separateLevels={levels} epicCrit={epic} legendaryShock={legendary} offers={offers} choice={choice} reset={reset} distribution={string.Join(",",totals)}"); Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
