using UnityEngine;
namespace Swarm
{
    public class OrbitCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 5.5f, sensitivity = 2.5f;
        float yaw, pitch = 15;
        void Start() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        void LateUpdate()
        {
            if (!target) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (CombatPlayground.BlocksInput) return;
            if (Input.GetMouseButtonDown(0) && Input.mousePosition.x < Screen.width - 330 && !CombatPlayground.HudConsumesPointer) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                yaw += Input.GetAxis("Mouse X") * sensitivity;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * sensitivity, -35, 75);
            }
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 focus = target.position + Vector3.up * .65f;
            Vector3 backwards = rotation * Vector3.back;
            float length = distance;
            if (Physics.SphereCast(focus, .2f, backwards, out RaycastHit hit, distance, 1 << 0, QueryTriggerInteraction.Ignore))
                length = Mathf.Max(.25f, hit.distance - .1f);
            transform.SetPositionAndRotation(focus + backwards * length, rotation);
        }
    }
}
