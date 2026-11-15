#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Swarm
{
    // Opt-in real runtime scenario. It advances the clock, not Time.timeScale,
    // so deadline/late-overtime behavior can be checked without a 15-minute wait.
    public class AreaTimerSmokeProbe : MonoBehaviour
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-area-timer-test")>=0)
                new GameObject("Area timer probe").AddComponent<AreaTimerSmokeProbe>();
        }
        IEnumerator Start()
        {
            yield return null;
            var combat=CombatPlayground.Instance;
            combat.AutoSpawn=combat.AutoFire=false;
            var motor=FindObjectOfType<MomentumMotor>();
            motor.enabled=false;
            combat.Begin();
            motor.SetSpawn(new Vector3(0,1.1f,10));
            var type=typeof(CombatPlayground);
            var elapsed=type.GetField("elapsed",Private);
            var enemyField=type.GetField("enemies",Private);
            var tick=type.GetMethod("TickWaves",Private);
            float Stat(string name) => Convert.ToSingle(type.GetProperty(name,Private).GetValue(combat));
            void Clock(float value) => elapsed.SetValue(combat,value);
            bool initial=!combat.IsAreaOvertime && combat.AreaSecondsRemaining>599 && combat.AreaTimerText.Contains("10:00");

            Clock(599);
            float beforePack=Stat("SwarmPackSize"),beforeCap=Stat("SwarmEnemyLimit"),beforeHealth=Stat("EnemyHealthScale"),beforeDamage=Stat("EnemyDamageScale");
            bool pre=!combat.IsAreaOvertime && combat.AreaSecondsRemaining==1;
            Clock(600);
            tick.Invoke(combat,new object[]{.05f});
            int atCount=combat.EnemyCount;
            float atPack=Stat("SwarmPackSize"),atCap=Stat("SwarmEnemyLimit"),atHealth=Stat("EnemyHealthScale"),atDamage=Stat("EnemyDamageScale"),atRecovery=Stat("PackRecovery");
            bool deadline=combat.IsAreaOvertime && combat.AreaSecondsRemaining==0 && combat.AreaOvertimeSeconds==0 &&
                !combat.Resting && atCount>0 && atPack>beforePack && atCap>beforeCap && atHealth>beforeHealth && atDamage>beforeDamage;
            bool noForcedBoss=type.GetField("bossPhase",Private).GetValue(combat).ToString()=="Hidden" && !combat.Won;
            var enemy=((IList)enemyField.GetValue(combat))[0];
            var et=enemy.GetType();
            float enemyHealth=(float)et.GetField("health").GetValue(enemy),enemyDamage=(float)et.GetField("damageScale").GetValue(enemy);
            Clock(720);
            type.GetMethod("ApplyOvertimePressure",Private).Invoke(combat,new[]{enemy});
            bool oldEnemiesGrow=(float)et.GetField("health").GetValue(enemy)>enemyHealth && (float)et.GetField("damageScale").GetValue(enemy)>enemyDamage;
            bool later=Stat("SwarmPackSize")>atPack && Stat("SwarmEnemyLimit")>=atCap && Stat("EnemyHealthScale")>atHealth &&
                Stat("EnemyDamageScale")>atDamage && Stat("PackRecovery")<atRecovery && combat.AreaTimerText.Contains("+02:00");

            // Simulate the actual Fighting state to exercise TickWaves' boss path.
            var phase=type.GetField("bossPhase",Private);
            phase.SetValue(combat,Enum.Parse(phase.FieldType,"Fighting"));
            type.GetField("spawnClock",Private).SetValue(combat,0f);
            tick.Invoke(combat,new object[]{.05f});
            bool bossSpawns=combat.EnemyCount>atCount && phase.GetValue(combat).ToString()=="Fighting" && combat.AreaOvertimeSeconds==120;
            Clock(900);
            bool bounded=Stat("SwarmEnemyLimit")<=128 && Stat("SwarmPackSize")<=32 && Stat("EnemyDamageScale")>atDamage*3;

            // Selection must stop the same elapsed clock used by countdown/overtime.
            Clock(620);
            type.GetField("<ChoosingUpgrade>k__BackingField",Private).SetValue(combat,true);
            yield return new WaitForSecondsRealtime(.2f);
            bool selectionPaused=Mathf.Abs((float)elapsed.GetValue(combat)-620)<.001f;
            type.GetField("<ChoosingUpgrade>k__BackingField",Private).SetValue(combat,false);
            yield return new WaitForSecondsRealtime(.15f);
            bool resumed=(float)elapsed.GetValue(combat)>620 && phase.GetValue(combat).ToString()=="Fighting";
            combat.Begin();
            bool reset=!combat.IsAreaOvertime && combat.AreaSecondsRemaining>599;
            combat.StopCombat();
            bool passed=initial && pre && deadline && noForcedBoss && oldEnemiesGrow && later && bossSpawns && bounded && selectionPaused && resumed && reset;
            Debug.Log($"SWARM_AREA_TIMER_TEST: {(passed ? "PASS" : "FAIL")} initial={initial} pre={pre} deadline={deadline} enemiesAtExpiry={atCount} noForcedBoss={noForcedBoss} existingEnemiesGrow={oldEnemiesGrow} laterEscalation={later} bossWaves={bossSpawns} bounded={bounded} selectionPause={selectionPaused} resume={resumed} reset={reset}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
