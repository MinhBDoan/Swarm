#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

namespace Swarm
{
    public class GroupPauseCoopSmokeProbe : MonoBehaviour
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        bool passed=true;
        CombatPlayground combat;
        void Check(bool ok,string name) { passed &= ok; Debug.Log($"SWARM_GROUPPAUSE_STEP: {name}={ok}"); }
        float Elapsed => (float)typeof(CombatPlayground).GetField("elapsed",Private).GetValue(combat);
        void Phase(string value) => typeof(CombatPlayground).GetProperty("Notice").SetValue(combat,value);
        IEnumerator WaitFor(Func<bool> ready,string name)
        {
            float until=Time.realtimeSinceStartup+20;
            while (!ready() && Time.realtimeSinceStartup<until) yield return null;
            Check(ready(),name);
        }
        IEnumerator AssertFrozen(string name)
        {
            float elapsed=Elapsed;
            var drill=DrillHazard.Instance;
            float cycle=drill ? drill.CycleTime : 0;
            yield return new WaitForSecondsRealtime(.5f);
            Check(Mathf.Abs(Elapsed-elapsed)<.001f,name+" clock frozen");
            Check(!drill || Mathf.Abs(drill.CycleTime-cycle)<.001f,name+" drill frozen");
            Check(CombatPlayground.BlocksInput,name+" movement blocked");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-grouppause-role")>=0)
                new GameObject("Group pause co-op probe").AddComponent<GroupPauseCoopSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60;
            yield return null;
            var args=Environment.GetCommandLineArgs();
            bool host=args[Array.IndexOf(args,"-swarm-grouppause-role")+1]=="host";
            var session=FindObjectOfType<MultiplayerSession>();
            session.Begin(host);
            yield return WaitFor(()=>NetworkManager.Singleton.IsConnectedClient && NetworkManager.Singleton.LocalClient.PlayerObject,"connected");
            combat=CombatPlayground.Instance;
            NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<MomentumMotor>().enabled=false;
            if (host)
            {
                yield return WaitFor(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==2,"two players");
                combat.AutoSpawn=combat.AutoFire=false;
                combat.Begin();
                yield return new WaitForSecondsRealtime(.5f);
                combat.SetLocalMenuOpen(true);
                Phase("pause-probe overlap");
                yield return WaitFor(()=>combat.MenuPauseCount==2,"overlapping menus");
                combat.ResumeLocalMenu();
                yield return null;
                Check(combat.MenuPauseCount==1 && combat.PartyPaused,"remote menu retains pause");
                yield return AssertFrozen("remote menu");
                typeof(CombatPlayground).GetMethod("GivePartyXP",Private).Invoke(combat,new object[]{(int)typeof(CombatPlayground).GetProperty("RequiredXP",Private).GetValue(combat)});
                Phase("pause-probe upgrade");
                yield return WaitFor(()=>combat.MenuPauseCount==0 && combat.ChoosingUpgrade && !combat.PlayerState(1).choosing,"only host upgrade pending");
                Check(combat.PartyPaused,"upgrade retains pause after menus close");
                yield return AssertFrozen("upgrade");
                combat.SkipUpgrade();
                yield return WaitFor(()=>!combat.PartyPaused,"all selections resolved");
                float before=Elapsed;
                yield return new WaitForSecondsRealtime(.4f);
                Check(Elapsed>before+.1f,"simulation resumes");
                Phase("pause-probe disconnect");
                yield return WaitFor(()=>combat.MenuPauseCount==1,"remote disconnect menu opened");
                yield return WaitFor(()=>combat.PartySize==1,"remote disconnected");
                yield return WaitFor(()=>!combat.PartyPaused && combat.MenuPauseCount==0,"departed pause removed");
                before=Elapsed;
                yield return new WaitForSecondsRealtime(.3f);
                Check(Elapsed>before+.1f,"clock resumes after disconnect");
                combat.SetLocalMenuOpen(true);
                combat.StopCombat();
                combat.Begin();
                Check(!combat.LocalMenuOpen && combat.MenuPauseCount==0 && !combat.PartyPaused,"retry clears menu state");
                combat.StopCombat();
            }
            else
            {
                yield return WaitFor(()=>combat.Running && combat.Notice=="pause-probe overlap","host menu received");
                Check(combat.PartyPaused && combat.MenuPauseActive,"host pause replicated");
                combat.SetLocalMenuOpen(true);
                yield return WaitFor(()=>combat.ChoosingUpgrade && combat.Notice=="pause-probe upgrade","shared upgrade received");
                combat.ResumeLocalMenu();
                combat.SkipUpgrade();
                yield return WaitFor(()=>!combat.ChoosingUpgrade,"client upgrade resolved");
                Check(combat.PartyPaused,"host pending choice keeps client paused");
                yield return WaitFor(()=>!combat.PartyPaused,"host choice completed");
                yield return WaitFor(()=>combat.Notice=="pause-probe disconnect","disconnect phase received");
                combat.SetLocalMenuOpen(true);
                yield return new WaitForSecondsRealtime(.6f);
                session.Leave();
                yield return WaitFor(()=>!combat.LocalMenuOpen && combat.MenuPauseCount==0,"leave clears menu state");
            }
            Debug.Log($"SWARM_GROUPPAUSE_TEST: {(passed ? "PASS" : "FAIL")} role={(host ? "host" : "client")}");
            session.Leave();
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
