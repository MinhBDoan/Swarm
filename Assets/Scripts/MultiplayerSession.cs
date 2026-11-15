using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Swarm
{
    public class MultiplayerSession : MonoBehaviour
    {
        public const int MaxPlayers = 6;
        NetworkManager manager;
        UnityTransport transport;
        MomentumMotor offlinePlayer;
        OrbitCamera orbit;
        string address = "127.0.0.1", status = "Offline practice — host or join when ready.";
        bool connecting;
        readonly Dictionary<ulong, int> slots = new Dictionary<ulong, int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!FindObjectOfType<MultiplayerSession>() && FindObjectOfType<MomentumMotor>())
                new GameObject("Multiplayer Session").AddComponent<MultiplayerSession>();
        }

        void Awake()
        {
            Application.runInBackground = true;
            offlinePlayer = FindObjectOfType<MomentumMotor>();
            orbit = FindObjectOfType<OrbitCamera>();
            transport = gameObject.AddComponent<UnityTransport>();
            manager = gameObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new Unity.Netcode.NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = Resources.Load<GameObject>("NetworkPlayer");
            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.ConnectionApprovalCallback = Approve;
            manager.OnClientConnectedCallback += Connected;
            manager.OnClientDisconnectCallback += Disconnected;
            manager.OnTransportFailure += TransportFailure;
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            if (CombatPlayground.Instance && CombatPlayground.Instance.Running)
            { response.Approved = false; response.Pending = false; response.Reason = "Run in progress. Join before the host starts combat."; return; }
            var used = new HashSet<int>(slots.Values);
            int slot = 0;
            while (slot < MaxPlayers && used.Contains(slot)) slot++;
            response.Approved = slot < MaxPlayers;
            response.CreatePlayerObject = response.Approved;
            response.Pending = false;
            if (!response.Approved) { response.Reason = "Party is full (6 players)."; return; }
            slots[request.ClientNetworkId] = slot;
            response.Position = new Vector3((slot % 3 - 1) * 3, 2, -15 + slot / 3 * 3);
            response.Rotation = Quaternion.identity;
        }

        public void Begin(bool host, string destination = null)
        {
            if (manager.IsListening || connecting) return;
            if (!manager.NetworkConfig.PlayerPrefab) { status = "Player prefab missing. Use Swarm > Prepare Multiplayer."; return; }
            if (destination != null) address = destination;
            if (!System.Net.IPAddress.TryParse(address.Trim(), out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            { status = "Enter an IPv4 address, for example 192.168.1.20."; return; }
            if (CombatPlayground.Instance) CombatPlayground.Instance.StopCombat();
            slots.Clear();
            transport.SetConnectionData(address.Trim(), 7777, host ? "0.0.0.0" : null);
            offlinePlayer.gameObject.SetActive(false);
            connecting = true;
            status = host ? "Starting host..." : "Connecting...";
            bool started = host ? manager.StartHost() : manager.StartClient();
            if (!started) { connecting = false; RestoreOffline(); status = "Could not start networking. Check the port and log."; }
        }

        void Connected(ulong id)
        {
            // With scene management disabled, NGO 1.8 reports the server ID to clients.
            if (!manager.IsServer || id == manager.LocalClientId) { connecting = false; status = manager.IsHost ? "Hosting on UDP port 7777" : "Connected to " + address; }
            Debug.Log($"SWARM_NET: connected id={id} count={manager.ConnectedClientsIds.Count}");
        }
        void Disconnected(ulong id)
        {
            slots.Remove(id);
            if (!manager.IsServer || id == manager.LocalClientId)
            {
                connecting = false;
                status = string.IsNullOrEmpty(manager.DisconnectReason) ? "Disconnected. You can host or join again." : manager.DisconnectReason;
                Invoke(nameof(RestoreOffline), .1f);
            }
            Debug.Log($"SWARM_NET: disconnected id={id}");
        }
        void TransportFailure() { status = "Network transport failed. Try another host or check the port."; Leave(); }
        public void Leave()
        {
            manager.Shutdown(); connecting = false; slots.Clear();
            Invoke(nameof(RestoreOffline), .1f);
        }
        void RestoreOffline()
        {
            if (!offlinePlayer || manager.IsListening) return;
            offlinePlayer.gameObject.SetActive(true);
            orbit.target = offlinePlayer.transform;
            offlinePlayer.Respawn();
        }
        void OnGUI()
        {
            if (CombatPlayground.Instance && CombatPlayground.Instance.Running) return;
            GUILayout.BeginArea(new Rect(Screen.width - 320, 18, 302, 260), GUI.skin.box);
            GUILayout.Label("CO-OP PLAYGROUND / UP TO 6");
            GUILayout.Label(status);
            GUILayout.Label("Esc releases mouse for these controls.");
            GUI.enabled = !manager.IsListening && !connecting;
            GUILayout.Label("Host IPv4 (127.0.0.1 = this computer)");
            address = GUILayout.TextField(address, 45);
            if (GUILayout.Button("Host")) Begin(true);
            if (GUILayout.Button("Join")) Begin(false);
            GUI.enabled = true;
            if (manager.IsListening || connecting)
            {
                GUILayout.Label(manager.IsServer ? $"Party: {manager.ConnectedClientsIds.Count} / {MaxPlayers}" : "Client connected / connecting");
                if (GUILayout.Button("Leave / cancel")) { status = "Offline practice."; Leave(); }
            }
            GUILayout.EndArea();
        }
    }
}
