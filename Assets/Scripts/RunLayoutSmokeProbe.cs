#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Swarm
{
    public class RunLayoutSmokeProbe : MonoBehaviour
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        bool failed;
        void Check(bool ok,string label)
        {
            failed|=!ok;
            Debug.Log($"SWARM_RUN_LAYOUT_TRACE: {label}={ok}");
        }
        IEnumerator Wait(Func<bool> condition,string label)
        {
            float until=Time.realtimeSinceStartup+25;
            while (!condition() && Time.realtimeSinceStartup<until) yield return null;
            Check(condition(),label);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var args=Environment.GetCommandLineArgs();
            if (Array.IndexOf(args,"-swarm-run-layout-test")>=0 || Array.IndexOf(args,"-swarm-run-layout-role")>=0)
                new GameObject("Run layout probe").AddComponent<RunLayoutSmokeProbe>();
        }

        static bool Same(Vector3 a,Vector3 b) => (a-b).sqrMagnitude<.000001f;
        static bool Same(Vector3[] a,Vector3[] b)
        {
            if (a==null || b==null || a.Length!=b.Length) return false;
            for (int i=0;i<a.Length;i++) if (!Same(a[i],b[i])) return false;
            return true;
        }
        string Fingerprint(CombatPlayground combat,bool includeSeed=true)
        {
            var layout=combat.ActiveRunLayout;
            if (layout==null) return "missing";
            var text=new StringBuilder(includeSeed ? layout.seed.ToString(CultureInfo.InvariantCulture) : "layout");
            void Add(Vector3 point)
            {
                // Millimeter precision is enough to distinguish layouts while
                // tolerating normal JSON float round-trip precision.
                text.Append('|').Append(Mathf.RoundToInt(point.x*1000)).Append(',')
                    .Append(Mathf.RoundToInt(point.y*1000)).Append(',').Append(Mathf.RoundToInt(point.z*1000));
            }
            foreach (var p in layout.terminals) Add(p);
            foreach (var p in layout.chests) Add(p);
            foreach (var p in layout.shops) Add(p);
            foreach (var p in layout.challenges) Add(p);
            Add(layout.bossConsole);Add(layout.bossSpawn);
            uint hash=2166136261;
            foreach (char c in text.ToString()) hash=(hash^c)*16777619;
            return hash.ToString("x8",CultureInfo.InvariantCulture);
        }

        bool LiveMatches(CombatPlayground combat)
        {
            var layout=combat.ActiveRunLayout;
            return layout!=null && layout.seed==combat.RunSeed &&
                Same(layout.terminals,CanyonLayout.DownloadCenters) &&
                Same(layout.chests,CombatPlayground.MoneyCrateFloors) &&
                Same(layout.shops,CombatPlayground.ShopLocations) &&
                Same(layout.challenges,CombatPlayground.ChallengeLocations) &&
                Same(layout.bossConsole,CanyonLayout.BossConsole) && Same(layout.bossSpawn,CanyonLayout.BossSpawn);
        }

        bool BossPropsMatch(CombatPlayground combat)
        {
            var console=GameObject.Find("Sentinel activation console");
            var board=GameObject.Find("Loop sign / SENTINEL CONSOLE / E TO START");
            var label=GameObject.Find("SENTINEL CONSOLE / E TO START");
            var ringObject=GameObject.Find("Sentinel interaction ring");
            if (!console || !board || !label || !ringObject) return false;
            var ring=ringObject.GetComponent<LineRenderer>();
            if (!ring || !ring.useWorldSpace || ring.positionCount<3) return false;
            Vector3 center=CanyonLayout.BossConsole;
            if (!Same(console.transform.position,center+Vector3.up*.65f) ||
                !Same(board.transform.position,center+Vector3.up*2.4f) ||
                !Same(label.transform.position,center+Vector3.up*2.4f-Vector3.forward*.13f)) return false;
            for (int i=0;i<ring.positionCount;i++)
            {
                Vector3 point=ring.GetPosition(i),delta=point-center;
                if (Mathf.Abs(delta.y-.09f)>.001f || Mathf.Abs(Vector3.ProjectOnPlane(delta,Vector3.up).magnitude-CombatPlayground.BossInteractRange)>.001f) return false;
            }
            Vector3 before=console.transform.position;
            combat.RefreshBossLocation();combat.RefreshBossLocation();
            return Same(before,console.transform.position);
        }

        void CheckHostGeometry(CombatPlayground combat)
        {
            var layout=combat.ActiveRunLayout;
            Check(layout!=null && layout.terminals.Length==8 && layout.chests.Length==7 && layout.shops.Length==3 && layout.challenges.Length==3,"layout dimensions");
            if (layout==null) return;
            var points=new List<Vector3>();points.AddRange(layout.terminals);points.AddRange(layout.chests);points.AddRange(layout.shops);points.AddRange(layout.challenges);
            points.Add(layout.bossConsole);points.Add(layout.bossSpawn);
            bool support=true,navigation=true,separation=true;
            NavMesh.SamplePosition(new Vector3(0,0,-15),out var start,6,NavMesh.AllAreas);
            foreach (var point in points)
            {
                support &= Physics.Raycast(point+Vector3.up*.5f,Vector3.down,out var floor,2,1) && Mathf.Abs(floor.point.y-point.y)<.3f;
                bool sampled=NavMesh.SamplePosition(point,out var hit,1,NavMesh.AllAreas);
                var path=new NavMeshPath();
                navigation &= sampled && NavMesh.CalculatePath(start.position,hit.position,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete;
            }
            for (int i=0;i<points.Count;i++) for (int j=i+1;j<points.Count;j++)
            {
                // The boss encounter reserves its console/spawn as one site.
                if (i==points.Count-2 && j==points.Count-1) continue;
                separation &= Vector3.ProjectOnPlane(points[i]-points[j],Vector3.up).magnitude>=13.95f;
            }
            bool central=true;
            for (int i=5;i<layout.terminals.Length;i++) central &= CanyonLayout.InCentralFlats(layout.terminals[i]);
            Check(support,"all generated actor points supported by floor");
            Check(navigation,"all generated actor points reachable from basin");
            Check(separation,"generated actors separated by 14m");
            Check(central,"three added terminal slots inside central flats");
            Check(LiveMatches(combat),"layout arrays and live coordinates agree");
            Check(BossPropsMatch(combat),"boss props follow layout without repeated-apply drift");
        }

        IEnumerator Start()
        {
            Application.targetFrameRate=60;
            yield return null;
            var args=Environment.GetCommandLineArgs();int roleIndex=Array.IndexOf(args,"-swarm-run-layout-role");
            if (roleIndex<0) {yield return Solo();yield break;}
            bool host=roleIndex+1<args.Length && args[roleIndex+1]=="host";
            yield return Networked(host);
        }

        IEnumerator Solo()
        {
            var combat=CombatPlayground.Instance;var motor=FindObjectOfType<MomentumMotor>();motor.enabled=false;
            combat.AutoSpawn=combat.AutoFire=false;
            int previousSeed=0;string previous="";
            for (int run=0;run<3;run++)
            {
                combat.Begin();
                Check(combat.Running,"solo run begins");
                if (!combat.Running) break;
                CheckHostGeometry(combat);
                string fingerprint=Fingerprint(combat),placements=Fingerprint(combat,false);
                Check(run==0 || combat.RunSeed!=previousSeed && placements!=previous,"retry chooses a new seed and new physical placements");
                previousSeed=combat.RunSeed;previous=placements;
                for (int frame=0;frame<30;frame++)
                {
                    yield return null;
                    Check(combat.RunSeed==previousSeed && Fingerprint(combat)==fingerprint && LiveMatches(combat),"layout remains fixed during active run");
                }
            }
            combat.StopCombat();
            Finish("solo");
        }

        IEnumerator Networked(bool host)
        {
            var session=FindObjectOfType<MultiplayerSession>();session.Begin(host);
            yield return Wait(()=>NetworkManager.Singleton.IsConnectedClient && NetworkManager.Singleton.LocalClient.PlayerObject,"network connected");
            if (failed) {Finish(host ? "host" : "client");yield break;}
            var combat=CombatPlayground.Instance;
            var motor=NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<MomentumMotor>();motor.enabled=false;
            combat.AutoSpawn=combat.AutoFire=false;
            if (host)
            {
                yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==2,"party joined");
                if (failed) {Finish("host");yield break;}
                var peers=NetworkManager.Singleton.ConnectedClientsIds;
                ulong peer=peers[0]==NetworkManager.Singleton.LocalClientId ? peers[1] : peers[0];
                int previousSeed=0;
                for (int run=0;run<2;run++)
                {
                    combat.Begin();CheckHostGeometry(combat);
                    Check(run==0 || previousSeed!=combat.RunSeed,"host retry changes seed");previousSeed=combat.RunSeed;
                    typeof(CombatPlayground).GetProperty("Notice").SetValue(combat,$"RUN_LAYOUT_PROBE {run} {Fingerprint(combat)}");
                    yield return Wait(()=>
                    {
                        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(peer,out var client) || !client.PlayerObject) return false;
                        return (client.PlayerObject.transform.position-(CanyonLayout.BossConsole+Vector3.up*(1.1f+run*2))).sqrMagnitude<.25f;
                    },"client verified snapshot and acknowledged current console");
                    if (failed) break;
                    yield return new WaitForSecondsRealtime(.3f);
                }
                combat.StopCombat();yield return new WaitForSecondsRealtime(.5f);
            }
            else
            {
                int previousSeed=0;
                for (int run=0;run<2;run++)
                {
                    string prefix=$"RUN_LAYOUT_PROBE {run} ";
                    yield return Wait(()=>combat.Running && combat.ActiveRunLayout!=null && combat.Notice.StartsWith(prefix,StringComparison.Ordinal),"host layout message received");
                    if (failed) break;
                    string fingerprint=combat.Notice.Substring(prefix.Length);int seed=combat.RunSeed;
                    Check(fingerprint==Fingerprint(combat) && LiveMatches(combat),"host fingerprint and live coordinates match on client");
                    Check(run==0 || previousSeed!=seed,"client receives retry seed");previousSeed=seed;
                    Check(BossPropsMatch(combat),"client boss prop relocation");
                    bool stable=true;
                    for (int frame=0;frame<30;frame++)
                    {
                        yield return null;
                        stable &= seed==combat.RunSeed && Fingerprint(combat)==fingerprint && LiveMatches(combat);
                    }
                    Check(stable,"client layout stable for 30 frames");
                    if (failed) break;
                    // Distinct acknowledgement height prevents a repeated boss site
                    // from letting the host mistake the previous run for a new ACK.
                    motor.SetSpawn(CanyonLayout.BossConsole+Vector3.up*(1.1f+run*2));
                }
                yield return Wait(()=>!combat.Running,"host stop replicated");
            }
            session.Leave();yield return new WaitForSecondsRealtime(.3f);Finish(host ? "host" : "client");
        }

        void Finish(string role)
        {
            Debug.Log($"SWARM_RUN_LAYOUT_TEST: {(failed ? "FAIL" : "PASS")} role={role}");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
#endif
