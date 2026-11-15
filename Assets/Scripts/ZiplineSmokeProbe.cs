#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Swarm
{
    public class ZiplineSmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-zipline-test") >= 0)
                new GameObject("Zipline Smoke Probe").AddComponent<ZiplineSmokeProbe>();
        }
        MiningZipline Line(Vector3 a, Vector3 b)
        {
            var line = new GameObject("Test zipline").AddComponent<MiningZipline>();
            line.endpointA = a; line.endpointB = b; return line;
        }
        IEnumerator Start()
        {
            Application.targetFrameRate = 60; yield return null;
            var motor = FindObjectOfType<MomentumMotor>(); motor.enabled = false;
            var heading = new GameObject("Test heading"); motor.view = heading.transform;
            var vertical = Line(new Vector3(200,4,0),new Vector3(200,20,0));
            motor.SetSpawn(new Vector3(200.7f,10,0));
            bool up = motor.TryGrabZipline(vertical,Vector3.up);
            motor.Simulate(Vector2.zero,false,false,.1f); up &= motor.Velocity.y > 13 && motor.transform.position.y > 10;
            heading.transform.rotation = Quaternion.LookRotation(Vector3.down,Vector3.forward);
            motor.Simulate(Vector2.zero,false,false,.1f);
            bool stable = motor.IsZiplining && motor.Velocity.y > 0;
            motor.SetSpawn(new Vector3(200.7f,10,0));
            bool down = motor.TryGrabZipline(vertical,Vector3.down);
            motor.Simulate(Vector2.zero,false,false,.1f); down &= motor.Velocity.y < -13 && motor.transform.position.y < 10;

            var horizontal = Line(new Vector3(190,10,0),new Vector3(230,10,0));
            heading.transform.rotation = Quaternion.LookRotation(Vector3.right);
            motor.SetSpawn(new Vector3(200,9.1f,0));
            bool right = motor.TryGrabZipline(horizontal,Vector3.right) && motor.Velocity.x > 13;
            motor.SetSpawn(new Vector3(200,9.1f,0));
            bool left = motor.TryGrabZipline(horizontal,Vector3.left) && motor.Velocity.x < -13;
            motor.SetSpawn(new Vector3(200,9.1f,0));
            var velocity = typeof(MomentumMotor).GetProperty("Velocity",BindingFlags.Instance|BindingFlags.Public);
            velocity.SetValue(motor,Vector3.left*20);
            bool fallback = motor.TryGrabZipline(horizontal,Vector3.forward) && motor.Velocity.x < -19;
            motor.SetSpawn(new Vector3(200,9.1f,0)); velocity.SetValue(motor,Vector3.right*20);
            motor.TryGrabZipline(horizontal,Vector3.right); motor.Simulate(Vector2.zero,false,false,.1f);
            bool entry = motor.Velocity.x > 19;
            motor.Simulate(Vector2.zero,false,true,.02f);
            bool hop = !motor.IsZiplining && motor.Velocity.x > 19 && motor.Velocity.y >= 8;
            bool cooldown = !motor.TryGrabZipline(horizontal,Vector3.right);
            motor.SetSpawn(new Vector3(200,9.1f,0)); motor.TryGrabZipline(horizontal,Vector3.right);
            Vector3 beforeDrop = motor.Velocity;
            motor.Simulate(Vector2.zero,true,false,.02f);
            bool crouchDrop = !motor.IsZiplining && Vector3.Distance(motor.Velocity,beforeDrop) < .001f &&
                !motor.TryGrabZipline(horizontal,Vector3.right);
            motor.SetSpawn(new Vector3(200,9.1f,0)); motor.Simulate(Vector2.zero,true,false,.02f);
            motor.TryGrabZipline(horizontal,Vector3.right); motor.Simulate(Vector2.zero,true,false,.02f);
            bool heldEntry = motor.IsZiplining;
            motor.SetSpawn(new Vector3(229.5f,9.1f,0)); motor.TryGrabZipline(horizontal,Vector3.right);
            motor.Simulate(Vector2.zero,false,false,.1f);
            bool endpoint = !motor.IsZiplining && motor.Velocity.x > 13;

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(203,10,0); wall.transform.localScale = new Vector3(1,8,4);
            Physics.SyncTransforms(); motor.SetSpawn(new Vector3(200,9.1f,0));
            motor.TryGrabZipline(horizontal,Vector3.right); motor.Simulate(Vector2.zero,false,false,.3f);
            bool collision = !motor.IsZiplining && motor.transform.position.x < 202.2f;
            wall.SetActive(false); Destroy(wall);
            var crossing = GameObject.Find("Basin crossing line").GetComponent<MiningZipline>();
            motor.SetSpawn(crossing.endpointA+Vector3.down*1.2f);
            bool crossingRide = motor.TryGrabZipline(crossing,crossing.Axis);
            for (int i=0; i<100 && motor.IsZiplining; i++) motor.Simulate(Vector2.zero,false,false,.05f);
            crossingRide &= motor.transform.position.x > 22 && motor.transform.position.z > 43 && !motor.IsZiplining;
            bool passed = up && down && stable && right && left && fallback && entry && hop && cooldown && crouchDrop && heldEntry && endpoint && collision && crossingRide;
            Debug.Log($"SWARM_ZIPLINE_HELD_ENTRY_TEST: {(heldEntry ? "PASS" : "FAIL")}");
            Debug.Log($"SWARM_ZIPLINE_CROUCH_TEST: {(crouchDrop ? "PASS" : "FAIL")}");
            Debug.Log($"SWARM_ZIPLINE_TEST: {(passed ? "PASS" : "FAIL")} up={up} down={down} lookAround={stable} right={right} left={left} fallback={fallback} entrySpeed={entry} hopCarry={hop} regrabDelay={cooldown} endpoint={endpoint} collision={collision} crossing={crossingRide} finalPosition={motor.transform.position}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
