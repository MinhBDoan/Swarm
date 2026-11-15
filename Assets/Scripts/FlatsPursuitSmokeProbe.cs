#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace Swarm
{
    public class FlatsPursuitSmokeProbe : MonoBehaviour
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-flats-pursuit-test")>=0)
                new GameObject("Flats pursuit probe").AddComponent<FlatsPursuitSmokeProbe>();
        }

        IEnumerator Start()
        {
            Application.targetFrameRate=120;
            yield return null;
            var combat=CombatPlayground.Instance;
            combat.AutoSpawn=combat.AutoFire=false;
            var motor=FindObjectOfType<MomentumMotor>();
            motor.enabled=false;
            bool passed=true;
            var starts=new[] {new Vector3(-117,0,68),new Vector3(-80,1.4f,647),
                new Vector3(-55,.1f,390),new Vector3(-220,2.2f,350)};
            var targets=new[] {new Vector3(-118,-1,120),new Vector3(-80,0,590),
                new Vector3(-105,-3.6f,385),new Vector3(-180,-4,360)};
            for (int i=0;i<starts.Length;i++)
            {
                combat.Begin();
                motor.SetSpawn(targets[i]+Vector3.up*1.1f);
                bool spawned=combat.SpawnEnemy(starts[i]);
                var agents=FindObjectsOfType<NavMeshAgent>();
                float deadline=Time.realtimeSinceStartup+28;
                while (spawned && Vector3.Distance(agents[0].transform.position,targets[i])>3 &&
                    Time.realtimeSinceStartup<deadline) yield return null;
                bool followed=spawned && Vector3.Distance(agents[0].transform.position,targets[i])<3;
                passed &= followed;
                Debug.Log($"SWARM_FLATS_PURSUIT: entrance={i} spawned={spawned} followed={followed} position={(spawned ? agents[0].transform.position : Vector3.zero)}");
            }

            combat.Begin();
            var spawner=typeof(CombatPlayground).GetMethod("SpawnAroundPlayer",Private);
            var locations=new[] {new Vector3(-145,-4,375),new Vector3(-118,-1,120),new Vector3(-80,0,590),new Vector3(-185,-3.7f,250)};
            foreach (var point in locations)
            {
                motor.SetSpawn(point+Vector3.up*1.1f);
                bool spawned=(bool)spawner.Invoke(combat,new object[]{false});
                passed &= spawned;
                Debug.Log($"SWARM_FLATS_LOCAL_SPAWN: position={point} spawned={spawned}");
            }
            combat.StopCombat();
            Debug.Log($"SWARM_FLATS_PURSUIT_TEST: {(passed ? "PASS" : "FAIL")}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
