#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
namespace Swarm
{
    public class PacingSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swarm-pacing-test") >= 0)
                new GameObject("Pacing Smoke Probe").AddComponent<PacingSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            yield return null;
            UnityEngine.Random.InitState(681);
            var combat = CombatPlayground.Instance;
            var motor = FindObjectOfType<MomentumMotor>();
            motor.enabled = false;
            combat.AutoSpawn = combat.AutoFire = false;
            var flags = BindingFlags.Instance|BindingFlags.NonPublic;
            var clock = typeof(CombatPlayground).GetField("spawnClock",flags);
            var tick = typeof(CombatPlayground).GetMethod("TickWaves",flags);
            bool cadence = true, spacing = true, reachable = true, placements = true;
            int total = 0;
            foreach (var location in new[] { new Vector3(0,1.1f,-15), new Vector3(-7,11.1f,18), new Vector3(23,8.1f,46) })
            {
                combat.Begin();
                motor.SetSpawn(location);
                typeof(CombatPlayground).GetProperty("Wave").SetValue(combat,2);
                for (int i=0; i<6; i++)
                {
                    clock.SetValue(combat,0f);
                    tick.Invoke(combat,new object[] { 0f });
                    placements &= combat.EnemyCount == i+1;
                    cadence &= Mathf.Abs((float)clock.GetValue(combat)-((i+1)%3 == 0 ? 4f : .65f)) < .01f;
                }
                foreach (var agent in FindObjectsOfType<NavMeshAgent>())
                {
                    if (!agent.gameObject.activeInHierarchy) continue;
                    total++;
                    var offset = agent.transform.position-location;
                    spacing &= offset.magnitude >= 14 && Vector3.ProjectOnPlane(offset,Vector3.up).magnitude >= 12;
                    var path = new NavMeshPath();
                    reachable &= NavMesh.SamplePosition(location,out var target,12,NavMesh.AllAreas) &&
                        NavMesh.CalculatePath(agent.transform.position,target.position,NavMesh.AllAreas,path) && path.status == NavMeshPathStatus.PathComplete;
                }
                combat.StopCombat();
                yield return null;
            }
            bool passed = cadence && spacing && reachable && placements && total == 18;
            Debug.Log($"SWARM_PACING_TEST: {(passed ? "PASS" : "FAIL")} cadence={cadence} spacing={spacing} reachable={reachable} placements={placements} total={total}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
