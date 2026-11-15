#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
namespace Swarm
{
    public class WeaponFlowSmokeProbe : MonoBehaviour
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-weaponflow-test")>=0)
                new GameObject("Weapon flow probe").AddComponent<WeaponFlowSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60; yield return null;
            var combat=CombatPlayground.Instance; combat.AutoSpawn=combat.AutoFire=false; combat.Begin();
            var motor=FindObjectOfType<MomentumMotor>(); motor.SetSpawn(new Vector3(0,1.1f,-15)); motor.enabled=false;
            combat.SpawnEnemy(new Vector3(0,0,-9));
            foreach (var agent in FindObjectsOfType<NavMeshAgent>()) agent.speed=0;
            var type=typeof(CombatPlayground);
            var enemyList=(IList)type.GetField("enemies",Flags).GetValue(combat);
            foreach (var enemy in enemyList) enemy.GetType().GetField("health").SetValue(enemy,500f);
            var apply=type.GetMethod("ApplyUpgrade",Flags); var tick=type.GetMethod("TickLoadout",Flags);
            apply.Invoke(combat,new object[] { BoltUpgrade.Boomerang }); tick.Invoke(combat,new object[] { .02f });
            var bullets=(IList)type.GetField("bullets",Flags).GetValue(combat);
            yield return new WaitForSecondsRealtime(.72f);
            bool turned=bullets.Count>0 && (bool)bullets[0].GetType().GetField("returning").GetValue(bullets[0]);
            motor.SetSpawn(new Vector3(4,1.1f,-15));
            float deadline=Time.realtimeSinceStartup+2;
            while (bullets.Count>0 && Time.realtimeSinceStartup<deadline) yield return null;
            bool follows=turned && bullets.Count==0 && combat.DirectHits>=1;
            apply.Invoke(combat,new object[] { BoltUpgrade.PulseField }); apply.Invoke(combat,new object[] { BoltUpgrade.PulseField });
            tick.Invoke(combat,new object[] { .02f });
            motor.SetSpawn(new Vector3(6,1.1f,-14)); tick.Invoke(combat,new object[] { .3f });
            LineRenderer range=null,charge=null;
            foreach (var line in FindObjectsOfType<LineRenderer>())
            { if (line.name=="Pulse range") range=line; if (line.name=="Pulse charge") charge=line; }
            bool pulse=range && charge && Vector3.Distance(range.transform.position,motor.transform.position+Vector3.up*.1f)<.01f &&
                Mathf.Abs(range.GetPosition(0).magnitude-4)<.01f && charge.positionCount>2 && charge.positionCount<48;
            combat.StopCombat(); yield return null;
            bool cleanup=true;
            foreach (var line in FindObjectsOfType<LineRenderer>()) if (line.name.StartsWith("Pulse ")) cleanup=false;
            bool passed=follows && pulse && cleanup;
            Debug.Log($"SWARM_WEAPONFLOW_TEST: {(passed ? "PASS" : "FAIL")} movingReturn={follows} pulseRangeAndCharge={pulse} cleanup={cleanup}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
