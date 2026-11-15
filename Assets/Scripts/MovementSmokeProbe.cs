#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;

namespace Swarm
{
    public class MovementSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-swarm-movement-test") >= 0)
                new GameObject("Movement Smoke Probe").AddComponent<MovementSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            yield return null;
            var motor = FindObjectOfType<MomentumMotor>();
            motor.enabled = false;
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.transform.position = new Vector3(200,10,0);
            ramp.transform.localScale = new Vector3(12,1,160);
            ramp.transform.rotation = Quaternion.Euler(-20,0,0);
            Physics.SyncTransforms();
            Physics.Raycast(new Vector3(200,70,-20), Vector3.down, out var ground, 100);
            motor.SetSpawn(ground.point + Vector3.up*1.1f);
            // Use a fixed reference view rather than the orbit camera.
            var reference = new GameObject("Test heading"); motor.view = reference.transform;
            for (int i=0; i<65; i++)
            { motor.Simulate(Vector2.up,false,false,.02f); yield return new WaitForFixedUpdate(); }
            bool enteredFast = motor.Velocity.z > 8;
            bool slowed = false, rolledBack = false;
            for (int i=0; i<220; i++)
            {
                motor.Simulate(Vector2.up,true,false,.02f);
                slowed |= Mathf.Abs(motor.Velocity.z) < 1;
                rolledBack |= motor.Sliding && motor.Velocity.z < -2;
                if (i % 50 == 0) Debug.Log($"SWARM_MOVEMENT_TRACE: step={i} pos={motor.transform.position} velocity={motor.Velocity} sliding={motor.Sliding}");
                yield return new WaitForFixedUpdate();
            }
            // Flat-slide and jump checks use actual controller collision as well.
            motor.SetSpawn(new Vector3(0,1.1f,-15));
            for (int i=0; i<40; i++)
            { motor.Simulate(Vector2.up,false,false,.02f); yield return new WaitForFixedUpdate(); }
            float initial = new Vector2(motor.Velocity.x,motor.Velocity.z).magnitude;
            for (int i=0; i<15; i++)
            { motor.Simulate(Vector2.up,true,false,.02f); yield return new WaitForFixedUpdate(); }
            bool flat = Mathf.Abs(new Vector2(motor.Velocity.x,motor.Velocity.z).magnitude-initial) < .2f;
            float beforeJump = motor.Velocity.z;
            motor.Simulate(Vector2.zero,true,true,.02f);
            bool jump = Mathf.Abs(motor.Velocity.z-beforeJump)<.2f && motor.Velocity.y>5;
            bool passed = enteredFast && slowed && rolledBack && flat && jump;
            Debug.Log($"SWARM_MOVEMENT_TEST: {(passed ? "PASS" : "FAIL")} enteredFast={enteredFast} slowed={slowed} rollback={rolledBack} flatCarry={flat} jumpCarry={jump}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
