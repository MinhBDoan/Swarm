#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
namespace Swarm
{
    public class HudCaptureProbe : MonoBehaviour
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-hud-capture")>=0)
                new GameObject("HUD capture probe").AddComponent<HudCaptureProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60;
            var args=Environment.GetCommandLineArgs(); string folder=args[Array.IndexOf(args,"-swarm-hud-capture")+1]; Directory.CreateDirectory(folder);
            yield return null;
            var combat=CombatPlayground.Instance;
            combat.AutoSpawn=false; combat.Begin();
            var motor=FindObjectOfType<MomentumMotor>(); motor.SetSpawn(new Vector3(0,1.1f,-15)); motor.enabled=false;
            var camera=Camera.main; camera.GetComponent<OrbitCamera>().enabled=false;
            camera.transform.position=new Vector3(10,11,-28); camera.transform.LookAt(new Vector3(0,1,-1)); camera.fieldOfView=68;
            var type=typeof(CombatPlayground); var apply=type.GetMethod("ApplyUpgrade",Flags);
            foreach (var weapon in new[] { BoltUpgrade.Boomerang,BoltUpgrade.PulseField,BoltUpgrade.RocketLauncher })
                for (int i=0;i<2;i++) apply.Invoke(combat,new object[] { weapon });
            for (int i=0;i<9;i++) combat.SpawnEnemy(new Vector3(-8+(i%3)*6,0,-7+(i/3)*4),false,(EnemyKind)(i%3));
            var enemies=(IList)type.GetField("enemies",Flags).GetValue(combat);
            foreach (var enemy in enemies) enemy.GetType().GetField("health").SetValue(enemy,10000f);
            foreach (var agent in FindObjectsOfType<NavMeshAgent>()) { agent.speed=0; agent.enabled=false; }
            type.GetProperty("Health").SetValue(combat,76f); type.GetProperty("Level").SetValue(combat,4);
            type.GetField("xp",Flags).SetValue(combat,11); type.GetField("elapsed",Flags).SetValue(combat,92f);
            type.GetProperty("Wave").SetValue(combat,2);
            Screen.SetResolution(1280,720,false);
            yield return new WaitForSecondsRealtime(1);
            Screen.SetResolution(1600,900,false);
            yield return new WaitForSecondsRealtime(2);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"combat-1600.png"));
            yield return new WaitForSecondsRealtime(1);
            type.GetField("elapsed",Flags).SetValue(combat,625f);
            yield return new WaitForSecondsRealtime(.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"overtime-1600.png"));
            yield return new WaitForSecondsRealtime(.5f);
            combat.SetLocalMenuOpen(true);
            yield return new WaitForSecondsRealtime(.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"menu-pause-1600.png"));
            yield return new WaitForSecondsRealtime(.5f);
            combat.ResumeLocalMenu();
            type.GetField("elapsed",Flags).SetValue(combat,92f);
            combat.AutoFire=false;
            var weapons=(Dictionary<BoltUpgrade,int>)type.GetField("weapons",Flags).GetValue(combat); weapons.Remove(BoltUpgrade.RocketLauncher);
            type.GetProperty("ChoosingUpgrade").SetValue(combat,true);
            var choices=(List<BoltUpgrade>)type.GetField("choices",Flags).GetValue(combat);
            choices.Clear(); choices.AddRange(new[] { BoltUpgrade.PiercingBow,BoltUpgrade.RocketLauncher,BoltUpgrade.Recovery });
            yield return new WaitForSecondsRealtime(.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"upgrades-1600.png"));
            yield return new WaitForSecondsRealtime(.5f);
            Screen.SetResolution(1280,720,false); yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"upgrades-1280.png"));
            yield return new WaitForSecondsRealtime(.5f);
            Screen.SetResolution(1920,1080,false); yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"upgrades-1920.png"));
            yield return new WaitForSecondsRealtime(1);
            Debug.Log("SWARM_HUD_CAPTURE: saved gameplay and upgrade screenshots"); Application.Quit(0);
        }
    }
}
#endif
