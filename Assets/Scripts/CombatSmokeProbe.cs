#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;

namespace Swarm
{
    // Exercises actual navigation, targeting, projectile hits, drops, and collection.
    public class CombatSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swarm-combat-test") >= 0)
                new GameObject("Combat Smoke Probe").AddComponent<CombatSmokeProbe>();
        }
        IEnumerator Start()
        {
            yield return null;
            var combat = CombatPlayground.Instance;
            combat.AutoSpawn = false;
            combat.Begin();
            var motor = FindObjectOfType<MomentumMotor>();
            motor.SetSpawn(new Vector3(0,1.1f,-15)); motor.enabled = false;
            for (int i=0; i<3; i++)
            {
                combat.SpawnEnemy(new Vector3(0,0,-9));
                float deadline = Time.realtimeSinceStartup + 6;
                while (combat.Kills <= i && Time.realtimeSinceStartup < deadline) yield return null;
            }
            float limit = Time.realtimeSinceStartup + 6;
            while (!combat.ChoosingUpgrade && Time.realtimeSinceStartup < limit) yield return null;
            bool loop = combat.Kills == 3 && combat.TotalXP == 9 && combat.Level == 2 && combat.ChoosingUpgrade;
            float before = combat.Damage;
            combat.ChooseUpgrade(0);
            bool upgrade = combat.UpgradeCount == 1 && !combat.ChoosingUpgrade &&
                (combat.UpgradeChoices.Count == 0);
            combat.AutoFire = false;
            combat.SpawnEnemy(new Vector3(0,0,-13));
            limit = Time.realtimeSinceStartup + 5;
            while (combat.Health >= 100 && Time.realtimeSinceStartup < limit) yield return null;
            bool damage = combat.Health < 100;
            limit = Time.realtimeSinceStartup + 15;
            while (combat.Health > 0 && Time.realtimeSinceStartup < limit) yield return null;
            bool defeat = combat.Health == 0;
            combat.Begin();
            bool reset = combat.Health == 100 && combat.Kills == 0 && combat.Level == 1 && combat.ProjectileCount == 1;
            combat.StopCombat();
            bool cleanup = !combat.Running && combat.EnemyCount == 0 && combat.OrbCount == 0;
            combat.AutoSpawn = combat.AutoFire = true;
            combat.Begin();
            bool openingPack = combat.EnemyCount == 3;
            yield return new WaitForSecondsRealtime(2);
            bool openingFire = combat.ShotsFired > 0;
            combat.StopCombat();
            bool passed = loop && upgrade && damage && defeat && reset && cleanup && openingPack && openingFire;
            Debug.Log($"SWARM_COMBAT_TEST: {(passed ? "PASS" : "FAIL")} loop={loop} upgrade={upgrade} damage={damage} defeat={defeat} reset={reset} cleanup={cleanup} openingPack={openingPack} openingFire={openingFire}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
