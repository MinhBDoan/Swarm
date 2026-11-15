#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using System.IO;
using UnityEngine;
namespace Swarm
{
    public class DownloadSmokeProbe : MonoBehaviour
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-download-test")>=0) new GameObject("Download probe").AddComponent<DownloadSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=120; yield return null;
            var combat=CombatPlayground.Instance; combat.AutoSpawn=combat.AutoFire=false; combat.Begin(); combat.enabled=false;
            var motor=FindObjectOfType<MomentumMotor>(); motor.enabled=false;
            var tick=typeof(CombatPlayground).GetMethod("TickDownload",Flags);
            Action<float> step=dt=>tick.Invoke(combat,new object[]{dt});
            step(1); bool distance=combat.DownloadState==CombatPlayground.DownloadPhase.Available; motor.SetSpawn(CanyonLayout.DownloadCenter+Vector3.up*1.1f);
            step(10); bool start=combat.DownloadState==CombatPlayground.DownloadPhase.Active;
            bool solo=Mathf.Abs(combat.DownloadProgress-50)<.001f && combat.DownloadOccupants==1;
            motor.SetSpawn(CanyonLayout.DownloadCenter+new Vector3(0,-1,0)); bool below=!CombatPlayground.InsideDownloadZone(motor);
            motor.SetSpawn(CanyonLayout.DownloadCenter+Vector3.up*3.08f); bool jump=CombatPlayground.InsideDownloadZone(motor);
            motor.SetSpawn(CanyonLayout.DownloadCenter+Vector3.up*3.5f); bool above=!CombatPlayground.InsideDownloadZone(motor);
            motor.SetSpawn(CanyonLayout.DownloadCenter+new Vector3(5,1.1f,0)); step(2);
            bool grace=Mathf.Abs(combat.DownloadProgress-50)<.001f; step(3);
            bool decay=Mathf.Abs(combat.DownloadProgress-44)<.001f;
            motor.SetSpawn(CanyonLayout.DownloadCenter+Vector3.up*1.1f); step(2);
            bool resume=Mathf.Abs(combat.DownloadProgress-54)<.001f;
            typeof(CombatPlayground).GetProperty("ChoosingUpgrade").SetValue(combat,true); step(5);
            bool pause=Mathf.Abs(combat.DownloadProgress-54)<.001f;
            typeof(CombatPlayground).GetProperty("ChoosingUpgrade").SetValue(combat,false);
            motor.SetSpawn(CanyonLayout.DownloadCenter+new Vector3(5,1.1f,0)); step(100);
            bool floor=combat.DownloadProgress==0;
            bool abandoned=!(bool)typeof(CombatPlayground).GetProperty("DownloadEngaged",Flags).GetValue(combat);
            combat.AutoSpawn=true; step(1); abandoned &= combat.DownloadEnemiesSpawned==0; combat.AutoSpawn=false;
            motor.SetSpawn(CanyonLayout.DownloadCenter+Vector3.up*1.1f); combat.AutoSpawn=true; step(1.6f); combat.AutoSpawn=false;
            bool enemies=combat.DownloadEnemiesSpawned==3 && combat.EnemyCount==3;
            combat.AutoSpawn=true;
            motor.SetSpawn(CanyonLayout.DownloadCenter+new Vector3(5,1.1f,0));
            step(4);
            enemies &= combat.DownloadEnemiesSpawned==3;
            motor.SetSpawn(CanyonLayout.DownloadCenter+Vector3.up*1.1f);
            step(4);
            enemies &= combat.DownloadEnemiesSpawned==6 && combat.EnemyCount==6;
            step(4);
            enemies &= combat.DownloadEnemiesSpawned==6 && combat.EnemyCount==6;
            combat.AutoSpawn=false;
            var args=Environment.GetCommandLineArgs(); int capture=Array.IndexOf(args,"-swarm-download-capture");
            if(capture>=0)
            {
                var camera=Camera.main; camera.GetComponent<OrbitCamera>().enabled=false;
                camera.transform.position=CanyonLayout.StationPoint(9,22,24); camera.transform.LookAt(CanyonLayout.DownloadCenter); camera.fieldOfView=70;
                Screen.SetResolution(1280,720,false); yield return new WaitForSecondsRealtime(.6f);
                Screen.SetResolution(1600,900,false); yield return new WaitForSecondsRealtime(.6f);
                Directory.CreateDirectory(args[capture+1]); typeof(CombatPlayground).GetProperty("DownloadProgress").SetValue(combat,50f);
                combat.enabled=true; yield return new WaitForSecondsRealtime(.3f);
                ScreenCapture.CaptureScreenshot(Path.Combine(args[capture+1],"download-active.png")); yield return new WaitForSecondsRealtime(.4f);
                motor.SetSpawn(CanyonLayout.DownloadCenter+new Vector3(5,1.1f,0)); yield return new WaitForSecondsRealtime(2.5f);
                ScreenCapture.CaptureScreenshot(Path.Combine(args[capture+1],"download-empty.png")); yield return new WaitForSecondsRealtime(.4f); combat.enabled=false;
            }
            motor.SetSpawn(CanyonLayout.DownloadCenter+Vector3.up*1.1f);
            typeof(CombatPlayground).GetProperty("DownloadProgress").SetValue(combat,99f);
            typeof(CombatPlayground).GetField("bonusPending",Flags).SetValue(combat,1); step(1);
            bool reward=combat.DownloadState==CombatPlayground.DownloadPhase.Completed && combat.BonusUpgrade;
            combat.ChooseUpgrade(0); bool queued=combat.BonusUpgrade && combat.ChoosingUpgrade;
            combat.ChooseUpgrade(0); queued &= combat.UpgradeCount==2 && !combat.ChoosingUpgrade;
            step(10); bool once=combat.DownloadState==CombatPlayground.DownloadPhase.Completed && combat.UpgradeCount==2;
            combat.StopCombat(); bool cleanup=combat.DownloadProgress==0 && combat.DownloadEnemiesSpawned==0;
            combat.Begin(); bool reset=combat.DownloadState==CombatPlayground.DownloadPhase.Available; combat.StopCombat();
            bool passed=distance && start && solo && below && jump && above && grace && decay && resume && pause && floor && abandoned && enemies && reward && queued && once && cleanup && reset;
            Debug.Log($"SWARM_DOWNLOAD_TEST: {(passed ? "PASS" : "FAIL")} distance={distance} start={start} soloRate={solo} height={below&&jump&&above} grace={grace} decay={decay} resume={resume} pause={pause} zeroClamp={floor} abandoned={abandoned} pressure={enemies} reward={reward} queued={queued} once={once} cleanup={cleanup} reset={reset}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
