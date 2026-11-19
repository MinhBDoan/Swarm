#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
namespace Swarm
{
    public class GrindRailSmokeProbe : MonoBehaviour
    {
        bool passed=true;
        void Check(bool ok,string label){passed &= ok;Debug.Log($"SWARM_GRIND_STEP: {label}={ok}");}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-grind-test")>=0)new GameObject("Grind probe").AddComponent<GrindRailSmokeProbe>();}
        IEnumerator Start()
        {
            yield return null;
            var combat=CombatPlayground.Instance;combat.AutoSpawn=combat.AutoFire=false;combat.enabled=false;
            var motor=FindObjectOfType<MomentumMotor>();motor.enabled=false;
            Check(GrindRail.Rails.Count==6,$"six central rails installed (found {GrindRail.Rails.Count})");
            bool supported=true,clear=true;
            foreach(var r in GrindRail.Rails)
            foreach(var point in r.points)
            {
                supported &= Physics.Raycast(point+Vector3.up*.1f,Vector3.down,out var hit,1,1) && Mathf.Abs(point.y-hit.point.y-.7f)<.05f;
                clear &= !Physics.CheckCapsule(point+Vector3.up*.7f,point+Vector3.up*1.6f,.45f,1);
            }
            Check(supported,"all rails follow terrain");Check(clear,"rail rider clearance");
            var rail=new GameObject("Test rail").AddComponent<GrindRail>();rail.points=new[]{new Vector3(200,40,-100),new Vector3(200,40,1000)};
            void Reset(float speed=20){motor.SetSpawn(new Vector3(200,41.15f,0));motor.ApplyImpulse(Vector3.forward*speed);}
            Reset();Check(motor.TryGrabRail(rail,Vector3.forward),"grab nearby rail");
            float entry=motor.Velocity.magnitude;motor.StepGrinding(0,false,false,.02f);
            Check(motor.IsGrinding && Mathf.Abs(motor.Velocity.magnitude-entry)<.01f && motor.transform.position.z>0,"flat grind preserves momentum");
            float balance=motor.GrindBalance;motor.StepGrinding(-.3f,false,false,.02f);
            Check(motor.GrindBalance<balance,"mouse left shifts balance left");
            motor.StepGrinding(0,true,false,.02f);
            Check(!motor.IsGrinding && Mathf.Abs(motor.Velocity.z-entry)<.01f && motor.Velocity.y>=motor.jumpSpeed-.01f,"Space jumps off with speed");
            Check(!motor.TryGrabRail(rail,Vector3.forward),"regrab cooldown");
            Reset();motor.TryGrabRail(rail,Vector3.forward);
            combat.Begin();motor.SetSpawn(new Vector3(200,41.15f,0));motor.ApplyImpulse(Vector3.forward*20);motor.TryGrabRail(rail,Vector3.forward);
            combat.ToggleLocalMenu();Vector3 pausedPosition=motor.transform.position;float pausedBalance=motor.GrindBalance;
            motor.StepGrinding(8,true,false,.02f);Check(motor.IsGrinding && motor.transform.position==pausedPosition && motor.GrindBalance==pausedBalance,"menu pause freezes grind and exit input");
            combat.ResumeLocalMenu();combat.StopCombat();
            Reset();motor.TryGrabRail(rail,Vector3.forward);var before=motor.Velocity;motor.StepGrinding(0,false,true,.02f);
            Check(!motor.IsGrinding && Vector3.Distance(before,motor.Velocity)<.01f,"crouch drop keeps speed");
            Reset();motor.TryGrabRail(rail,Vector3.forward);motor.StepGrinding(10,false,false,.02f);
            Check(!motor.IsGrinding && motor.Velocity.z>0 && motor.Velocity.z<entry*.9f && motor.Velocity.x>0,"failed balance slips right with recoverable speed loss");
            Reset(-20);motor.TryGrabRail(rail,Vector3.forward);Check(motor.GrindDirection.z<0,"approach velocity determines reverse direction");
            motor.ApplyImpulse(Vector3.right*3);Check(!motor.IsGrinding,"enemy impulse detaches grind");
            Reset();motor.TryGrabRail(rail,Vector3.forward);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(200,41,4);wall.transform.localScale=new Vector3(8,8,1);Physics.SyncTransforms();
            for(int i=0;i<30 && motor.IsGrinding;i++)motor.StepGrinding(0,false,false,.02f);
            Check(!motor.IsGrinding && motor.transform.position.z<3.6f,"obstruction releases without crossing wall");Destroy(wall);yield return null;
            var shortRail=new GameObject("Short rail").AddComponent<GrindRail>();shortRail.points=new[]{new Vector3(200,40,-1),new Vector3(200,40,1)};
            Reset();motor.TryGrabRail(shortRail,Vector3.forward);for(int i=0;i<10 && motor.IsGrinding;i++)motor.StepGrinding(0,false,false,.02f);
            Check(!motor.IsGrinding && motor.Velocity.z>19,"rail end releases with speed");Destroy(shortRail.gameObject);
            Reset();motor.TryGrabRail(rail,Vector3.forward);motor.Respawn();Check(!motor.IsGrinding,"respawn clears grind");
            motor.SetSpawn(new Vector3(230,41.15f,0));Check(!motor.TryGrabRail(rail,Vector3.forward),"distant grab rejected");
            Reset();motor.TryGrabRail(rail,Vector3.forward);
            combat.Begin();Check(!motor.IsGrinding,"new combat run resets grind");
            combat.StopCombat();Destroy(rail.gameObject);
            Debug.Log($"SWARM_GRIND_TEST: {(passed ? "PASS" : "FAIL")}");Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
