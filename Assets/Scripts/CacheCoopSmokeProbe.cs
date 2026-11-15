#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
namespace Swarm
{
    public class CacheCoopSmokeProbe : MonoBehaviour
    {
        bool failed; const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        void Check(bool ok,string label) { failed|=!ok; Debug.Log($"SWARM_CACHECOOP_TRACE: {label}={ok}"); }
        IEnumerator Wait(Func<bool> condition)
        {
            float until=Time.realtimeSinceStartup+18;
            while (!condition() && Time.realtimeSinceStartup<until) yield return null;
            Check(condition(),"wait completed");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-cachecoop-role")>=0)
                new GameObject("Cache co-op probe").AddComponent<CacheCoopSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60; yield return null;
            var args=Environment.GetCommandLineArgs(); bool host=args[Array.IndexOf(args,"-swarm-cachecoop-role")+1]=="host";
            int count=2, arg=Array.IndexOf(args,"-swarm-cachecoop-count"); if (arg>=0) count=int.Parse(args[arg+1]);
            var session=FindObjectOfType<MultiplayerSession>(); session.Begin(host);
            yield return Wait(()=>NetworkManager.Singleton.IsConnectedClient && NetworkManager.Singleton.LocalClient.PlayerObject);
            var motor=NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<MomentumMotor>(); motor.enabled=false;
            var combat=CombatPlayground.Instance;
            if (host)
            {
                yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==count);
                combat.AutoSpawn=combat.AutoFire=false; combat.Begin();
                motor.SetSpawn(CanyonLayout.CachePositions[0]+Vector3.up*.35f);
                yield return Wait(()=>combat.CacheState==CombatPlayground.CacheChallengeState.Active);
                Check(combat.CachePosition==CanyonLayout.CachePositions[1] && combat.CacheGuardsRemaining==4,"remote activated station cache");
                Check(!combat.StartCacheChallenge(),"second challenge blocked while active");
                yield return new WaitForSecondsRealtime(1);
                ClearGuards(combat);
                yield return Wait(()=>combat.BonusUpgrade);
                Check(combat.CacheState==CombatPlayground.CacheChallengeState.Completed,"station completion");
                combat.ChooseUpgrade(0);
                yield return Wait(()=>!combat.PartyPaused && AllUpgrades(combat,1));
                Check(combat.StartCacheChallenge(),"basin cache still available");
                yield return new WaitForSecondsRealtime(1);
                ClearGuards(combat);
                yield return Wait(()=>combat.BonusUpgrade); combat.ChooseUpgrade(0);
                yield return Wait(()=>!combat.PartyPaused && AllUpgrades(combat,2));
                Check(!combat.StartCacheChallenge(),"basin reward only once");
                motor.SetSpawn(CanyonLayout.CachePositions[1]+Vector3.up*.35f);
                Check(!combat.StartCacheChallenge(),"station reward only once");
                yield return new WaitForSecondsRealtime(.7f); combat.StopCombat();
                yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==1);
            }
            else
            {
                yield return Wait(()=>combat.Running);
                motor.SetSpawn(CanyonLayout.CachePositions[1]+Vector3.up*.35f);
                yield return new WaitForSecondsRealtime(.8f);
                if (NetworkManager.Singleton.LocalClientId==1) Check(combat.StartCacheChallenge(),"client requested station cache");
                yield return Wait(()=>combat.CacheState==CombatPlayground.CacheChallengeState.Active);
                Check(combat.CachePosition==CanyonLayout.CachePositions[1] && combat.CacheGuardsRemaining==4,"station state replicated");
                yield return Wait(()=>combat.BonusUpgrade);
                Check(combat.CacheState==CombatPlayground.CacheChallengeState.Completed,"station completion replicated");
                combat.ChooseUpgrade(0);
                yield return Wait(()=>!combat.ChoosingUpgrade);
                Check(!combat.StartCacheChallenge(),"remote cannot reuse station");
                yield return Wait(()=>combat.CacheState==CombatPlayground.CacheChallengeState.Active && combat.CachePosition==CanyonLayout.CachePositions[0]);
                yield return Wait(()=>combat.BonusUpgrade);
                Check(combat.UpgradeCount==1,"independent second reward"); combat.ChooseUpgrade(0);
                yield return Wait(()=>combat.UpgradeCount==2);
                Check(!combat.StartCacheChallenge(),"remote station remains spent after basin reward");
                yield return Wait(()=>!combat.Running);
            }
            session.Leave(); yield return new WaitForSecondsRealtime(.5f);
            Debug.Log($"SWARM_CACHECOOP_TEST: {(failed ? "FAIL" : "PASS")} role={(host ? "host" : "client")}"); Application.Quit(failed ? 1 : 0);
        }
        void ClearGuards(CombatPlayground combat)
        {
            for (int i=combat.EnemyCount-1;i>=0;i--) typeof(CombatPlayground).GetMethod("Kill",Flags).Invoke(combat,new object[]{i});
            // Isolate cache rewards from normal XP rewards, tested by the combat probe.
            var orbs=(IList)typeof(CombatPlayground).GetField("orbs",Flags).GetValue(combat);
            foreach (var orb in orbs) Destroy((GameObject)orb.GetType().GetField("body").GetValue(orb));
            orbs.Clear();
        }
        bool AllUpgrades(CombatPlayground combat,int count)
        {
            foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
                if (combat.PlayerState(id)==null || combat.PlayerState(id).upgrades!=count) return false;
            return true;
        }
    }
}
#endif
