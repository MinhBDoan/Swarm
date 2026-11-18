using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
namespace Swarm
{
    public partial class CombatPlayground
    {
        [Serializable] class DrillPacket { public float time,health; public bool paused,respawn; public int serial; public Vector3 impulse; }
        class DrillPatient { public float health=100; public int serial; public bool respawn; public Vector3 impulse; }
        readonly Dictionary<ulong,DrillPatient> drillPatients=new Dictionary<ulong,DrillPatient>();
        int drillSeenSerial;
        float drillSendClock;
        bool drillWasNetworked;
        public float DrillPracticeHealth { get; private set; }=100;
        public int DrillPlayerHits { get; private set; }
        public int DrillEnemyHits { get; private set; }
        public int ReceivedDrillStates { get; private set; }
        public bool DrillWorldPaused => NetworkActive ? Running && (LocalMenuOpen || PartyPaused || PartyDefeated || Won) :
            Running && (LocalMenuOpen || ChoosingUpgrade || Health<=0 || Won) || !Application.isBatchMode && !ShopOpen && Cursor.lockState!=CursorLockMode.Locked;
        public void TickDrill(DrillHazard drill,float dt)
        {
            if (drillWasNetworked!=NetworkActive) { drillPatients.Clear(); drillSeenSerial=0; DrillPracticeHealth=100; drillWasNetworked=NetworkActive; }
            if (!drill.Paused)
            {
                ulong original=contextId;
                if (Running)
                    foreach (var enemy in enemies.ToArray())
                        if (drill.TryStrike(enemy.body.GetInstanceID(),enemy.body.transform.position+Vector3.up*(enemy.elite ? 1.6f : 1),enemy.elite ? 1.6f : 1,enemy.elite ? .8f : .5f))
                        {
                            DrillEnemyHits++; Hit(enemy,100);
                            if (enemy.body && enemy.body.activeSelf && enemy.agent.isOnNavMesh)
                            {
                                Vector3 away=Vector3.ProjectOnPlane(enemy.body.transform.position-drill.Center,Vector3.up).normalized;
                                if (NavMesh.SamplePosition(enemy.body.transform.position+away*3,out var hit,4,NavMesh.AllAreas)) enemy.agent.Warp(hit.position);
                            }
                        }
                if (NetworkActive)
                {
                    foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
                    {
                        var motor=MotorFor(id); if (!motor) continue;
                        if (CoopAuthority) { SwitchContext(id); if (Health<=0) continue; }
                        if (drill.TryStrike(motor.GetInstanceID(),motor.transform.position,1,.45f)) ApplyDrillHit(drill,motor,id);
                    }
                    if (CoopAuthority) SwitchContext(original);
                }
                else
                {
                    ResolveLocalPlayer();
                    if (player && (!Running || Health>0) && drill.TryStrike(player.GetInstanceID(),player.transform.position,1,.45f)) ApplyDrillHit(drill,player,0);
                }
            }
            if (!NetworkActive || IsCoopClient || !boundManager) return;
            drillSendClock-=dt;
            if (drillSendClock>0) return;
            drillSendClock=.1f;
            foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (id==NetworkManager.Singleton.LocalClientId) continue;
                var patient=DrillPatientFor(id);
                SendJson("swarm.drill.state",id,JsonUtility.ToJson(new DrillPacket {time=drill.CycleTime,paused=drill.Paused,health=patient.health,
                    serial=patient.serial,impulse=patient.impulse,respawn=patient.respawn}));
            }
        }
        DrillPatient DrillPatientFor(ulong id)
        {
            if (!drillPatients.TryGetValue(id,out var patient)) { patient=new DrillPatient(); drillPatients.Add(id,patient); }
            return patient;
        }
        void ApplyDrillHit(DrillHazard drill,MomentumMotor motor,ulong id)
        {
            var patient=DrillPatientFor(id); patient.serial++; patient.impulse=drill.EscapeImpulse(motor.transform.position); patient.respawn=false;
            DrillPlayerHits++;
            if (Running) { Health=Mathf.Max(0,Health-30); hurtClock=Mathf.Max(hurtClock,.75f); }
            else
            {
                patient.health=Mathf.Max(0,patient.health-30);
                if (patient.health<=0) { patient.health=100; patient.respawn=true; }
            }
            if (!NetworkActive || id==NetworkManager.Singleton.LocalClientId)
            {
                DrillPracticeHealth=patient.health;
                if (patient.respawn) motor.Respawn(); else motor.ApplyImpulse(patient.impulse);
                if (Running && Health<=0) ReleaseMouse();
            }
        }
        void ReceiveDrillState(ulong sender,FastBufferReader reader)
        {
            if (!IsCoopClient || sender!=NetworkManager.ServerClientId || !DrillHazard.Instance) return;
            if (!drillWasNetworked) { drillWasNetworked=true; drillSeenSerial=0; }
            var packet=JsonUtility.FromJson<DrillPacket>(ReadJson(reader));
            DrillHazard.Instance.AcceptState(packet.time,packet.paused); DrillPracticeHealth=packet.health; ReceivedDrillStates++;
            ResolveLocalPlayer();
            if (packet.serial!=drillSeenSerial && player)
            { drillSeenSerial=packet.serial; if (packet.respawn) player.Respawn(); else player.ApplyImpulse(packet.impulse); }
        }
    }
}
