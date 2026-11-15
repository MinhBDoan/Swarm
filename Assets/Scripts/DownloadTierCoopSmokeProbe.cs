#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
namespace Swarm
{
    public class DownloadTierCoopSmokeProbe : MonoBehaviour
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic; bool failed;
        void Check(bool ok,string label) {failed|=!ok;Debug.Log($"SWARM_DOWNLOADTIERCOOP_TRACE: {label}={ok}");}
        IEnumerator Wait(Func<bool> condition) {float until=Time.realtimeSinceStartup+15;while(!condition() && Time.realtimeSinceStartup<until) yield return null;Check(condition(),"wait completed");}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap() {if(Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-download-tiercoop-role")>=0)new GameObject("Download tier co-op probe").AddComponent<DownloadTierCoopSmokeProbe>();}
        IEnumerator Start()
        {
            Application.targetFrameRate=60;yield return null;var args=Environment.GetCommandLineArgs();bool host=args[Array.IndexOf(args,"-swarm-download-tiercoop-role")+1]=="host";
            var session=FindObjectOfType<MultiplayerSession>();session.Begin(host);yield return Wait(()=>NetworkManager.Singleton.IsConnectedClient && NetworkManager.Singleton.LocalClient.PlayerObject);
            var motor=NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<MomentumMotor>();motor.enabled=false;var combat=CombatPlayground.Instance;
            if(host)
            {
                yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==2);combat.AutoSpawn=combat.AutoFire=false;combat.Begin();
                for(int i=0;i<5;i++)
                {
                    int index=i;motor.SetSpawn(CanyonLayout.DownloadCenters[i]+Vector3.up*1.1f);yield return Wait(()=>combat.SelectedDownload==index && combat.DownloadState==CombatPlayground.DownloadPhase.Active);
                    typeof(CombatPlayground).GetProperty("DownloadProgress").SetValue(combat,99.99f);
                    yield return Wait(()=>combat.BonusUpgrade && combat.DownloadStateAt(index)==CombatPlayground.DownloadPhase.Completed);
                    for(int choice=0;choice<combat.UpgradeChoices.Count;choice++) Check((int)combat.ChoiceRarity(choice)==i,"host exact tier");
                    combat.ChooseUpgrade(0);yield return Wait(()=>combat.PlayerState(1).upgrades==index+1 && !combat.PartyPaused);
                }
                yield return new WaitForSecondsRealtime(.5f);combat.StopCombat();yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==1);
            }
            else
            {
                yield return Wait(()=>combat.Running);
                for(int i=0;i<5;i++)
                {
                    int index=i;yield return Wait(()=>combat.BonusUpgrade && combat.DownloadStateAt(index)==CombatPlayground.DownloadPhase.Completed);
                    Check(combat.SelectedDownload==i,"selected index replicated");
                    for(int choice=0;choice<combat.UpgradeChoices.Count;choice++) Check((int)combat.ChoiceRarity(choice)==i,"outside player exact tier");
                    for(int old=0;old<=i;old++) Check(combat.DownloadStateAt(old)==CombatPlayground.DownloadPhase.Completed,"completed archive replicated");
                    combat.ChooseUpgrade(0);yield return Wait(()=>combat.UpgradeCount==index+1);
                }
                yield return Wait(()=>!combat.Running);
            }
            session.Leave();yield return new WaitForSecondsRealtime(.4f);Debug.Log($"SWARM_DOWNLOADTIERCOOP_TEST: {(failed ? "FAIL" : "PASS")} role={(host ? "host" : "client")}");Application.Quit(failed ? 1 : 0);
        }
    }
}
#endif
