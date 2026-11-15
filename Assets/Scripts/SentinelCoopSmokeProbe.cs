#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

namespace Swarm
{
    public class SentinelCoopSmokeProbe : MonoBehaviour
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        bool passed=true;
        void Check(bool ok,string name) {passed &= ok;Debug.Log($"SWARM_SENTINELCOOP_STEP: {name}={ok}");}
        IEnumerator WaitFor(Func<bool> condition)
        {
            float until=Time.realtimeSinceStartup+15;
            while (!condition() && Time.realtimeSinceStartup<until) yield return null;
            Check(condition(),"wait");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-sentinelcoop-role")>=0)
                new GameObject("Sentinel co-op probe").AddComponent<SentinelCoopSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60;
            yield return null;
            var args=Environment.GetCommandLineArgs();
            bool host=args[Array.IndexOf(args,"-swarm-sentinelcoop-role")+1]=="host";
            var session=FindObjectOfType<MultiplayerSession>();
            session.Begin(host);
            yield return WaitFor(()=>NetworkManager.Singleton.IsConnectedClient && NetworkManager.Singleton.LocalClient.PlayerObject);
            var combat=CombatPlayground.Instance;
            var motor=NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<MomentumMotor>();
            motor.enabled=false;
            if (host)
            {
                yield return WaitFor(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==2);
                combat.AutoSpawn=combat.AutoFire=false;
                combat.Begin();
                motor.SetSpawn(new Vector3(-3,1.1f,14));
                yield return new WaitForSecondsRealtime(1);
                Check(combat.SpawnEnemy(Vector3.zero,true),"spawn");
                object enemy=((IList)typeof(CombatPlayground).GetField("enemies",Private).GetValue(combat))[0];
                Type type=enemy.GetType();
                typeof(CombatPlayground).GetField("consoleSentinel",Private).SetValue(combat,enemy);
                string Phase() => type.GetField("sentinelPhase").GetValue(enemy).ToString();
                type.GetField("attackClock").SetValue(enemy,0f);
                yield return WaitFor(()=>Phase()=="Windup");
                type.GetField("aim").SetValue(enemy,Vector3.forward);
                typeof(CombatPlayground).GetMethod("CreateSentinelWarning",Private).Invoke(combat,new[]{enemy});
                yield return WaitFor(()=>Phase()=="ChargePound");
                Check(combat.Health==100 && combat.PlayerState(1).health==100,"charge misses side players");
                type.GetField("attackClock").SetValue(enemy,.1f);
                yield return WaitFor(()=>combat.PlayerState(1).knockbackSerial==1);
                Check(combat.Health<65 && combat.PlayerState(1).health<65 && motor.Velocity.x< -4,"pound hits both and pushes host outward");
                yield return new WaitForSecondsRealtime(.5f);
                Check(combat.PlayerState(1).knockbackSerial==1,"remote hit once");
                typeof(CombatPlayground).GetMethod("Kill",Private).Invoke(combat,new object[]{0});
                yield return new WaitForSecondsRealtime(.7f);
                combat.StopCombat();
                yield return new WaitForSecondsRealtime(.5f);
            }
            else
            {
                yield return WaitFor(()=>combat.Running);
                motor.SetSpawn(new Vector3(3,1.1f,14));
                yield return WaitFor(()=>combat.Health<100);
                Check(motor.Velocity.x>4 && combat.Health<65,"remote receives radial impulse and damage");
                string move=(string)typeof(CombatPlayground).GetField("remoteSentinelMove",Private).GetValue(combat);
                Check(move.Contains("CHARGE"),"move status replicated");
                typeof(MomentumMotor).GetProperty("Velocity").SetValue(motor,Vector3.zero);
                yield return new WaitForSecondsRealtime(.4f);
                Check(motor.Velocity==Vector3.zero,"no repeated impulse");
                yield return WaitFor(()=>!combat.Running);
            }
            Debug.Log($"SWARM_SENTINELCOOP_TEST: {(passed ? "PASS" : "FAIL")} role={(host ? "host" : "client")}");
            session.Leave();
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
