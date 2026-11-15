#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Swarm
{
    public class LayoutSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-layout-test") >= 0)
                new GameObject("Layout Smoke Probe").AddComponent<LayoutSmokeProbe>();
        }
        bool Path(Vector3 from, Vector3 to)
        {
            if (!NavMesh.SamplePosition(from,out var a,2,NavMesh.AllAreas) || !NavMesh.SamplePosition(to,out var b,2,NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path) && path.status == NavMeshPathStatus.PathComplete;
        }
        float Surface(float x, float z)
        {
            return Physics.Raycast(new Vector3(x,30,z),Vector3.down,out var hit,40,1 << 0) ? hit.point.y : -100;
        }
        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            yield return null;
            var combat = CombatPlayground.Instance;
            combat.AutoSpawn = combat.AutoFire = false; combat.Begin();
            var motor = FindObjectOfType<MomentumMotor>(); motor.enabled = false;
            bool groundRoute = Path(new Vector3(0,0,-15),new Vector3(12,0,26));
            bool cacheRoute = Path(new Vector3(0,0,-15),combat.CachePosition);
            bool underpass = Path(new Vector3(12,0,26),new Vector3(0,0,60));
            bool geometry = Mathf.Abs(Surface(-22,25)-5.5f)<.1f && Mathf.Abs(Surface(23,46)-7)<.1f && Surface(-2.5f,48)<1;
            var signs=FindObjectsOfType<CanyonLandmark>();
            bool labels = signs.Length == 6 && System.Array.Exists(signs,sign=>sign.title=="DRILLING STATION");
            var heading = new GameObject("Test heading"); motor.view = heading.transform;
            motor.SetSpawn(new Vector3(23,1.1f,0));
            for (int i=0; i<210; i++)
            { motor.Simulate(Vector2.up,false,false,.02f); yield return new WaitForFixedUpdate(); }
            bool ascent = motor.transform.position.z > 35 && motor.transform.position.y > 7.5f;
            motor.SetSpawn(new Vector3(-9,8.1f,48)); heading.transform.rotation = Quaternion.Euler(0,90,0);
            for (int i=0; i<20; i++)
            { motor.Simulate(Vector2.up,false,false,.02f); yield return new WaitForFixedUpdate(); }
            motor.Simulate(Vector2.up,false,true,.02f); yield return new WaitForFixedUpdate();
            for (int i=0; i<50; i++)
            { motor.Simulate(Vector2.up,false,false,.02f); yield return new WaitForFixedUpdate(); }
            bool gap = motor.transform.position.x > 0 && motor.transform.position.y > 7.5f;
            bool passed = groundRoute && cacheRoute && underpass && geometry && labels && ascent && gap;
            Debug.Log($"SWARM_LAYOUT_TEST: {(passed ? "PASS" : "FAIL")} ground={groundRoute} cache={cacheRoute} underpass={underpass} geometry={geometry} landmarks={labels} ascent={ascent} gapJump={gap} finalPosition={motor.transform.position}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
