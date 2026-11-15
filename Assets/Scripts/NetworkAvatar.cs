using Unity.Netcode;
using UnityEngine;

namespace Swarm
{
    public class NetworkAvatar : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            var motor = GetComponent<MomentumMotor>();
            var controller = GetComponent<CharacterController>();
            controller.enabled = IsOwner;
            motor.enabled = IsOwner;
            if (!IsOwner) motor.view = transform;
            var renderer = GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", Color.HSVToRGB((OwnerClientId * .19f + .12f) % 1, .7f, .95f));
            renderer.SetPropertyBlock(properties);
            name = "Player " + (OwnerClientId + 1);
            if (IsOwner)
            {
                motor.SetSpawn(transform.position);
                var camera = FindObjectOfType<OrbitCamera>();
                camera.target = transform;
                motor.view = camera.transform;
            }
            Debug.Log($"SWARM_NET: spawned owner={OwnerClientId} local={IsOwner}");
        }

        void OnGUI()
        {
            if (!IsSpawned || IsOwner || !Camera.main) return;
            Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
            var rect=new Rect(screen.x-40,Screen.height-screen.y,100,25);
            if (screen.z > 0 && !CombatPlayground.WorldLabelBlocked(rect)) GUI.Label(rect,name);
        }
    }
}
