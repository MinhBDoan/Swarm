using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Swarm
{
    public partial class CombatPlayground
    {
        enum SentinelPhase { Pursuing, Windup, Charging, ChargePound, Shockwave, SecondPound, Recovering }
        static readonly string[] SentinelMoves={ "CHARGE + POUND", "FRONT SWEEP", "SHOCKWAVE", "DOUBLE POUND" };
        string remoteSentinelMove="";
        string SentinelAttackText => IsCoopClient ? remoteSentinelMove :
            consoleSentinel!=null ? SentinelMoves[consoleSentinel.sentinelMove]+" / "+
                SentinelPhaseText(consoleSentinel.sentinelPhase) : "Sentinel active";

        static string SentinelPhaseText(SentinelPhase phase)
        {
            switch (phase)
            {
                case SentinelPhase.Windup: return "GET READY";
                case SentinelPhase.Charging: return "CHARGING";
                case SentinelPhase.ChargePound:
                case SentinelPhase.SecondPound: return "GROUND POUND";
                case SentinelPhase.Shockwave: return "JUMP OR EVADE";
                case SentinelPhase.Recovering: return "RECOVERING";
                default: return "APPROACHING";
            }
        }

        void TickSentinel(Enemy enemy,float dt,bool found,Vector3 destination)
        {
            if (!enemy.agent.isOnNavMesh) return;
            enemy.attackClock-=dt;
            if (enemy.sentinelPhase==SentinelPhase.Pursuing)
            {
                enemy.agent.isStopped=!found;
                if (found) enemy.agent.SetDestination(destination);
                float range=enemy.sentinelMove==0 || enemy.sentinelMove==2 ? 20 : 9;
                if (enemy.attackClock>0 || Vector3.Distance(enemy.body.transform.position,player.transform.position)>range) return;
                enemy.agent.isStopped=true;
                enemy.aim=Vector3.ProjectOnPlane(player.transform.position-enemy.body.transform.position,Vector3.up).normalized;
                if (enemy.aim.sqrMagnitude<.01f) enemy.aim=enemy.body.transform.forward;
                enemy.body.transform.rotation=Quaternion.LookRotation(enemy.aim);
                enemy.sentinelPhase=SentinelPhase.Windup;
                enemy.attackClock=enemy.sentinelMove==0 ? 1.2f : 1.1f;
                CreateSentinelWarning(enemy);
                return;
            }
            if (enemy.sentinelPhase==SentinelPhase.Windup)
            {
                if (enemy.attackClock>0) return;
                enemy.chargeVictims.Clear();
                if (enemy.sentinelMove==0)
                {
                    enemy.sentinelPhase=SentinelPhase.Charging;
                    enemy.sentinelDistance=14;
                    SetChargerPlayerCollision(enemy,true);
                }
                else if (enemy.sentinelMove==1)
                {
                    HitSentinelArea(enemy,7,32,5,true);
                    RecoverSentinel(enemy,1.5f);
                }
                else if (enemy.sentinelMove==2)
                {
                    enemy.sentinelPhase=SentinelPhase.Shockwave;
                    enemy.attackClock=1;
                    enemy.sentinelRadius=1.5f;
                }
                else
                {
                    HitSentinelArea(enemy,4.5f,45,5);
                    enemy.sentinelPhase=SentinelPhase.SecondPound;
                    enemy.attackClock=1;
                    DrawSentinelRing(enemy,7);
                }
                return;
            }
            if (enemy.sentinelPhase==SentinelPhase.Charging)
            {
                float step=Mathf.Min(16*dt,enemy.sentinelDistance);
                Vector3 from=enemy.body.transform.position;
                Vector3 next=from+enemy.aim*step;
                bool wall=NavMesh.Raycast(from,next,out var boundary,NavMesh.AllAreas);
                enemy.agent.Move((wall ? boundary.position : next)-from);
                enemy.sentinelDistance-=step;
                ulong original=contextId;
                if (CoopAuthority)
                {
                    foreach (ulong id in new List<ulong>(party.Keys))
                    {
                        SwitchContext(id);
                        TryChargeHit(enemy,from,enemy.body.transform.position,id);
                    }
                    SwitchContext(original);
                }
                else TryChargeHit(enemy,from,enemy.body.transform.position,0);
                if (!wall && enemy.sentinelDistance>.001f) return;
                enemy.sentinelPhase=SentinelPhase.ChargePound;
                enemy.attackClock=.8f;
                DrawSentinelRing(enemy,5.5f);
                return;
            }
            if (enemy.sentinelPhase==SentinelPhase.ChargePound)
            {
                if (enemy.attackClock>0) return;
                enemy.chargeVictims.Clear();
                HitSentinelArea(enemy,5.5f,40,5.5f);
                RecoverSentinel(enemy,1.7f);
                return;
            }
            if (enemy.sentinelPhase==SentinelPhase.Shockwave)
            {
                float previous=enemy.sentinelRadius;
                enemy.sentinelRadius=Mathf.Lerp(1.5f,12,1-Mathf.Clamp01(enemy.attackClock));
                DrawSentinelRing(enemy,enemy.sentinelRadius);
                HitSentinelArea(enemy,enemy.sentinelRadius+.65f,30,4.5f,false,Mathf.Max(0,previous-.65f));
                if (enemy.attackClock<=0) RecoverSentinel(enemy,1.7f);
                return;
            }
            if (enemy.sentinelPhase==SentinelPhase.SecondPound)
            {
                if (enemy.attackClock>0) return;
                enemy.chargeVictims.Clear();
                HitSentinelArea(enemy,7,35,5.5f);
                RecoverSentinel(enemy,1.8f);
                return;
            }
            if (enemy.attackClock>0) return;
            SetChargerPlayerCollision(enemy,false);
            enemy.sentinelMove=(enemy.sentinelMove+1)%SentinelMoves.Length;
            enemy.sentinelPhase=SentinelPhase.Pursuing;
            enemy.attackClock=1.8f;
        }

        void RecoverSentinel(Enemy enemy,float seconds)
        {
            enemy.sentinelPhase=SentinelPhase.Recovering;
            enemy.attackClock=seconds;
            Destroy(enemy.warning);
            enemy.warning=null;
        }

        void CreateSentinelWarning(Enemy enemy)
        {
            Destroy(enemy.warning);
            enemy.warning=new GameObject("Sentinel "+SentinelMoves[enemy.sentinelMove],typeof(LineRenderer));
            enemy.warning.transform.SetParent(enemy.body.transform);
            var line=enemy.warning.GetComponent<LineRenderer>();
            line.startWidth=line.endWidth=.2f;
            line.sharedMaterial=enemy.sentinelMove==2 ? bulletMaterial : blastMaterial;
            if (enemy.sentinelMove==0)
            {
                line.loop=true;
                line.positionCount=4;
                Vector3 side=Vector3.Cross(Vector3.up,enemy.aim)*1.5f;
                Vector3 start=enemy.body.transform.position+Vector3.up*.15f;
                line.SetPosition(0,start-side);
                line.SetPosition(1,start+side);
                line.SetPosition(2,start+side+enemy.aim*14);
                line.SetPosition(3,start-side+enemy.aim*14);
            }
            else DrawSentinelRing(enemy,enemy.sentinelMove==1 ? 7 : enemy.sentinelMove==2 ? 1.5f : 4.5f,enemy.sentinelMove==1);
        }

        void DrawSentinelRing(Enemy enemy,float radius,bool cone=false)
        {
            var line=enemy.warning.GetComponent<LineRenderer>();
            line.loop=true;
            const int segments=48;
            line.positionCount=cone ? segments+2 : segments;
            Vector3 origin=enemy.body.transform.position;
            for (int i=0;i<line.positionCount;i++)
            {
                Vector3 offset;
                if (cone && (i==0 || i==line.positionCount-1)) offset=Vector3.zero;
                else
                {
                    float angle=cone ? -60+(i-1)*120f/(segments-1) : i*360f/segments;
                    offset=Quaternion.Euler(0,angle,0)*(cone ? enemy.aim : Vector3.forward)*radius;
                }
                Vector3 point=origin+offset+Vector3.up*.15f;
                if (Physics.Raycast(point+Vector3.up*3,Vector3.down,out var ground,10,WorldMask))
                    point.y=ground.point.y+.15f;
                line.SetPosition(i,point);
            }
        }

        void HitSentinelArea(Enemy enemy,float radius,float damage,float shove,bool cone=false,float inner=0)
        {
            if (inner<=0) Spark(enemy.body.transform.position+Vector3.up*.2f,1,radius,.3f,blastMaterial);
            ulong original=contextId;
            if (CoopAuthority)
            {
                foreach (ulong id in new List<ulong>(party.Keys))
                {
                    SwitchContext(id);
                    TrySentinelAreaHit(enemy,radius,damage,shove,cone,inner,id);
                }
                SwitchContext(original);
            }
            else TrySentinelAreaHit(enemy,radius,damage,shove,cone,inner,0);
        }

        void TrySentinelAreaHit(Enemy enemy,float radius,float damage,float shove,bool cone,float inner,ulong id)
        {
            if (!player || Health<=0 || enemy.chargeVictims.Contains(id)) return;
            Vector3 origin=enemy.body.transform.position;
            if (!Physics.Raycast(player.transform.position+Vector3.up*.1f,Vector3.down,out var floor,4,WorldMask) ||
                player.transform.position.y-floor.point.y>2.1f) return;
            Vector3 delta=player.transform.position-origin;
            Vector3 flat=Vector3.ProjectOnPlane(delta,Vector3.up);
            if (delta.y< -2 || delta.y>2.5f || flat.magnitude>radius || flat.magnitude<inner ||
                cone && flat.sqrMagnitude>.01f && Vector3.Dot(flat.normalized,enemy.aim)<.5f ||
                Physics.Linecast(origin+Vector3.up*.8f,player.transform.position,WorldMask)) return;
            enemy.chargeVictims.Add(id);
            Vector3 direction=flat.sqrMagnitude>.01f ? flat.normalized : enemy.aim;
            ApplyEnemyKnockbackHit(enemy,id,damage,direction*shove+Vector3.up*1.5f);
        }
    }
}
