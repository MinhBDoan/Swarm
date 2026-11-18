using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace Swarm
{
    public partial class CombatPlayground
    {
        public enum DownloadPhase { Available, Active, Completed }
        readonly DownloadPhase[] downloadStates=new DownloadPhase[CanyonLayout.DownloadCount];
        readonly float[] downloadPercents=new float[CanyonLayout.DownloadCount];
        int downloadIndex=2;
        public DownloadPhase DownloadState { get=>downloadStates[downloadIndex]; private set=>downloadStates[downloadIndex]=value; }
        public float DownloadProgress { get=>downloadPercents[downloadIndex]; private set=>downloadPercents[downloadIndex]=value; }
        public int DownloadOccupants { get; private set; }
        public int DownloadEnemiesSpawned { get; private set; }
        public int SelectedDownload => downloadIndex;
        public DownloadPhase DownloadStateAt(int index) => downloadStates[index];
        public float DownloadProgressAt(int index) => downloadPercents[index];
        Vector3 SelectedDownloadCenter => CanyonLayout.DownloadCenters[downloadIndex];
        // Relocate scene-owned props only after the shared layout changes. They are not
        // combat snapshot entities, so clients retain one authoritative scene copy.
        public void RefreshDownloadLocations()
        {
            foreach(var terminal in FindObjectsOfType<DownloadTerminal>()) terminal.RefreshLocation();
        }
        int NearestDownloadIndex()
        {
            int nearest=0; float distance=float.MaxValue;
            for(int i=0;i<CanyonLayout.DownloadCount;i++) { float next=(player.transform.position-CanyonLayout.DownloadCenters[i]).sqrMagnitude; if(next<distance) {nearest=i;distance=next;} }
            return nearest;
        }
        int DownloadViewIndex => DownloadEngaged ? downloadIndex : NearestDownloadIndex();
        bool DownloadEngaged => DownloadState==DownloadPhase.Active && (DownloadOccupants>0 || DownloadProgress>0);
        public float DownloadSpeedMultiplier => DownloadOccupants>0 ? 1+.1f*(Mathf.Min(6,DownloadOccupants)-1) : 0;
        const int DownloadPackSize = 3;
        const int DownloadGuardLimit = 6;
        const int DownloadEnemyLimit = 24;
        float downloadEmptyClock,downloadSpawnClock;
        readonly List<Enemy> downloadEnemies=new List<Enemy>();
        void ResetDownload()
        {
            downloadIndex=2; for(int i=0;i<CanyonLayout.DownloadCount;i++) {downloadStates[i]=DownloadPhase.Available;downloadPercents[i]=0;}
            DownloadOccupants=DownloadEnemiesSpawned=0;
            downloadEmptyClock=downloadSpawnClock=0; downloadEnemies.Clear();
        }
        public static bool InsideDownloadZone(MomentumMotor motor,int terminal=2)
        {
            if (!motor || motor.IsZiplining || terminal<0 || terminal>=CanyonLayout.DownloadCount) return false;
            Vector3 delta=motor.transform.position-CanyonLayout.DownloadCenters[terminal];
            return new Vector2(delta.x,delta.z).sqrMagnitude<=CanyonLayout.DownloadRadius*CanyonLayout.DownloadRadius && delta.y>=-.3f && delta.y<=3.2f;
        }
        int CountDownloadOccupants(int terminal)
        {
            if (!CoopAuthority) return Health>0 && InsideDownloadZone(player,terminal) ? 1 : 0;

            int count=0;
            foreach (var state in party.Values)
                if (state.health>0 && InsideDownloadZone(MotorFor(state.id),terminal)) count++;
            return count;
        }
        void BeginDownload(int terminal,int occupants)
        {
            if (DownloadState==DownloadPhase.Active) DownloadState=DownloadPhase.Available;
            downloadIndex=terminal;
            DownloadEnemiesSpawned=0;
            DownloadState=DownloadPhase.Active;
            DownloadProgress=0;
            DownloadOccupants=occupants;
            downloadEmptyClock=0;
            downloadSpawnClock=1.5f;
            Notice=$"{(UpgradeRarity)CanyonLayout.DownloadTierIndex(terminal)} download started. Stay inside the circle.";
        }
        void TickDownload(float dt)
        {
            if (!Running || IsCoopClient || Won || ChoosingUpgrade || PartyPaused || (CoopAuthority ? PartyDefeated : Health<=0)) return;
            if (CoopAuthority) SaveContext();
            DownloadOccupants=DownloadState==DownloadPhase.Active ? CountDownloadOccupants(downloadIndex) : 0;
            if (!DownloadEngaged)
            {
                for (int terminal=0;terminal<downloadStates.Length;terminal++)
                {
                    if (downloadStates[terminal]!=DownloadPhase.Available) continue;
                    int occupants=CountDownloadOccupants(terminal);
                    if (occupants==0) continue;
                    BeginDownload(terminal,occupants);
                    break;
                }
            }
            if (DownloadState!=DownloadPhase.Active) return;
            if (DownloadOccupants>0)
            {
                downloadEmptyClock=0;
                DownloadProgress=Mathf.Min(100,DownloadProgress+dt*(100/CanyonLayout.DownloadDuration(downloadIndex))*DownloadSpeedMultiplier);
            }
            else
            {
                float before=Mathf.Max(0,downloadEmptyClock-2); downloadEmptyClock+=dt;
                DownloadProgress=Mathf.Max(0,DownloadProgress-2*(Mathf.Max(0,downloadEmptyClock-2)-before));
            }
            if (DownloadProgress>=100)
            {
                DownloadState=DownloadPhase.Completed;
                Notice="Download complete! Everyone receives an extra upgrade choice.";
                if(CoopAuthority) GivePartyDownloadReward((UpgradeRarity)CanyonLayout.DownloadTierIndex(downloadIndex));
                else QueueBonusUpgrade(CanyonLayout.DownloadTierIndex(downloadIndex));
                return;
            }
            if (!AutoSpawn || DownloadOccupants==0) return;
            downloadSpawnClock-=dt;
            if (downloadSpawnClock>0) return;
            SpawnDownloadPack();
        }

        void SpawnDownloadPack()
        {
            downloadEnemies.RemoveAll(enemy=>!enemies.Contains(enemy));
            int count=Mathf.Min(DownloadPackSize,DownloadGuardLimit-downloadEnemies.Count,DownloadEnemyLimit-enemies.Count);
            if (count<=0)
            {
                downloadSpawnClock=1;
                return;
            }

            ulong original=contextId;
            Vector3 center=SelectedDownloadCenter;
            if (CoopAuthority) SwitchToNearest(center);
            int spawned=0;
            const int placements=16;
            int start=(DownloadEnemiesSpawned*3)%placements;
            for (int attempt=0;attempt<placements && spawned<count;attempt++)
            {
                float angle=(start+attempt)*Mathf.PI*2/placements;
                Vector3 point=center+new Vector3(Mathf.Sin(angle)*18,0,Mathf.Cos(angle)*18);
                if (!NavMesh.SamplePosition(point,out var placement,4,NavMesh.AllAreas) ||
                    PlayerNearRouteSpawn(placement.position,12) ||
                    downloadEnemies.Exists(enemy=>(enemy.body.transform.position-placement.position).sqrMagnitude<4)) continue;

                var kind=DownloadEnemiesSpawned%3==0 ? EnemyKind.Runner :
                    DownloadEnemiesSpawned%3==1 ? EnemyKind.Chaser : EnemyKind.Spitter;
                if (!SpawnEnemy(placement.position,false,kind)) continue;
                downloadEnemies.Add(enemies[enemies.Count-1]);
                DownloadEnemiesSpawned++;
                spawned++;
            }
            if (CoopAuthority) SwitchContext(original);
            downloadSpawnClock=spawned>0 ? 4 : 1;
        }
        void GivePartyDownloadReward(UpgradeRarity tier)
        {
            ulong original=contextId;
            foreach(ulong id in new List<ulong>(party.Keys)) { SwitchContext(id); QueueBonusUpgrade((int)tier); }
            SwitchContext(original);
        }
        bool ShowDownloadObjective => Running && player && (DownloadEngaged || Vector3.Distance(player.transform.position,CanyonLayout.DownloadCenters[NearestDownloadIndex()])<24);
        string DownloadStatus()
        {
            int index=DownloadViewIndex; var state=downloadStates[index];
            if(state==DownloadPhase.Completed) return "COMPLETE / party upgrade earned";
            if(state==DownloadPhase.Available) return "Enter the circle to start. Expect enemy packs.";
            if(DownloadOccupants>0) return $"{DownloadProgress:0}% / {DownloadOccupants} inside / {DownloadSpeedMultiplier:0.0}x speed\nEnemy packs incoming";
            return downloadEmptyClock<2 ? $"{DownloadProgress:0}% / empty / decay in {2-downloadEmptyClock:0.0}s" : $"{DownloadProgress:0}% / empty / losing 2% per second";
        }
    }
}
