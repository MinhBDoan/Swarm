#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

namespace Swarm
{
    public class ChargerCoopSmokeProbe : MonoBehaviour
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        bool passed=true;
        void Check(bool ok,string name) { passed &= ok; Debug.Log($"SWARM_CHARGERCOOP_STEP: {name}={ok}"); }
        IEnumerator WaitFor(Func<bool> ready)
        {
            float until=Time.realtimeSinceStartup+15;
            while (!ready() && Time.realtimeSinceStartup<until) yield return null;
            Check(ready(),"wait");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-chargercoop-role")>=0)
                new GameObject("Charger co-op probe").AddComponent<ChargerCoopSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60;
            yield return null;
            var args=Environment.GetCommandLineArgs();
            bool host=args[Array.IndexOf(args,"-swarm-chargercoop-role")+1]=="host";
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
                motor.SetSpawn(new Vector3(0,1.1f,-15));
                yield return new WaitForSecondsRealtime(1);
                Check(combat.SpawnEnemy(new Vector3(0,0,12),false,EnemyKind.Charger),"charger spawned");
                yield return WaitFor(()=>combat.PlayerState(1).knockbackSerial==1);
                yield return new WaitForSecondsRealtime(.6f);
                Check(combat.PlayerState(1).knockbackSerial==1 && combat.PlayerState(1).health<100 && combat.Health==100,"one hit for remote victim only");
                typeof(CombatPlayground).GetMethod("Kill",Private).Invoke(combat,new object[]{0});
                yield return new WaitForSecondsRealtime(.8f);
                combat.StopCombat();
                yield return new WaitForSecondsRealtime(.5f);
            }
            else
            {
                yield return WaitFor(()=>combat.Running);
                motor.SetSpawn(new Vector3(0,1.1f,0));
                yield return WaitFor(()=>combat.Health<100);
                Check(motor.Velocity.z< -3.5f && motor.Velocity.y>0,"remote owner receives impulse");
                typeof(MomentumMotor).GetProperty("Velocity").SetValue(motor,Vector3.zero);
                yield return new WaitForSecondsRealtime(.4f);
                Check(motor.Velocity==Vector3.zero,"snapshot does not repeat impulse");
                yield return WaitFor(()=>!combat.Running);
            }
            Debug.Log($"SWARM_CHARGERCOOP_TEST: {(passed ? "PASS" : "FAIL")} role={(host ? "host" : "client")}");
            session.Leave();
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
