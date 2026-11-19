using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Swarm
{
    public partial class CombatPlayground
    {
        public enum EliteChallengePhase { Available, Arriving, Fighting, Completed }
        public static readonly Vector3[] ChallengeLocations={new Vector3(-125,-2.75f,190),new Vector3(-183,-4,395),new Vector3(-135,-2.6f,525)};
        public const float EliteChallengeInteractRange=4;
        const int EliteChallengePackSize=2;
        readonly EliteChallengePhase[] eliteChallengeStates=new EliteChallengePhase[3];
        readonly int[] eliteChallengeSpawned=new int[3],eliteChallengeQuotas=new int[3],eliteChallengeChestIndices={-1,-1,-1};
        readonly List<Enemy>[] eliteChallengeGuards={new List<Enemy>(),new List<Enemy>(),new List<Enemy>()};
        readonly int[] remoteEliteChallengeRemaining=new int[3];
        readonly GameObject[] eliteChallengeMarkers=new GameObject[3];
        Material eliteChallengeMaterial;
        int activeEliteChallenge=-1;
        float eliteChallengeSpawnClock;
        public int ActiveEliteChallenge => activeEliteChallenge;
        public int EliteChallengeRewardsSpawned {get;private set;}
        public EliteChallengePhase EliteChallengeStateAt(int index)=>eliteChallengeStates[index];
        public int EliteChallengeSpawned(int index)=>eliteChallengeSpawned[index];
        public int EliteChallengeQuota(int index)=>eliteChallengeQuotas[index];
        public int EliteChallengeRemaining(int index)
        {
            if(IsCoopClient)return remoteEliteChallengeRemaining[index];
            int count=0;foreach(var enemy in eliteChallengeGuards[index])if(enemies.Contains(enemy))count++;return count;
        }
        [Serializable] public class EliteChallengeSnapshot
        {
            public int active=-1,rewards;
            public int[] states,spawned,remaining,quotas;
        }
        EliteChallengeSnapshot BuildChallengeSnapshot()
        {
            return new EliteChallengeSnapshot {active=activeEliteChallenge,rewards=EliteChallengeRewardsSpawned,
                states=Array.ConvertAll(eliteChallengeStates,value=>(int)value),spawned=(int[])eliteChallengeSpawned.Clone(),
                remaining=new[]{EliteChallengeRemaining(0),EliteChallengeRemaining(1),EliteChallengeRemaining(2)},quotas=(int[])eliteChallengeQuotas.Clone()};
        }
        void AcceptChallengeSnapshot(EliteChallengeSnapshot snapshot)
        {
            if(snapshot==null || snapshot.states==null || snapshot.states.Length!=3 || snapshot.spawned==null || snapshot.spawned.Length!=3 ||
                snapshot.remaining==null || snapshot.remaining.Length!=3 || snapshot.quotas==null || snapshot.quotas.Length!=3)return;
            activeEliteChallenge=snapshot.active>=0 && snapshot.active<3 ? snapshot.active : -1;
            EliteChallengeRewardsSpawned=snapshot.rewards;
            for(int i=0;i<3;i++)
            {
                eliteChallengeStates[i]=(EliteChallengePhase)Mathf.Clamp(snapshot.states[i],0,3);
                eliteChallengeSpawned[i]=Mathf.Max(0,snapshot.spawned[i]);eliteChallengeQuotas[i]=Mathf.Max(0,snapshot.quotas[i]);
                remoteEliteChallengeRemaining[i]=Mathf.Max(0,snapshot.remaining[i]);
            }
        }
        void ResetEliteChallenges()
        {
            activeEliteChallenge=-1;eliteChallengeSpawnClock=0;EliteChallengeRewardsSpawned=0;
            for(int i=0;i<3;i++)
            {
                eliteChallengeStates[i]=EliteChallengePhase.Available;eliteChallengeSpawned[i]=eliteChallengeQuotas[i]=remoteEliteChallengeRemaining[i]=0;
                eliteChallengeChestIndices[i]=-1;eliteChallengeGuards[i].Clear();eliteChallengeMarkers[i]=null;
            }
        }
        void CreateEliteChallenges()
        {
            if(!root || IsCoopClient)return;
            if(!eliteChallengeMaterial)eliteChallengeMaterial=MakeMaterial(new Color(1,.22f,.42f));
            for(int i=0;i<3;i++)
            {
                var pedestal=GameObject.CreatePrimitive(PrimitiveType.Cylinder);pedestal.name="Elite challenge "+(i+1);pedestal.layer=2;
                pedestal.transform.SetParent(root);pedestal.transform.position=ChallengeLocations[i]+Vector3.up*.18f;
                pedestal.transform.localScale=new Vector3(2.2f,.18f,2.2f);Destroy(pedestal.GetComponent<Collider>());
                pedestal.GetComponent<Renderer>().sharedMaterial=eliteChallengeMaterial;eliteChallengeMarkers[i]=pedestal;
                var beacon=GameObject.CreatePrimitive(PrimitiveType.Cube);beacon.name="Elite challenge beacon";beacon.layer=2;
                beacon.transform.SetParent(root);beacon.transform.position=ChallengeLocations[i]+Vector3.up*1.4f;
                beacon.transform.localScale=new Vector3(.45f,2,.45f);Destroy(beacon.GetComponent<Collider>());
                beacon.GetComponent<Renderer>().sharedMaterial=eliteChallengeMaterial;
            }
        }
        void DisposeEliteChallenges(){if(eliteChallengeMaterial)Destroy(eliteChallengeMaterial);}
        int NearestEliteChallenge(float radius)
        {
            if(!player)return -1;
            float best=radius*radius;int nearest=-1;
            for(int i=0;i<3;i++)
            {
                float distance=(player.transform.position-(ChallengeLocations[i]+Vector3.up)).sqrMagnitude;
                if(distance<best){best=distance;nearest=i;}
            }
            return nearest;
        }
        public bool TryEliteChallenge(int index=-1)
        {
            if(!Running || Won || Health<=0 || ChoosingUpgrade || PartyPaused || LocalMenuOpen || !player || player.IsZiplining)return false;
            if(index<0)index=NearestEliteChallenge(EliteChallengeInteractRange);
            if(index<0 || index>=3 || eliteChallengeStates[index]!=EliteChallengePhase.Available || activeEliteChallenge>=0 ||
                Vector3.Distance(player.transform.position,ChallengeLocations[index]+Vector3.up)>EliteChallengeInteractRange ||
                Physics.Linecast(player.transform.position,ChallengeLocations[index]+Vector3.up,WorldMask))return false;
            if(IsCoopClient){SendCombatCommand("elite-challenge",index);return true;}
            eliteChallengeQuotas[index]=Mathf.Min(20,10+2*ExtraPartyMembers);
            eliteChallengeSpawned[index]=0;eliteChallengeGuards[index].Clear();eliteChallengeStates[index]=EliteChallengePhase.Arriving;
            activeEliteChallenge=index;eliteChallengeSpawnClock=.5f;
            Notice="Elite challenge activated! Defeat every reinforcement for a free party chest.";
            snapshotClock=0;return true;
        }
        void TickEliteChallenges(float dt)
        {
            if(!Running || IsCoopClient || Won || PartyPaused || ChoosingUpgrade || LocalMenuOpen ||
                (CoopAuthority ? PartyDefeated : Health<=0) || dt<=0 || activeEliteChallenge<0)return;
            int index=activeEliteChallenge;
            eliteChallengeGuards[index].RemoveAll(enemy=>!enemies.Contains(enemy));
            if(eliteChallengeStates[index]==EliteChallengePhase.Arriving)
            {
                eliteChallengeSpawnClock-=dt;
                if(eliteChallengeSpawnClock<=0)
                {
                    SpawnEliteChallengePack(index);
                    // Solo: five packs across ~9s. Full party: ten packs across ~10s.
                    float packs=Mathf.Ceil(eliteChallengeQuotas[index]/(float)EliteChallengePackSize);
                    eliteChallengeSpawnClock=10/Mathf.Max(1,packs);
                }
                if(eliteChallengeSpawned[index]>=eliteChallengeQuotas[index])eliteChallengeStates[index]=EliteChallengePhase.Fighting;
            }
            if(eliteChallengeStates[index]!=EliteChallengePhase.Fighting || EliteChallengeRemaining(index)>0)return;
            if(eliteChallengeChestIndices[index]<0)
            {
                int chest=SpawnFreeRewardChest(ChallengeLocations[index]+Vector3.right*2.5f,UpgradeRarity.Epic,true);
                if(chest<0)return; // Keep the earned reward pending if a temporary allocation limit is hit.
                eliteChallengeChestIndices[index]=chest;EliteChallengeRewardsSpawned++;
            }
            eliteChallengeStates[index]=EliteChallengePhase.Completed;activeEliteChallenge=-1;
            var marker=eliteChallengeMarkers[index];
            if(marker)marker.transform.localScale=new Vector3(2.2f,.08f,2.2f);
            Notice="Elite challenge cleared! A free Epic party chest awaits at the challenge spot.";
            snapshotClock=0;
        }
        void SpawnEliteChallengePack(int index)
        {
            ulong original=contextId;
            if(CoopAuthority)
            {
                var nearest=NearestLiving(ChallengeLocations[index]);
                if(!nearest)return;
                SwitchContext(nearest.GetComponent<NetworkAvatar>().OwnerClientId);
            }
            try
            {
                for(int slot=0;slot<EliteChallengePackSize && eliteChallengeSpawned[index]<eliteChallengeQuotas[index];slot++)
                {
                    EnemyKind kind=(EnemyKind)(eliteChallengeSpawned[index]%4);
                    for(int attempt=0;attempt<16;attempt++)
                    {
                        float angle=(eliteChallengeSpawned[index]*2.39996f+attempt*Mathf.PI*.25f);
                        float radius=14+(attempt%3)*5;
                        Vector3 candidate=ChallengeLocations[index]+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                        if(!NavMesh.SamplePosition(candidate,out var hit,4,NavMesh.AllAreas) || PlayerNearRouteSpawn(hit.position,10))continue;
                        if(!SpawnEliteEnemy(hit.position,kind,true))continue;
                        var guard=enemies[enemies.Count-1];guard.challengeElite=true;
                        eliteChallengeGuards[index].Add(guard);eliteChallengeSpawned[index]++;break;
                    }
                }
            }
            finally {if(CoopAuthority)SwitchContext(original);}
        }
        public bool ShowEliteChallengeObjective => Running && (activeEliteChallenge>=0 || NearestEliteChallenge(7)>=0);
        public void DrawEliteChallengeObjective(float width)
        {
            int index=activeEliteChallenge>=0 ? activeEliteChallenge : NearestEliteChallenge(7);if(index<0)return;
            var rect=new Rect(width-330,22,308,165);var color=new Color(1,.4f,.5f);
            HudPanelBox(rect,color);HudLabel(new Rect(rect.x+16,35,276,27),"ELITE CHALLENGE",hudSmall,color);
            string detail=eliteChallengeStates[index]==EliteChallengePhase.Completed ? "CLEARED / free Epic party chest earned" :
                eliteChallengeStates[index]==EliteChallengePhase.Available ? "Fight an elite reinforcement squad.\nReward: free Epic chest for the party." :
                $"{EliteChallengeSpawned(index)}/{EliteChallengeQuota(index)} arrived / {EliteChallengeRemaining(index)} still alive\nDefeat all reinforcements. Normal waves continue.";
            HudLabel(new Rect(rect.x+16,70,276,72),detail,hudText);
            HudLabel(new Rect(rect.x+16,145,276,30),eliteChallengeStates[index]==EliteChallengePhase.Available ?
                NearestEliteChallenge(EliteChallengeInteractRange)==index ? "E: ACTIVATE CHALLENGE" : "Move closer to activate" : "No time limit / no automatic failure",hudSmall,color);
        }
        public void DrawChallengeLabels()
        {
            if(!Running || !Camera.main || !player)return;
            for(int i=0;i<3;i++)
            {
                Vector3 anchor=ChallengeLocations[i]+Vector3.up*2.6f;
                if(Vector3.Distance(player.transform.position,anchor)>40 || Physics.Linecast(Camera.main.transform.position,anchor,WorldMask))continue;
                Vector3 screen=Camera.main.WorldToScreenPoint(anchor);var rect=new Rect(screen.x-145,Screen.height-screen.y,290,28);
                if(screen.z<=0 || WorldLabelBlocked(rect))continue;
                GUI.Label(rect,eliteChallengeStates[i]==EliteChallengePhase.Available ? "ELITE CHALLENGE / E / FREE EPIC CHEST" :
                    eliteChallengeStates[i]==EliteChallengePhase.Completed ? "CHALLENGE CLEARED" : "ELITE REINFORCEMENTS ACTIVE");
            }
        }
    }
}
