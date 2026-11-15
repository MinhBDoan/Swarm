#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Swarm
{
    public class SentinelSmokeProbe : MonoBehaviour
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        CombatPlayground combat;
        MomentumMotor motor;
        object enemy;
        Type enemyType;
        bool passed=true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-sentinel-test")>=0)
                new GameObject("Sentinel moves probe").AddComponent<SentinelSmokeProbe>();
        }
        string Phase() => enemyType.GetField("sentinelPhase").GetValue(enemy).ToString();
        void Check(bool ok,string name) { passed &= ok; Debug.Log($"SWARM_SENTINEL_STEP: {name}={ok}"); }
        void Setup(int move,Vector3 target,Vector3? start=null)
        {
            combat.Begin();
            motor.SetSpawn(target);
            Check(combat.SpawnEnemy(start ?? Vector3.zero,true),"spawn");
            enemy=((IList)typeof(CombatPlayground).GetField("enemies",Private).GetValue(combat))[0];
            enemyType=enemy.GetType();
            enemyType.GetField("sentinelMove").SetValue(enemy,move);
            enemyType.GetField("attackClock").SetValue(enemy,0f);
        }
        IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline=Time.realtimeSinceStartup+5;
            while (!condition() && Time.realtimeSinceStartup<deadline) yield return null;
            Check(condition(),"wait");
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=120;
            yield return null;
            combat=CombatPlayground.Instance;
            combat.AutoSpawn=combat.AutoFire=false;
            motor=FindObjectOfType<MomentumMotor>();
            motor.enabled=false;
            Setup(0,new Vector3(0,1.1f,12));
            yield return WaitFor(()=>Phase()=="Windup");
            yield return new WaitForSecondsRealtime(.4f);
            Check(Phase()=="Windup" && combat.Health==100,"charge telegraph");
            motor.SetSpawn(new Vector3(3,1.1f,14));
            yield return WaitFor(()=>Phase()=="ChargePound");
            var body=(GameObject)enemyType.GetField("body").GetValue(enemy);
            Check(body.transform.position.z>12 && body.transform.position.z<15 && combat.Health==100,"fixed charge stops before pound");
            float remaining=(float)enemyType.GetField("attackClock").GetValue(enemy);
            typeof(CombatPlayground).GetProperty("ChoosingUpgrade").SetValue(combat,true);
            yield return new WaitForSecondsRealtime(.2f);
            Check(Mathf.Abs(remaining-(float)enemyType.GetField("attackClock").GetValue(enemy))<.01f,"upgrade pause freezes attack");
            typeof(CombatPlayground).GetProperty("ChoosingUpgrade").SetValue(combat,false);
            yield return WaitFor(()=>combat.Health<100);
            Check(combat.Health<65 && motor.Velocity.x>4,"pound damage and outward shove");
            float after=combat.Health;
            yield return new WaitForSecondsRealtime(.3f);
            Check(combat.Health==after,"pound hits once");
            yield return WaitFor(()=>Phase()=="Pursuing");
            Check((int)enemyType.GetField("sentinelMove").GetValue(enemy)==1,"cycle advances to sweep");

            Setup(1,new Vector3(0,1.1f,5));
            yield return WaitFor(()=>combat.Health<100);
            Check(combat.Health<70 && motor.Velocity.z>3,"sweep hits front");
            Setup(1,new Vector3(0,1.1f,5));
            yield return WaitFor(()=>Phase()=="Windup");
            motor.SetSpawn(new Vector3(0,1.1f,-5));
            yield return WaitFor(()=>Phase()=="Recovering");
            Check(combat.Health==100,"sweep can be dodged behind boss");

            Setup(2,new Vector3(0,1.1f,8));
            yield return WaitFor(()=>combat.Health<100);
            float waveHealth=combat.Health;
            yield return new WaitForSecondsRealtime(.5f);
            Check(waveHealth<75 && combat.Health==waveHealth,"shockwave crosses and hits once");
            Setup(2,new Vector3(0,1.1f,8));
            yield return WaitFor(()=>Phase()=="Windup");
            motor.SetSpawn(new Vector3(0,3.3f,8));
            yield return WaitFor(()=>Phase()=="Recovering");
            body=(GameObject)enemyType.GetField("body").GetValue(enemy);
            Debug.Log($"SWARM_SENTINEL_JUMP_TRACE: health={combat.Health} player={motor.transform.position} boss={body.transform.position}");
            Check(combat.Health==100,"shockwave can be jumped");

            Setup(3,new Vector3(0,1.1f,3));
            yield return WaitFor(()=>Phase()=="Recovering");
            Check(combat.Health>0 && combat.Health<25,"double pound has two impacts");
            enemyType.GetField("attackClock").SetValue(enemy,0f);
            yield return null;
            Check((int)enemyType.GetField("sentinelMove").GetValue(enemy)==0,"four move cycle wraps");

            Setup(0,new Vector3(29,1.1f,-5),new Vector3(24,0,-5));
            yield return WaitFor(()=>Phase()=="Windup");
            motor.SetSpawn(new Vector3(0,1.1f,-15));
            yield return WaitFor(()=>Phase()=="ChargePound");
            body=(GameObject)enemyType.GetField("body").GetValue(enemy);
            Check(body.transform.position.x<30.5f && combat.Health==100,"wall stops charge early");
            combat.StopCombat();
            Debug.Log($"SWARM_SENTINEL_TEST: {(passed ? "PASS" : "FAIL")}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
