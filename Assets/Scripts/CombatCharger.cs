using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Swarm
{
    public partial class CombatPlayground
    {
        enum ChargePhase { Pursuing, WindingUp, Rushing, Recovering }

        static Vector3 ChargerImpulse(Vector3 heading,Vector3 contactOffset)
        {
            Vector3 normal=Vector3.ProjectOnPlane(contactOffset,Vector3.up).normalized;
            if (normal.sqrMagnitude<.01f) normal=heading;
            float front=Mathf.Clamp01(Vector3.Dot(normal,heading));
            Vector3 direction=(heading*.65f+normal*.85f).normalized;
            return direction*Mathf.Lerp(3.25f,4.5f,front)+Vector3.up*1.2f;
        }

        void TickCharger(Enemy enemy,float dt,bool found,Vector3 destination)
        {
            if (!enemy.agent.isOnNavMesh) return;
            enemy.attackClock-=dt;
            if (enemy.chargePhase==ChargePhase.Pursuing)
            {
                enemy.agent.isStopped=!found;
                if (found) enemy.agent.SetDestination(destination);
                Vector3 delta=player.transform.position-(enemy.body.transform.position+Vector3.up);
                if (enemy.attackClock>0 || delta.magnitude<.5f || delta.magnitude>17 ||
                    Physics.Linecast(enemy.body.transform.position+Vector3.up,player.transform.position,WorldMask)) return;
                enemy.aim=Vector3.ProjectOnPlane(delta,Vector3.up).normalized;
                if (enemy.aim.sqrMagnitude<.01f) return;
                enemy.chargePhase=ChargePhase.WindingUp;
                enemy.attackClock=1;
                enemy.agent.isStopped=true;
                enemy.body.transform.rotation=Quaternion.LookRotation(enemy.aim);
                enemy.warning=new GameObject("Charger warning",typeof(LineRenderer));
                enemy.warning.transform.SetParent(enemy.body.transform);
                var line=enemy.warning.GetComponent<LineRenderer>();
                line.positionCount=2;
                line.startWidth=.18f;
                line.endWidth=.7f;
                line.sharedMaterial=chargerMaterial;
                Vector3 start=enemy.body.transform.position+Vector3.up*.12f;
                line.SetPosition(0,start);
                line.SetPosition(1,start+enemy.aim*16);
                return;
            }
            if (enemy.chargePhase==ChargePhase.WindingUp)
            {
                if (enemy.attackClock>0) return;
                SetChargerPlayerCollision(enemy,true);
                enemy.chargePhase=ChargePhase.Rushing;
                enemy.attackClock=.8f;
                enemy.chargeVictims.Clear();
            }
            if (enemy.chargePhase==ChargePhase.Rushing)
            {
                Vector3 from=enemy.body.transform.position;
                Vector3 next=from+enemy.aim*(20*dt);
                bool wall=NavMesh.Raycast(from,next,out var boundary,NavMesh.AllAreas);
                if (wall) next=boundary.position;
                enemy.agent.Move(next-from);
                Vector3 to=enemy.body.transform.position;
                ulong original=contextId;
                if (CoopAuthority)
                {
                    foreach (ulong id in new List<ulong>(party.Keys))
                    {
                        SwitchContext(id);
                        TryChargeHit(enemy,from,to,id);
                    }
                    SwitchContext(original);
                }
                else TryChargeHit(enemy,from,to,0);
                if (!wall && enemy.attackClock>0) return;
                SetChargerPlayerCollision(enemy,false);
                enemy.chargePhase=ChargePhase.Recovering;
                enemy.attackClock=wall ? 1.2f : .8f;
                Destroy(enemy.warning);
                enemy.warning=null;
                return;
            }
            if (enemy.attackClock>0) return;
            enemy.chargePhase=ChargePhase.Pursuing;
            enemy.attackClock=3.2f;
        }

        void SetChargerPlayerCollision(Enemy enemy,bool ignore)
        {
            var body=enemy.body.GetComponent<Collider>();
            if (!ignore)
            {
                foreach (var controller in enemy.ignoredChargeControllers)
                    if (controller) Physics.IgnoreCollision(body,controller,false);
                enemy.ignoredChargeControllers.Clear();
                return;
            }
            if (CoopAuthority)
            {
                foreach (var member in party.Values)
                {
                    var motor=MotorFor(member.id);
                    if (!motor) continue;
                    var controller=motor.GetComponent<CharacterController>();
                    Physics.IgnoreCollision(body,controller,true);
                    enemy.ignoredChargeControllers.Add(controller);
                }
            }
            else
            {
                var controller=player.GetComponent<CharacterController>();
                Physics.IgnoreCollision(body,controller,true);
                enemy.ignoredChargeControllers.Add(controller);
            }
        }

        void TryChargeHit(Enemy enemy,Vector3 from,Vector3 to,ulong id)
        {
            if (!player || Health<=0 || enemy.chargeVictims.Contains(id)) return;
            Vector3 segment=Vector3.ProjectOnPlane(to-from,Vector3.up);
            Vector3 offset=Vector3.ProjectOnPlane(player.transform.position-from,Vector3.up);
            float t=segment.sqrMagnitude>.0001f ? Mathf.Clamp01(Vector3.Dot(offset,segment)/segment.sqrMagnitude) : 0;
            Vector3 contact=Vector3.Lerp(from,to,t);
            Vector3 delta=player.transform.position-contact;
            if (delta.y<-.3f || delta.y>2.8f ||
                Vector3.ProjectOnPlane(delta,Vector3.up).sqrMagnitude>(enemy.elite ? 1.5f*1.5f : 1.15f*1.15f)) return;
            enemy.chargeVictims.Add(id);
            Vector3 impulse=ChargerImpulse(enemy.aim,delta);
            ApplyEnemyKnockbackHit(enemy,id,enemy.elite ? 30 : 18,impulse);
        }

        void ApplyEnemyKnockbackHit(Enemy enemy,ulong id,float damage,Vector3 impulse)
        {
            if (hurtClock>0) return;
            Health=Mathf.Max(0,Health-damage*enemy.damageScale);
            hurtClock=.75f;
            if (CoopAuthority && party.TryGetValue(id,out var state))
            {
                state.knockbackSerial++;
                state.knockback=impulse;
            }
            if (!NetworkActive || id==NetworkManager.Singleton.LocalClientId)
                player.ApplyImpulse(impulse);
            if (Health<=0) ReleaseMouse();
        }
    }
}
