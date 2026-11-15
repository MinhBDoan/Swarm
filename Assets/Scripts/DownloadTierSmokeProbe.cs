#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using System.IO;
using UnityEngine;
namespace Swarm
{
    public class DownloadTierSmokeProbe : MonoBehaviour
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap() {if(Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-download-tier-test")>=0) new GameObject("Download tier probe").AddComponent<DownloadTierSmokeProbe>();}
        IEnumerator Start()
        {
            Application.targetFrameRate=120; yield return null;
            var combat=CombatPlayground.Instance; combat.AutoSpawn=combat.AutoFire=false; combat.Begin(); combat.enabled=false;
            var motor=FindObjectOfType<MomentumMotor>(); motor.enabled=false;
            var tick=typeof(CombatPlayground).GetMethod("TickDownload",Flags); Action<float> step=dt=>tick.Invoke(combat,new object[]{dt});
            bool rates=true,rewards=true,archive=true,once=true,concurrent=true;
            for(int i=0;i<5;i++)
            {
                motor.SetSpawn(CanyonLayout.DownloadCenters[i]+Vector3.up*1.1f);
                step(CanyonLayout.DownloadSeconds[i]/2); rates &= combat.SelectedDownload==i && Mathf.Abs(combat.DownloadProgress-50)<.01f;
                motor.SetSpawn(CanyonLayout.DownloadCenters[(i+1)%5]+Vector3.up*1.1f);
                step(3); concurrent &= combat.SelectedDownload==i; rates &= Mathf.Abs(combat.DownloadProgress-48)<.01f;
                motor.SetSpawn(CanyonLayout.DownloadCenters[i]+Vector3.up*1.1f); step(CanyonLayout.DownloadSeconds[i]);
                for(int choice=0;choice<combat.UpgradeChoices.Count;choice++) rewards &= (int)combat.ChoiceRarity(choice)==i;
                typeof(CombatPlayground).GetProperty("RerollsRemaining").SetValue(combat,3); combat.RerollUpgrades();
                for(int choice=0;choice<combat.UpgradeChoices.Count;choice++) rewards &= (int)combat.ChoiceRarity(choice)==i;
                combat.ChooseUpgrade(0); step(1); once &= combat.DownloadStateAt(i)==CombatPlayground.DownloadPhase.Completed;
                for(int old=0;old<=i;old++) archive &= combat.DownloadStateAt(old)==CombatPlayground.DownloadPhase.Completed && combat.DownloadProgressAt(old)==100;
            }
            bool pressure=true;
            for(int i=0;i<5;i++)
            {
                combat.Begin(); motor.SetSpawn(CanyonLayout.DownloadCenters[i]+Vector3.up*1.1f); combat.AutoSpawn=true; step(1.6f); combat.AutoSpawn=false;
                bool spawned=combat.DownloadEnemiesSpawned==3; pressure &= spawned;
                Debug.Log($"SWARM_DOWNLOAD_TIER_TRACE: index={i} seconds={CanyonLayout.DownloadSeconds[i]} guards={spawned}");
            }
            var args=Environment.GetCommandLineArgs(); int capture=Array.IndexOf(args,"-swarm-download-tier-capture");
            if(capture>=0)
            {
                var camera=Camera.main; camera.GetComponent<OrbitCamera>().enabled=false;
                camera.transform.position=CanyonLayout.StationPoint(-15,21,-26); camera.transform.LookAt(CanyonLayout.DownloadCenters[4]); camera.fieldOfView=70;
                Screen.SetResolution(1280,720,false); yield return new WaitForSecondsRealtime(.6f);
                Screen.SetResolution(1600,900,false); yield return new WaitForSecondsRealtime(.6f);
                combat.enabled=true; Directory.CreateDirectory(args[capture+1]);
                yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(args[capture+1],"legendary-terminal.png")); yield return new WaitForSecondsRealtime(.5f); combat.enabled=false;
            }
            combat.StopCombat(); bool reset=true; for(int i=0;i<5;i++) reset &= combat.DownloadStateAt(i)==CombatPlayground.DownloadPhase.Available;
            bool passed=rates && rewards && archive && once && concurrent && pressure && reset;
            Debug.Log($"SWARM_DOWNLOAD_TIER_TEST: {(passed ? "PASS" : "FAIL")} rates={rates} exactRewards={rewards} archived={archive} once={once} concurrent={concurrent} pressure={pressure} reset={reset}"); Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
