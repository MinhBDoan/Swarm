#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace Swarm
{
    // Single-volley integration scenarios test the real projectile simulation.
    public class BuildSmokeProbe : MonoBehaviour
    {
        CombatPlayground combat;
        MomentumMotor motor;
        MethodInfo apply;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-build-test") >= 0)
                new GameObject("Build Smoke Probe").AddComponent<BuildSmokeProbe>();
        }
        void Setup(params BoltUpgrade[] upgrades)
        {
            combat.AutoSpawn = false; combat.AutoFire = true; combat.Begin();
            motor.SetSpawn(new Vector3(0,1.1f,-15)); motor.enabled = false;
            typeof(CombatPlayground).GetProperty("Damage").SetValue(combat,100f);
            typeof(CombatPlayground).GetProperty("FireInterval").SetValue(combat,100f);
            foreach (var upgrade in upgrades) apply.Invoke(combat,new object[] { upgrade });
        }
        bool Spawn(float x, float z)
        {
            bool result = combat.SpawnEnemy(new Vector3(x,0,z));
            foreach (var agent in FindObjectsOfType<NavMeshAgent>()) agent.speed = 0;
            return result;
        }
        IEnumerator Volley()
        {
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(1.7f);
        }
        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            yield return null;
            combat = CombatPlayground.Instance; motor = FindObjectOfType<MomentumMotor>();
            apply = typeof(CombatPlayground).GetMethod("ApplyUpgrade",BindingFlags.Instance|BindingFlags.NonPublic);

            Setup(BoltUpgrade.Piercing);
            bool spawned = Spawn(0,-9) && Spawn(0,-6);
            yield return Volley();
            bool pierce = spawned && combat.Kills == 2 && combat.ShotsFired == 1 && combat.PierceHits == 1;
            Debug.Log($"SWARM_BUILD_TRACE: piercing={pierce} kills={combat.Kills} shots={combat.ShotsFired} hits={combat.PierceHits}");

            Setup(BoltUpgrade.Ricochet);
            spawned = Spawn(0,-9) && Spawn(3,-7);
            yield return Volley();
            bool bounce = spawned && combat.Kills == 2 && combat.ShotsFired == 1 && combat.RicochetHits == 1;
            Debug.Log($"SWARM_BUILD_TRACE: ricochet={bounce} kills={combat.Kills} shots={combat.ShotsFired} hits={combat.RicochetHits}");

            Setup(BoltUpgrade.ExplosiveHits);
            spawned = Spawn(0,-9) && Spawn(1.7f,-9);
            yield return Volley();
            bool blast = spawned && combat.Kills == 2 && combat.ShotsFired == 1 && combat.ExplosionHits == 1;
            Debug.Log($"SWARM_BUILD_TRACE: explosion={blast} kills={combat.Kills} hits={combat.ExplosionHits}");

            Setup(BoltUpgrade.Piercing,BoltUpgrade.Ricochet,BoltUpgrade.ExplosiveHits);
            spawned = Spawn(0,-9) && Spawn(0,-6) && Spawn(3,-4) && Spawn(4.6f,-4);
            yield return Volley();
            bool combined = spawned && combat.Kills == 4 && combat.ShotsFired == 1 &&
                combat.PierceHits > 0 && combat.RicochetHits > 0 && combat.ExplosionHits > 0;
            Debug.Log($"SWARM_BUILD_TRACE: combined={combined} kills={combat.Kills} direct={combat.DirectHits} pierce={combat.PierceHits} bounce={combat.RicochetHits} blast={combat.ExplosionHits}");

            Setup(BoltUpgrade.Piercing,BoltUpgrade.Ricochet,BoltUpgrade.ExplosiveHits);
            spawned = Spawn(0,-9) && Spawn(0,-7.4f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Test wall"; wall.transform.position = new Vector3(0,1.5f,-8.25f);
            wall.transform.localScale = new Vector3(6,3,.3f);
            yield return Volley();
            bool walls = spawned && combat.Kills == 1 && combat.ShotsFired == 1 && combat.ExplosionHits == 0 && combat.RicochetHits == 0;
            Debug.Log($"SWARM_BUILD_TRACE: walls={walls} kills={combat.Kills} direct={combat.DirectHits} blast={combat.ExplosionHits}");
            wall.SetActive(false); Destroy(wall);

            Setup();
            bool reset = combat.PierceRank == 0 && combat.RicochetRank == 0 && combat.BlastRank == 0;
            typeof(CombatPlayground).GetField("xp",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(combat,20);
            typeof(CombatPlayground).GetMethod("OpenUpgrade",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(combat,null);
            var unique = new System.Collections.Generic.HashSet<BoltUpgrade>(combat.UpgradeChoices);
            bool offers = unique.Count == 3;
            combat.ChooseUpgrade(0);
            bool overflow = combat.Level == 3 && combat.ChoosingUpgrade && combat.UpgradeCount == 1;
            combat.ChooseUpgrade(0);
            bool selection = combat.UpgradeCount == 2 && !combat.ChoosingUpgrade;
            for (int i=0; i<5; i++) apply.Invoke(combat,new object[] { BoltUpgrade.Ricochet });
            bool cap = combat.RicochetRank == 3;
            combat.StopCombat();
            bool passed = pierce && bounce && blast && combined && walls && reset && offers && overflow && selection && cap;
            Debug.Log($"SWARM_BUILD_TEST: {(passed ? "PASS" : "FAIL")} pierce={pierce} ricochet={bounce} explosion={blast} combined={combined} walls={walls} reset={reset} offers={offers} overflow={overflow} selection={selection} cap={cap}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
