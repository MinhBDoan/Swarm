#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

namespace Swarm
{
    public class BossCoopSmokeProbe : MonoBehaviour
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        bool passed = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swarm-bosscoop-role") >= 0)
                new GameObject("Boss co-op probe").AddComponent<BossCoopSmokeProbe>();
        }

        void Check(bool condition, string step)
        {
            passed &= condition;
            Debug.Log($"SWARM_BOSSCOOP_STEP: {step}={condition}");
        }

        IEnumerator WaitFor(Func<bool> condition, string step)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Check(condition(), step);
        }

        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            yield return null;
            var args = Environment.GetCommandLineArgs();
            bool host = args[Array.IndexOf(args, "-swarm-bosscoop-role") + 1] == "host";
            var session = FindObjectOfType<MultiplayerSession>();
            session.Begin(host);
            yield return WaitFor(() => NetworkManager.Singleton.IsConnectedClient && NetworkManager.Singleton.LocalClient.PlayerObject, "connected");
            if (!passed) { Application.Quit(1); yield break; }

            var combat = CombatPlayground.Instance;
            var motor = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<MomentumMotor>();
            motor.enabled = false;
            var phase = typeof(CombatPlayground).GetField("bossPhase", Private);
            var clock = typeof(CombatPlayground).GetField("bossClock", Private);
            string Phase() => phase.GetValue(combat).ToString();
            if (host)
            {
                yield return WaitFor(() => NetworkManager.Singleton.ConnectedClientsIds.Count == 2, "party joined");
                combat.AutoSpawn = combat.AutoFire = false;
                combat.Begin();
                Check(!combat.TryBossConsole(), "distant proposal rejected");
                yield return WaitFor(() => Phase() == "Voting", "remote proposal");
                yield return new WaitForSecondsRealtime(.6f);
                Check(Phase() == "Voting" && combat.Wave == 1 && combat.EnemyCount == 0, "waits for teammate");
                var ready = typeof(CombatPlayground).GetMethod("ReadyForBoss", Private);
                Check((bool)ready.Invoke(combat, null), "host votes from afar");
                yield return WaitFor(() => Phase() == "Preparing", "unanimous countdown");
                Check(combat.EnemyCount == 0, "no spawn before countdown");
                clock.SetValue(combat, .1f);
                yield return WaitFor(() => Phase() == "Fighting" && combat.EnemyCount == 1, "early boss spawn");
                yield return new WaitForSecondsRealtime(.6f);
                typeof(CombatPlayground).GetMethod("Kill", Private).Invoke(combat, new object[] { 0 });
                Check(combat.Won && combat.SharedMoney == 40, "boss defeat ends stage");
                yield return new WaitForSecondsRealtime(1f);
            }
            else
            {
                yield return WaitFor(() => combat.Running, "run replicated");
                motor.SetSpawn(CanyonLayout.BossConsole + Vector3.up*1.1f);
                yield return WaitFor(() => Phase() == "Discovered", "discovery replicated");
                yield return new WaitForSecondsRealtime(.8f);
                Check(combat.TryBossConsole() && combat.TryBossConsole(), "remote console interaction");
                yield return WaitFor(() => Phase() == "Voting", "vote replicated");
                yield return WaitFor(() => Phase() == "Fighting", "fight replicated");
                yield return WaitFor(() => combat.Won && combat.SharedMoney == 40, "victory replicated");
            }
            Debug.Log($"SWARM_BOSSCOOP_TEST: {(passed ? "PASS" : "FAIL")} role={(host ? "host" : "client")}");
            session.Leave();
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
