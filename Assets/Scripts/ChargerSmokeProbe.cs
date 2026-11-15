#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Swarm
{
    public class ChargerSmokeProbe : MonoBehaviour
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-charger-test")>=0)
                new GameObject("Charger probe").AddComponent<ChargerSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=120;
            yield return null;
            var combat=CombatPlayground.Instance;
            combat.AutoSpawn=combat.AutoFire=false;
            var motor=FindObjectOfType<MomentumMotor>();
            motor.enabled=false;
            var type=typeof(CombatPlayground);
            var listField=type.GetField("enemies",Private);
            object Enemy() => ((IList)listField.GetValue(combat))[0];
            string Phase() => Enemy().GetType().GetField("chargePhase").GetValue(Enemy()).ToString();

            combat.Begin();
            motor.SetSpawn(new Vector3(0,1.1f,0));
            bool spawn=combat.SpawnEnemy(new Vector3(0,0,12),false,EnemyKind.Charger);
            float health=(float)Enemy().GetType().GetField("health").GetValue(Enemy());
            float deadline=Time.realtimeSinceStartup+3;
            while (Phase()!="WindingUp" && Time.realtimeSinceStartup<deadline) yield return null;
            bool warning=Phase()=="WindingUp" && combat.Health==100 && (GameObject)Enemy().GetType().GetField("warning").GetValue(Enemy());
            yield return new WaitForSecondsRealtime(.4f);
            warning &= Phase()=="WindingUp" && combat.Health==100;
            deadline=Time.realtimeSinceStartup+3;
            while (combat.Health==100 && Time.realtimeSinceStartup<deadline) yield return null;
            bool hit=combat.Health<100 && Vector3.Dot(motor.Velocity,Vector3.back)>3.5f;
            float after=combat.Health;
            Vector3 beforePush=motor.transform.position;
            motor.enabled=true;
            yield return new WaitForSecondsRealtime(.5f);
            motor.enabled=false;
            float pushed=Vector3.ProjectOnPlane(motor.transform.position-beforePush,Vector3.up).magnitude;
            bool once=combat.Health==after && pushed>.5f && pushed<2.7f;

            var impulse=type.GetMethod("ChargerImpulse",BindingFlags.Static|BindingFlags.NonPublic);
            Vector3 head=(Vector3)impulse.Invoke(null,new object[]{Vector3.forward,Vector3.forward});
            Vector3 side=(Vector3)impulse.Invoke(null,new object[]{Vector3.forward,Vector3.right});
            bool direction=head.z>4 && Mathf.Abs(head.x)<.01f && side.x>2 && side.z>1.5f && side.magnitude<head.magnitude;

            combat.Begin();
            motor.SetSpawn(new Vector3(29,1.1f,-5));
            combat.SpawnEnemy(new Vector3(24,0,-5),false,EnemyKind.Charger);
            deadline=Time.realtimeSinceStartup+3;
            while (Phase()!="WindingUp" && Time.realtimeSinceStartup<deadline) yield return null;
            Vector3 locked=(Vector3)Enemy().GetType().GetField("aim").GetValue(Enemy());
            motor.SetSpawn(new Vector3(0,1.1f,-15));
            deadline=Time.realtimeSinceStartup+3;
            while (Phase()!="Recovering" && Time.realtimeSinceStartup<deadline) yield return null;
            bool stun=Phase()=="Recovering" && (float)Enemy().GetType().GetField("attackClock").GetValue(Enemy())>.9f;
            bool dodge=combat.Health==100 && Vector3.Dot(locked,Vector3.right)>.9f;

            type.GetField("elapsed",Private).SetValue(combat,120f);
            type.GetField("waveSpawned",Private).SetValue(combat,7);
            bool introduced=(EnemyKind)type.GetMethod("KindForWave",Private).Invoke(combat,null)==EnemyKind.Charger;
            combat.StopCombat();
            bool passed=spawn && health>60 && warning && hit && once && direction && stun && dodge && introduced;
            Debug.Log($"SWARM_CHARGER_TEST: {(passed ? "PASS" : "FAIL")} health={health} pushMetres={pushed} telegraph={warning} hit={hit} once={once} directional={direction} wallStun={stun} dodge={dodge} timedIntroduction={introduced}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
