using UnityEngine;

namespace Swarm
{
    public class CanyonLandmark : MonoBehaviour
    {
        public string title;
        public string hint;
        void OnGUI()
        {
            if (!Camera.main || CombatPlayground.Instance && CombatPlayground.Instance.Running) return;
            Vector3 point = Camera.main.WorldToScreenPoint(transform.position);
            if (point.z < 0 || point.x < 0 || point.x > Screen.width || point.y < 0 || point.y > Screen.height ||
                Vector3.Distance(Camera.main.transform.position,transform.position) > 80) return;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
            var rect = new Rect(point.x-155,Screen.height-point.y,310,42);
            GUI.Box(rect,GUIContent.none);
            GUI.Label(rect,title+"\n"+hint,style);
        }
    }
}
