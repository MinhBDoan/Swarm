using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace Swarm
{
    public partial class CombatPlayground
    {
        public enum RouteStage { Waiting, Warning, Arriving, Fighting, Cleared }
        public static readonly Vector3[] RouteTriggers={new Vector3(28,-2,200),new Vector3(7,1,535)};
        static readonly string[] routeNames={"BRIDGE SCOUTS","STATION OUTRIDERS"};
        readonly RouteStage[] routeStages=new RouteStage[2];
        readonly float[] routeTimers=new float[2],routeDeadlines=new float[2];
        readonly int[] routeSpawned=new int[2],remoteRouteRemaining=new int[2];
        readonly List<Enemy>[] routeEnemies={new List<Enemy>(),new List<Enemy>()};
        public int RouteEnemiesSpawned => routeSpawned[0]+routeSpawned[1];
        public RouteStage RouteEncounterStage(int index) => routeStages[index];
        public int RouteEnemiesRemaining(int index)
        {
            if (IsCoopClient) return remoteRouteRemaining[index];
            int count=0; foreach (var enemy in routeEnemies[index]) if (enemies.Contains(enemy)) count++; return count;
        }
        bool RouteEncounterBusy
        {
            get { foreach (var stage in routeStages) if (stage==RouteStage.Warning || stage==RouteStage.Arriving || stage==RouteStage.Fighting) return true; return false; }
        }
        void ResetRouteEncounters()
        {
            for (int i=0;i<2;i++) { routeStages[i]=RouteStage.Waiting; routeTimers[i]=routeDeadlines[i]=0; routeSpawned[i]=remoteRouteRemaining[i]=0; routeEnemies[i].Clear(); }
        }
        bool PlayerNearRouteSpawn(Vector3 point,float distance)
        {
            if (!CoopAuthority) return Vector3.ProjectOnPlane(player.transform.position-point,Vector3.up).sqrMagnitude<distance*distance;
            foreach (var state in party.Values)
            {
                var motor=MotorFor(state.id);
                if (state.health>0 && motor && Vector3.ProjectOnPlane(motor.transform.position-point,Vector3.up).sqrMagnitude<distance*distance) return true;
            }
            return false;
        }
        void TickRouteEncounters(float dt)
        {
            if (!Running || Won || IsCoopClient) return;
            ulong original=contextId;
            for (int i=0;i<2;i++)
            {
                var activator=CoopAuthority ? NearestLiving(RouteTriggers[i]) : player;
                if (!activator) continue;
                if (routeStages[i]==RouteStage.Waiting)
                {
                    if (RouteEncounterBusy || enemies.Count>12 || Vector3.ProjectOnPlane(activator.transform.position-RouteTriggers[i],Vector3.up).magnitude>20) continue;
                    routeStages[i]=RouteStage.Warning; routeTimers[i]=1.6f;
                    Notice=routeNames[i]+" ahead! Keep moving or regroup to fight.";
                    continue;
                }
                if (routeStages[i]==RouteStage.Warning)
                {
                    routeTimers[i]-=dt; if (routeTimers[i]>0) continue;
                    routeStages[i]=RouteStage.Arriving; routeTimers[i]=0; routeDeadlines[i]=12;
                }
                if (routeStages[i]==RouteStage.Arriving)
                {
                    routeTimers[i]-=dt; routeDeadlines[i]-=dt;
                    int quota=i==0 ? 5 : 4;
                    if (routeTimers[i]<=0 && routeSpawned[i]<quota)
                    {
                        if (CoopAuthority) SwitchContext(activator.GetComponent<NetworkAvatar>().OwnerClientId);
                        bool spawned=false;
                        for (int attempt=0;attempt<8;attempt++)
                        {
                            float t=(routeSpawned[i]+attempt)%8/7f;
                            Vector3 point=i==0 ? Vector3.Lerp(new Vector3(26,-2,240),new Vector3(-12,-2,282),t) : Vector3.Lerp(new Vector3(-5,0,560),new Vector3(0,0,595),t);
                            point.x+=(routeSpawned[i]%2==0 ? -4 : 4);
                            if (!NavMesh.SamplePosition(point,out var placement,6,NavMesh.AllAreas) || PlayerNearRouteSpawn(placement.position,12)) continue;
                            point=placement.position;
                            EnemyKind kind=routeSpawned[i]==quota-1 ? EnemyKind.Spitter : routeSpawned[i]%2==0 ? EnemyKind.Runner : EnemyKind.Chaser;
                            if (!SpawnEnemy(point,false,kind)) continue;
                            routeEnemies[i].Add(enemies[enemies.Count-1]); routeSpawned[i]++; spawned=true; break;
                        }
                        routeTimers[i]=spawned ? .75f : .4f;
                        if (CoopAuthority) SwitchContext(original);
                    }
                    if (routeSpawned[i]>=quota || routeDeadlines[i]<=0) routeStages[i]=RouteStage.Fighting;
                }
                if (routeStages[i]==RouteStage.Fighting && RouteEnemiesRemaining(i)==0)
                {
                    routeStages[i]=RouteStage.Cleared;
                    Notice=routeNames[i]+" cleared. Collect XP and keep heading toward the station.";
                }
            }
            if (CoopAuthority) SwitchContext(original);
        }
        string RouteEncounterStatus()
        {
            for (int i=0;i<2;i++)
            {
                if (routeStages[i]==RouteStage.Warning) return routeNames[i]+$" / arriving in {Mathf.CeilToInt(routeTimers[i])}s";
                if (routeStages[i]==RouteStage.Arriving) return routeNames[i]+" / reinforcements approaching";
                if (routeStages[i]==RouteStage.Fighting) return routeNames[i]+$" / {RouteEnemiesRemaining(i)} remaining";
            }
            return "Follow the canyon / optional upper routes";
        }
    }
}
