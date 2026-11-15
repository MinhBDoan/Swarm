#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Swarm
{
    // Opt-in, headless integration check. Normal play never creates this component.
    public class NetworkSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swarm-test-role") >= 0)
                new GameObject("Network Smoke Probe").AddComponent<NetworkSmokeProbe>();
        }
        static string Arg(string key, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }
        IEnumerator Start()
        {
            yield return null;
            var session = FindObjectOfType<MultiplayerSession>();
            session.Begin(Arg("-swarm-test-role", "client") == "host");
            bool reject = Arg("-swarm-test-reject", "false") == "true";
            float duration = float.Parse(Arg("-swarm-test-seconds", "35"), System.Globalization.CultureInfo.InvariantCulture);
            float started = Time.realtimeSinceStartup;
            var initial = new Dictionary<ulong, Vector3>();
            var moved = new HashSet<ulong>();
            int peakPlayers = 0;
            while (Time.realtimeSinceStartup - started < duration)
            {
                var manager = NetworkManager.Singleton;
                if (reject && manager != null && (manager.DisconnectReason ?? "").Contains("Party is full"))
                {
                    Debug.Log("SWARM_TEST: PASS seventh player rejected"); Application.Quit(0); yield break;
                }
                var avatars = FindObjectsOfType<NetworkAvatar>();
                peakPlayers = Mathf.Max(peakPlayers, avatars.Length);
                foreach (var avatar in avatars)
                {
                    if (!avatar.IsSpawned) continue;
                    if (avatar.IsOwner)
                    {
                        avatar.GetComponent<MomentumMotor>().enabled = false;
                        avatar.GetComponent<CharacterController>().enabled = false;
                        // Move the owner only; observers must receive this over transport.
                        avatar.transform.position = new Vector3((avatar.OwnerClientId % 3 - 1f) * 3 + Mathf.Sin(Time.realtimeSinceStartup * 2) * 2,
                            2, -15f + avatar.OwnerClientId / 3 * 3);
                    }
                    if (!initial.ContainsKey(avatar.OwnerClientId)) initial[avatar.OwnerClientId] = avatar.transform.position;
                    if (Vector3.Distance(initial[avatar.OwnerClientId], avatar.transform.position) > 1)
                        moved.Add(avatar.OwnerClientId);
                }
                yield return null;
            }
            session.Leave();
            yield return new WaitForSecondsRealtime(.5f);
            bool offlineRestored = FindObjectOfType<MomentumMotor>() != null && !NetworkManager.Singleton.IsListening;
            bool passed = !reject && peakPlayers == 6 && moved.Count == 6 && offlineRestored;
            Debug.Log($"SWARM_TEST: {(passed ? "PASS" : "FAIL")} peakPlayers={peakPlayers} movingPlayers={moved.Count} offlineRestored={offlineRestored}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
