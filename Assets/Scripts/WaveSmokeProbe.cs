#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Swarm
{
    public class WaveSmokeProbe : MonoBehaviour
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-wave-test")>=0)
                new GameObject("Swarm pressure probe").AddComponent<WaveSmokeProbe>();
        }

        IEnumerator Start()
        {
            Application.targetFrameRate=120;
            yield return null;
            var combat=CombatPlayground.Instance;
            combat.AutoSpawn=combat.AutoFire=false;
            var motor=FindObjectOfType<MomentumMotor>();
            motor.enabled=false;
            var type=typeof(CombatPlayground);
            var elapsed=type.GetField("elapsed",Private);
            var clock=type.GetField("spawnClock",Private);
            var tick=type.GetMethod("TickWaves",Private);
            var enemies=type.GetField("enemies",Private);
            float LastHealth()
            {
                var list=(IList)enemies.GetValue(combat);
                return (float)list[list.Count-1].GetType().GetField("health").GetValue(list[list.Count-1]);
            }
            float Property(string name) => Convert.ToSingle(type.GetProperty(name,Private).GetValue(combat));

            combat.Begin();
            motor.SetSpawn(new Vector3(-145,-2.9f,375));
            combat.SpawnEnemy(new Vector3(-145,-4,390));
            float early=LastHealth();
            float earlyInterval=Property("PackRecovery"), earlySize=Property("SwarmPackSize"), earlyLimit=Property("SwarmEnemyLimit");
            elapsed.SetValue(combat,300f);
            combat.SpawnEnemy(new Vector3(-145,-4,390));
            float late=LastHealth();
            float interval=Property("PackRecovery"), size=Property("SwarmPackSize"), limit=Property("SwarmEnemyLimit");
            bool growth=late>early*3 && interval<earlyInterval && size>earlySize && limit>earlyLimit;
            type.GetProperty("Damage").SetValue(combat,99999f);
            type.GetProperty("ProjectileCount").SetValue(combat,6);
            combat.SpawnEnemy(new Vector3(-145,-4,390));
            bool independent=Mathf.Abs(LastHealth()-late)<.01f;
            int before=combat.EnemyCount;
            clock.SetValue(combat,0f);
            tick.Invoke(combat,new object[]{6f});
            bool continued=combat.Wave==6 && combat.EnemyCount>=before+5;
            before=combat.EnemyCount;
            clock.SetValue(combat,0f);
            tick.Invoke(combat,new object[]{1f});
            continued &= combat.EnemyCount>before;
            while (combat.EnemyCount<Property("SwarmEnemyLimit"))
            {
                int count=combat.EnemyCount;
                clock.SetValue(combat,0f);
                tick.Invoke(combat,new object[]{1f});
                if (combat.EnemyCount==count) break;
            }
            before=combat.EnemyCount;
            clock.SetValue(combat,0f);
            tick.Invoke(combat,new object[]{1f});
            bool cap=combat.EnemyCount==before && before<=Property("SwarmEnemyLimit");
            Debug.Log($"SWARM_PRESSURE_TRACE: earlyHP={early} lateHP={late} pack={earlySize}->{size} interval={earlyInterval}->{interval} cap={earlyLimit}->{limit}");

            // Exercise the normal Update path while old objectives remain unfinished elsewhere.
            combat.Begin();
            motor.SetSpawn(new Vector3(-145,-2.9f,375));
            var stages=(CombatPlayground.RouteStage[])type.GetField("routeStages",Private).GetValue(combat);
            stages[0]=CombatPlayground.RouteStage.Warning;
            ((float[])type.GetField("routeTimers",Private).GetValue(combat))[0]=100;
            type.GetProperty("DownloadState").SetValue(combat,CombatPlayground.DownloadPhase.Active);
            type.GetProperty("DownloadProgress").SetValue(combat,50f);
            clock.SetValue(combat,0f);
            combat.AutoSpawn=true;
            yield return null;
            bool travel=combat.EnemyCount>0;
            combat.AutoSpawn=false;
            combat.Begin();
            elapsed.SetValue(combat,300f);
            motor.SetSpawn(CanyonLayout.BossConsole+Vector3.up*1.1f);
            bool activated=combat.TryBossConsole();
            type.GetField("bossClock",Private).SetValue(combat,.01f);
            yield return null;
            bool boss=activated && combat.EnemyCount==1 && LastHealth()>6000;
            type.GetProperty("Damage").SetValue(combat,99999f);
            type.GetProperty("FireInterval").SetValue(combat,.01f);
            combat.AutoFire=true;
            type.GetProperty("Health").SetValue(combat,10000f);
            float deadline=Time.realtimeSinceStartup+8;
            while (!combat.Won && Time.realtimeSinceStartup<deadline)
            {
                if (combat.ChoosingUpgrade) combat.ChooseUpgrade(0);
                yield return null;
            }
            bool won=combat.Won;
            combat.Begin();
            bool reset=Property("EnemyHealthScale")==1 && combat.Wave==1 && combat.EnemyCount==0;
            combat.StopCombat();
            bool passed=growth && independent && continued && cap && travel && boss && won && reset;
            Debug.Log($"SWARM_WAVE_TEST: {(passed ? "PASS" : "FAIL")} timeScaling={growth} loadoutIndependent={independent} continuing={continued} bounded={cap} followsTravel={travel} scaledBoss={boss} victory={won} reset={reset}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
