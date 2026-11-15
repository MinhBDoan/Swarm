#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
namespace Swarm
{
    public class DownloadCoopSmokeProbe : MonoBehaviour
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic; bool failed;
        void Check(bool ok,string label) { failed|=!ok; Debug.Log($"SWARM_DOWNLOADCOOP_TRACE: {label}={ok}"); }
        IEnumerator Wait(Func<bool> condition)
        {
            float until=Time.realtimeSinceStartup+22;
            while(!condition() && Time.realtimeSinceStartup<until) yield return null;
            Check(condition(),"condition completed");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-downloadcoop-role")>=0) new GameObject("Download co-op probe").AddComponent<DownloadCoopSmokeProbe>();
        }
        void Health(CombatPlayground combat,ulong id,float value)
        {
            typeof(CombatPlayground).GetMethod("SwitchContext",Flags).Invoke(combat,new object[]{id});
            typeof(CombatPlayground).GetProperty("Health").SetValue(combat,value);
            typeof(CombatPlayground).GetMethod("SwitchContext",Flags).Invoke(combat,new object[]{0ul});
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60; yield return null;
            var args=Environment.GetCommandLineArgs(); bool host=args[Array.IndexOf(args,"-swarm-downloadcoop-role")+1]=="host";
            var session=FindObjectOfType<MultiplayerSession>(); session.Begin(host);
            yield return Wait(()=>NetworkManager.Singleton.IsConnectedClient && NetworkManager.Singleton.LocalClient.PlayerObject);
            var motor=NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<MomentumMotor>(); motor.enabled=false;
            var combat=CombatPlayground.Instance; Vector3 inside=CanyonLayout.DownloadCenter+Vector3.up*1.1f,outside=inside+Vector3.back*10;
            if(host)
            {
                yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==6);
                combat.AutoSpawn=combat.AutoFire=false; combat.Begin();
                yield return Wait(()=>combat.DownloadState==CombatPlayground.DownloadPhase.Active && combat.DownloadOccupants==5);
                Check(Mathf.Abs(combat.DownloadSpeedMultiplier-1.4f)<.001f,"five players at1.4x");
                motor.SetSpawn(inside); yield return Wait(()=>combat.DownloadOccupants==6);
                Check(Mathf.Abs(combat.DownloadSpeedMultiplier-1.5f)<.001f,"six players at1.5x");
                yield return Wait(()=>combat.DownloadProgress>15);
                typeof(CombatPlayground).GetProperty("DownloadProgress").SetValue(combat,50f); motor.SetSpawn(outside);
                yield return Wait(()=>combat.DownloadOccupants==0); float progress=combat.DownloadProgress;
                yield return new WaitForSecondsRealtime(1); Check(Mathf.Abs(combat.DownloadProgress-progress)<.03f,"empty grace replicated");
                yield return new WaitForSecondsRealtime(1.7f); Check(combat.DownloadProgress<progress-.7f && combat.DownloadProgress>progress-4,"slow empty decay");
                typeof(CombatPlayground).GetProperty("DownloadProgress").SetValue(combat,45f);
                yield return Wait(()=>combat.DownloadOccupants==1); Check(combat.DownloadSpeedMultiplier==1,"single returning player resumes");
                Health(combat,1,0); yield return Wait(()=>combat.DownloadOccupants==0);
                Check(combat.DownloadOccupants==0,"downed occupant excluded"); Health(combat,1,100);
                yield return Wait(()=>combat.DownloadOccupants==1);
                Health(combat,5,0);
                typeof(CombatPlayground).GetProperty("DownloadProgress").SetValue(combat,99.99f);
                yield return Wait(()=>combat.DownloadState==CombatPlayground.DownloadPhase.Completed && combat.BonusUpgrade);
                Check(combat.PlayerState(5).pending==1 && combat.PlayerState(5).pendingTiers[0]==2,"downed distant player retains reward");
                typeof(CombatPlayground).GetMethod("ReviveParty",Flags).Invoke(combat,null);
                Check(!CombatPlayground.InsideDownloadZone(motor),"host outside at completion");
                combat.ChooseUpgrade(0);
                yield return Wait(()=>AllUpgrades(combat));
                yield return new WaitForSecondsRealtime(.5f); Check(combat.DownloadState==CombatPlayground.DownloadPhase.Completed,"once per run"); combat.StopCombat();
                yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==1);
            }
            else
            {
                yield return Wait(()=>combat.Running); motor.SetSpawn(inside);
                yield return Wait(()=>combat.DownloadState==CombatPlayground.DownloadPhase.Active);
                yield return Wait(()=>combat.DownloadProgress>=49); motor.SetSpawn(outside);
                if(NetworkManager.Singleton.LocalClientId==1)
                {
                    yield return Wait(()=>combat.DownloadProgress<46); motor.SetSpawn(inside);
                }
                yield return Wait(()=>combat.DownloadState==CombatPlayground.DownloadPhase.Completed && combat.BonusUpgrade);
                if(NetworkManager.Singleton.LocalClientId!=1) Check(!CombatPlayground.InsideDownloadZone(motor),"outside player gets upgrade");
                for(int offer=0;offer<combat.UpgradeChoices.Count;offer++) Check(combat.ChoiceRarity(offer)==CombatPlayground.UpgradeRarity.Rare,"guaranteed rare reward");
                Check(combat.DownloadProgress==100,"completion replicated"); combat.ChooseUpgrade(0);
                yield return Wait(()=>combat.UpgradeCount==1); yield return Wait(()=>!combat.Running);
            }
            session.Leave(); yield return new WaitForSecondsRealtime(.4f);
            Debug.Log($"SWARM_DOWNLOADCOOP_TEST: {(failed ? "FAIL" : "PASS")} role={(host ? "host" : "client")}"); Application.Quit(failed ? 1 : 0);
        }
        bool AllUpgrades(CombatPlayground combat)
        {
            foreach(ulong id in NetworkManager.Singleton.ConnectedClientsIds) if(combat.PlayerState(id)==null || combat.PlayerState(id).upgrades!=1) return false;
            return !combat.PartyPaused;
        }
    }
}
#endif
