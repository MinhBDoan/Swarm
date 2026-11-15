#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;

namespace Swarm
{
    public class EnemySmokeProbe : MonoBehaviour
    {
        CombatPlayground combat;
        MomentumMotor motor;
        bool warningSeen;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-enemy-test") >= 0)
                new GameObject("Enemy Smoke Probe").AddComponent<EnemySmokeProbe>();
        }
        void Setup()
        {
            combat.AutoSpawn = combat.AutoFire = false; combat.Begin();
            motor.SetSpawn(new Vector3(0,1.1f,-15)); motor.enabled = false;
            warningSeen = false;
        }
        IEnumerator WaitForShot()
        {
            float deadline = Time.realtimeSinceStartup+5;
            while (combat.EnemyShotsFired == 0 && Time.realtimeSinceStartup < deadline)
            {
                warningSeen |= GameObject.Find("Spitter aim warning") != null;
                yield return null;
            }
        }
        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            yield return null;
            combat = CombatPlayground.Instance; motor = FindObjectOfType<MomentumMotor>();
            Setup();
            bool spawned = combat.SpawnEnemy(new Vector3(-4,0,-3)) && combat.SpawnEnemy(new Vector3(4,0,-3),false,EnemyKind.Runner);
            var chaser = GameObject.Find("Chaser"); var runner = GameObject.Find("Runner");
            Vector3 chaserStart = chaser.transform.position, runnerStart = runner.transform.position;
            yield return new WaitForSecondsRealtime(1.2f);
            bool faster = spawned && Vector3.Distance(runner.transform.position,runnerStart) > Vector3.Distance(chaser.transform.position,chaserStart)+1.5f;
            bool appearance = runner.transform.Find("Visual").localScale.x < chaser.transform.Find("Visual").localScale.x &&
                runner.transform.Find("Visual").GetComponent<Renderer>().sharedMaterial.color.g > .7f;

            Setup(); combat.SpawnEnemy(new Vector3(0,0,-3),false,EnemyKind.Spitter);
            yield return WaitForShot();
            bool warned = warningSeen && combat.EnemyShotsFired == 1;
            yield return new WaitForSecondsRealtime(1.6f);
            bool hit = combat.EnemyProjectileHits == 1 && combat.Health == 88;

            Setup(); combat.SpawnEnemy(new Vector3(0,0,-3),false,EnemyKind.Spitter);
            yield return WaitForShot();
            motor.SetSpawn(new Vector3(5,1.1f,-15));
            yield return new WaitForSecondsRealtime(1.6f);
            bool dodge = combat.EnemyShotsFired > 0 && combat.EnemyProjectileHits == 0 && combat.Health == 100;

            Setup(); combat.SpawnEnemy(new Vector3(0,0,-3),false,EnemyKind.Spitter);
            yield return WaitForShot();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0,1.5f,-8); wall.transform.localScale = new Vector3(6,3,.5f);
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(1.6f);
            bool blocked = combat.EnemyShotsFired > 0 && combat.EnemyProjectileHits == 0 && combat.Health == 100 && combat.EnemyProjectileCount == 0;
            wall.SetActive(false); Destroy(wall);
            combat.StopCombat();
            bool cleanup = combat.EnemyProjectileCount == 0 && combat.EnemyCount == 0;
            bool passed = faster && appearance && warned && hit && dodge && blocked && cleanup;
            Debug.Log($"SWARM_ENEMY_TEST: {(passed ? "PASS" : "FAIL")} runnerSpeed={faster} appearance={appearance} warning={warned} projectileHit={hit} dodge={dodge} wall={blocked} cleanup={cleanup}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
