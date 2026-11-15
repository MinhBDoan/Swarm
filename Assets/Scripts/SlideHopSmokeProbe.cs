#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
namespace Swarm
{
    public class SlideHopSmokeProbe : MonoBehaviour
    {
        MomentumMotor motor;
        float Speed => Vector3.ProjectOnPlane(motor.Velocity,Vector3.up).magnitude;
        IEnumerator Step(int count,bool slide,bool jump=false, bool forward=true)
        {
            for (int i=0;i<count;i++) { motor.Simulate(forward ? Vector2.up : Vector2.zero,slide,jump,.02f); yield return new WaitForFixedUpdate(); }
        }
        IEnumerator Runup()
        { motor.SetSpawn(new Vector3(200,1.1f,0)); yield return Step(40,false); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-slidehop-test")>=0)
                new GameObject("Slide hop probe").AddComponent<SlideHopSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60; yield return null;
            motor=FindObjectOfType<MomentumMotor>(); motor.enabled=false;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position=new Vector3(200,-.5f,0); floor.transform.localScale=new Vector3(150,1,180);
            motor.view=new GameObject("Hop test heading").transform; Physics.SyncTransforms();
            yield return Runup(); yield return Step(6,true); float before=Speed;
            yield return Step(1,true,true); float boosted=Speed;
            bool timing=boosted>before*1.06f && boosted<before*1.08f && motor.Velocity.y>5;
            yield return Step(5,true,true); bool air=Mathf.Abs(Speed-boosted)<.02f;
            int steps=0;
            while (!motor.Sliding && steps++<100) { yield return Step(1,true); }
            float landing=Speed; yield return Step(6,true); yield return Step(1,true,true);
            bool chain=steps<100 && Speed>landing+.4f;
            yield return Runup(); yield return Step(20,true); before=Speed; yield return Step(1,true,true);
            bool expired=Mathf.Abs(Speed-before)<.02f;
            motor.SetSpawn(new Vector3(200,1.1f,0)); yield return Step(6,true,false,false); yield return Step(1,true,true,false);
            bool standing=Speed<.01f;
            motor.SetSpawn(new Vector3(200,1.1f,0)); motor.ApplyImpulse(Vector3.forward*20); yield return Step(6,true); before=Speed; yield return Step(1,true,true);
            float highGain=Speed-before; bool fade=highGain>0 && highGain<.4f;
            motor.SetSpawn(new Vector3(200,1.1f,0)); motor.ApplyImpulse(Vector3.forward*30); yield return Step(6,true); before=Speed; yield return Step(1,true,true);
            bool preserve=Mathf.Abs(Speed-before)<.02f;
            bool passed=timing && air && chain && expired && standing && fade && preserve;
            Debug.Log($"SWARM_SLIDEHOP_TEST: {(passed ? "PASS" : "FAIL")} timing={timing} noAirStack={air} chainLanding={chain} expiredCarry={expired} standing={standing} highSpeedFade={fade} terrainSpeedPreserved={preserve} initialBoost={boosted} highGain={highGain}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
