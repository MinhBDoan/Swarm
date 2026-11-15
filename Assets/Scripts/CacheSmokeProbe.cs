#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Swarm
{
    public class CacheSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-cache-test") >= 0)
                new GameObject("Cache Smoke Probe").AddComponent<CacheSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            yield return null;
            var combat = CombatPlayground.Instance;
            combat.AutoSpawn = false; combat.Begin();
            var motor = FindObjectOfType<MomentumMotor>(); motor.enabled = false;
            motor.SetSpawn(new Vector3(0,1.1f,-15));
            bool distanceGate = !combat.StartCacheChallenge();
            motor.SetSpawn(combat.CachePosition+Vector3.up*.35f);
            bool activated = combat.StartCacheChallenge() && combat.CacheGuardsRemaining == 4 && !combat.StartCacheChallenge();
            typeof(CombatPlayground).GetProperty("Damage").SetValue(combat,500f);
            typeof(CombatPlayground).GetProperty("FireInterval").SetValue(combat,.03f);
            float deadline = Time.realtimeSinceStartup+12;
            while (Time.realtimeSinceStartup < deadline && !(combat.CacheState == CombatPlayground.CacheChallengeState.Completed && combat.BonusUpgrade))
            {
                if (combat.ChoosingUpgrade) combat.ChooseUpgrade(0);
                yield return null;
            }
            bool completed = combat.CacheState == CombatPlayground.CacheChallengeState.Completed && combat.BonusUpgrade;
            int upgrades = combat.UpgradeCount, level = combat.Level;
            combat.ChooseUpgrade(0);
            bool reward = completed && combat.UpgradeCount == upgrades+1 && combat.Level == level && !combat.BonusUpgrade;
            bool once = !combat.StartCacheChallenge();

            motor.SetSpawn(CanyonLayout.CachePositions[1]+Vector3.up*.35f);
            bool second=combat.StartCacheChallenge() && combat.CacheGuardsRemaining==4;
            deadline=Time.realtimeSinceStartup+14;
            while (Time.realtimeSinceStartup<deadline && !(combat.CacheState==CombatPlayground.CacheChallengeState.Completed && combat.BonusUpgrade))
            {
                if (combat.ChoosingUpgrade) combat.ChooseUpgrade(0);
                yield return null;
            }
            second &= combat.CacheState==CombatPlayground.CacheChallengeState.Completed && combat.BonusUpgrade;
            while (combat.ChoosingUpgrade) combat.ChooseUpgrade(0);
            second &= !combat.StartCacheChallenge();
            motor.SetSpawn(CanyonLayout.CachePositions[0]+Vector3.up*.35f);
            bool bothOnce=!combat.StartCacheChallenge();
            Debug.Log($"SWARM_STATION_CACHE_TEST: secondReward={second} bothOnce={bothOnce}");
            motor.SetSpawn(CanyonLayout.CachePositions[2]+Vector3.up*.35f);
            bool flats=combat.StartCacheChallenge() && combat.CacheGuardsRemaining==4;
            deadline=Time.realtimeSinceStartup+14;
            while (Time.realtimeSinceStartup<deadline && !(combat.CacheState==CombatPlayground.CacheChallengeState.Completed && combat.BonusUpgrade))
            {
                if (combat.ChoosingUpgrade) combat.ChooseUpgrade(0);
                yield return null;
            }
            flats &= combat.CacheState==CombatPlayground.CacheChallengeState.Completed && combat.BonusUpgrade;
            while (combat.ChoosingUpgrade) combat.ChooseUpgrade(0);
            flats &= !combat.StartCacheChallenge();
            combat.AutoFire = false; combat.Begin(); motor.SetSpawn(combat.CachePosition+Vector3.up*.35f);
            combat.StartCacheChallenge();
            var type = typeof(CombatPlayground);
            type.GetField("xp",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(combat,8);
            type.GetMethod("OpenUpgrade",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(combat,null);
            float seconds = combat.CacheSecondsRemaining;
            yield return new WaitForSecondsRealtime(.25f);
            bool paused = combat.ChoosingUpgrade && Mathf.Abs(combat.CacheSecondsRemaining-seconds)<.001f;
            combat.ChooseUpgrade(0);
            type.GetProperty("CacheSecondsRemaining").SetValue(combat,.001f);
            yield return new WaitForSecondsRealtime(.1f);
            bool failure = combat.CacheState == CombatPlayground.CacheChallengeState.Failed && combat.Running && combat.CacheGuardsRemaining == 4 && !combat.BonusUpgrade;
            combat.StopCombat();
            bool cleanup = combat.EnemyCount == 0 && combat.CacheGuardsRemaining == 0 && !GameObject.Find("Optional supply cache");
            combat.Begin();
            bool reset = combat.CacheState == CombatPlayground.CacheChallengeState.Available && !combat.BonusUpgrade && combat.CacheGuardsRemaining == 0;
            combat.StopCombat();
            bool passed = flats && second && bothOnce && distanceGate && activated && completed && reward && once && paused && failure && cleanup && reset;
            Debug.Log($"SWARM_CACHE_TEST: {(passed ? "PASS" : "FAIL")} flats={flats} distance={distanceGate} activate={activated} complete={completed} reward={reward} once={once} pause={paused} failure={failure} cleanup={cleanup} reset={reset}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
