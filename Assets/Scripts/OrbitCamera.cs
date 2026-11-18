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
            var combat=CombatPlayground.Instance;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (combat && combat.ShopOpen) combat.CloseShop();
                else if (combat && combat.Running) combat.ToggleLocalMenu();
                else { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            }
            // Closing one's own menu must remain possible while the party is paused.
            // Other players' menus and upgrade choices still keep the world paused.
            if (combat && combat.LocalMenuOpen)
            {
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                if (Input.GetMouseButtonDown(0) && Input.mousePosition.x < Screen.width - 330 && !CombatPlayground.HudConsumesPointer)
                    combat.ResumeLocalMenu();
                return;
            }
            if (combat && combat.ShopOpen) return;
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
