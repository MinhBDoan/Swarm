#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using System.IO;
using UnityEngine;
namespace Swarm
{
    public class EconomySmokeProbe : MonoBehaviour
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-economy-test")>=0)new GameObject("Economy probe").AddComponent<EconomySmokeProbe>();}
        IEnumerator Start()
        {
            Application.targetFrameRate=120;yield return null;var combat=CombatPlayground.Instance;combat.AutoSpawn=combat.AutoFire=false;combat.Begin();combat.enabled=false;
            var motor=FindObjectOfType<MomentumMotor>();motor.enabled=false;
            bool distance=!combat.TrySupplyInteraction(0);motor.SetSpawn(CombatPlayground.MoneyCrateFloors[0]+Vector3.up*1.1f);
            combat.TrySupplyInteraction(0);bool insufficient=combat.SharedMoney==0 && !combat.MoneyCrateOpened(0) && !combat.ChoosingUpgrade;
            bool salvage=true;
            for(int i=0;i<3;i++){motor.SetSpawn(CombatPlayground.SalvageFloors[i]+Vector3.up*1.1f);salvage &= combat.TrySupplyInteraction(i+4) && combat.SalvageBroken(i) && !combat.TrySupplyInteraction(i+4);}
            salvage &= combat.SharedMoney==30;
            motor.SetSpawn(CombatPlayground.MoneyCrateFloors[0]+Vector3.up*1.1f);bool rare=combat.TrySupplyInteraction(0) && combat.SharedMoney==0 && combat.CratesPurchased==1;
            for(int i=0;i<combat.UpgradeChoices.Count;i++)rare &= combat.ChoiceRarity(i)==CombatPlayground.UpgradeRarity.Rare;
            combat.RerollUpgrades();for(int i=0;i<combat.UpgradeChoices.Count;i++)rare &= combat.ChoiceRarity(i)==CombatPlayground.UpgradeRarity.Rare;
            combat.ChooseUpgrade(0);bool duplicate=!combat.TrySupplyInteraction(0) && combat.SharedMoney==0;
            var kill=typeof(CombatPlayground).GetMethod("Kill",Flags);
            bool bounty=combat.SpawnEnemy(new Vector3(-22,5.5f,31));kill.Invoke(combat,new object[]{0});bounty &= combat.SharedMoney==5;
            bounty &= combat.SpawnEnemy(new Vector3(-22,5.5f,31),true);kill.Invoke(combat,new object[]{0});bounty &= combat.SharedMoney==45;
            for(int i=3;i<6;i++){motor.SetSpawn(CombatPlayground.SalvageFloors[i]+Vector3.up*1.1f);salvage &= combat.TrySupplyInteraction(i+4);}
            motor.SetSpawn(CombatPlayground.MoneyCrateFloors[2]+Vector3.up*1.1f);
            var args=Environment.GetCommandLineArgs();int capture=Array.IndexOf(args,"-swarm-economy-capture");
            if(capture>=0)
            {
                var camera=Camera.main;camera.GetComponent<OrbitCamera>().enabled=false;camera.transform.position=new Vector3(11,17,24);camera.transform.LookAt(CombatPlayground.SupplyPosition(2));camera.fieldOfView=70;
                Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.6f);Screen.SetResolution(1600,900,false);yield return new WaitForSecondsRealtime(.6f);
                int previousMode=PlayerPrefs.GetInt("Swarm.LegacyCombatHud",0);
                PlayerPrefs.SetInt("Swarm.LegacyCombatHud",0);
                var toggle=typeof(CombatPlayground).GetMethod("ToggleCombatHud",Flags);
                combat.enabled=true;
                Directory.CreateDirectory(args[capture+1]);
                yield return new WaitForSecondsRealtime(.5f);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(args[capture+1],"paid-supply-crate.png"));
                yield return new WaitForSecondsRealtime(.4f);
                toggle.Invoke(combat,null);
                yield return new WaitForSecondsRealtime(.4f);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(args[capture+1],"classic-combat-hud.png"));
                yield return new WaitForSecondsRealtime(.4f);
                PlayerPrefs.SetInt("Swarm.LegacyCombatHud",previousMode);
                PlayerPrefs.Save();
                combat.enabled=false;
            }
            bool epic=combat.SharedMoney==75 && combat.TrySupplyInteraction(2) && combat.SharedMoney==0 && combat.CratesPurchased==2;
            for(int i=0;i<combat.UpgradeChoices.Count;i++)epic &= combat.ChoiceRarity(i)==CombatPlayground.UpgradeRarity.Epic;
            combat.ChooseUpgrade(0);epic &= combat.UpgradeCount==2;
            combat.StopCombat();bool reset=combat.SharedMoney==0 && combat.CratesPurchased==0 && !combat.MoneyCrateOpened(0) && !combat.SalvageBroken(0);
            bool passed=distance && insufficient && salvage && rare && duplicate && bounty && epic && reset;
            Debug.Log($"SWARM_ECONOMY_TEST: {(passed ? "PASS" : "FAIL")} distance={distance} insufficient={insufficient} salvage={salvage} rare={rare} duplicate={duplicate} bounty={bounty} epic={epic} reset={reset}");Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
