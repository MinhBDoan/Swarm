using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Swarm
{
    public partial class CombatPlayground
    {
        [Serializable] public class RunLayoutData
        {
            public int seed;
            public Vector3[] terminals,chests,shops,challenges;
            public Vector3 bossConsole,bossSpawn;
        }
        public RunLayoutData ActiveRunLayout { get; private set; }
        public int RunSeed => ActiveRunLayout!=null ? ActiveRunLayout.seed : 0;
        static Vector3[] authoredTerminals,authoredChests,authoredShops,authoredChallenges;
        static Vector3 authoredBossConsole,authoredBossSpawn;
        static int previousLayoutSeed;
        void EnsureAuthoredLayout()
        {
            if(authoredTerminals!=null)return;
            authoredTerminals=(Vector3[])CanyonLayout.DownloadCenters.Clone();
            authoredChests=(Vector3[])MoneyCrateFloors.Clone();
            authoredShops=(Vector3[])ShopLocations.Clone();
            authoredChallenges=(Vector3[])ChallengeLocations.Clone();
            authoredBossConsole=CanyonLayout.BossConsole;authoredBossSpawn=CanyonLayout.BossSpawn;
        }
        void RestoreRunLayout()
        {
            EnsureAuthoredLayout();
            Array.Copy(authoredTerminals,CanyonLayout.DownloadCenters,authoredTerminals.Length);
            Array.Copy(authoredChests,MoneyCrateFloors,authoredChests.Length);
            Array.Copy(authoredShops,ShopLocations,authoredShops.Length);
            Array.Copy(authoredChallenges,ChallengeLocations,3);
            CanyonLayout.BossConsole=authoredBossConsole;CanyonLayout.BossSpawn=authoredBossSpawn;
            ActiveRunLayout=null;
            RefreshDownloadLocations();RefreshBossLocation();
        }
        void ApplyRunLayout(RunLayoutData data)
        {
            if(data==null || data.seed==0 || data.terminals==null || data.terminals.Length!=CanyonLayout.DownloadCount ||
                data.chests==null || data.chests.Length!=MoneyCrateCount || data.shops==null || data.shops.Length!=3 || data.challenges==null || data.challenges.Length!=3)return;
            if(ActiveRunLayout!=null && ActiveRunLayout.seed==data.seed)return;
            EnsureAuthoredLayout();ActiveRunLayout=data;
            Array.Copy(data.terminals,CanyonLayout.DownloadCenters,data.terminals.Length);
            Array.Copy(data.chests,MoneyCrateFloors,data.chests.Length);
            Array.Copy(data.shops,ShopLocations,3);
            Array.Copy(data.challenges,ChallengeLocations,3);
            CanyonLayout.BossConsole=data.bossConsole;CanyonLayout.BossSpawn=data.bossSpawn;
            RefreshDownloadLocations();RefreshBossLocation();
            Physics.SyncTransforms();
        }
        static void Shuffle<T>(List<T> items,System.Random random)
        {
            for(int i=items.Count-1;i>0;i--){int j=random.Next(i+1);T value=items[i];items[i]=items[j];items[j]=value;}
        }
        bool GroundedLayoutPoint(Vector3 point,out Vector3 floor,float radius=4)
        {
            floor=point;
            if(!Physics.Raycast(new Vector3(point.x,50,point.z),Vector3.down,out var hit,100,WorldMask) || hit.normal.y<.92f)return false;
            if(!NavMesh.SamplePosition(hit.point,out var nav,1.2f,NavMesh.AllAreas) || Mathf.Abs(nav.position.y-hit.point.y)>.5f)return false;
            var path=new NavMeshPath();
            if(!NavMesh.SamplePosition(new Vector3(0,0,-15),out var entry,4,NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(entry.position,nav.position,NavMesh.AllAreas,path) || path.status!=NavMeshPathStatus.PathComplete)return false;
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*.25f;Vector3 offset=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;
                if(!Physics.Raycast(hit.point+offset+Vector3.up*8,Vector3.down,out var edge,16,WorldMask) ||
                    edge.normal.y<.8f || Mathf.Abs(edge.point.y-hit.point.y)>1.5f)return false;
            }
            floor=hit.point;return true;
        }
        bool LayoutSeparated(Vector3 point,List<Vector3> reserved,float distance=14)
        {
            foreach(var other in reserved)if(Vector3.ProjectOnPlane(point-other,Vector3.up).sqrMagnitude<distance*distance)return false;
            return true;
        }
        List<Vector3> LayoutPool()
        {
            var raw=new List<Vector3>();raw.AddRange(authoredTerminals);raw.AddRange(authoredChests);
            foreach(float z in new[]{130f,175,220,265,310,405,445,525,565,605})
            foreach(float x in new[]{-215f,-175,-135,-95})raw.Add(new Vector3(x,0,z));
            raw.AddRange(new[]{new Vector3(14,0,-10),new Vector3(-15,0,-8),new Vector3(14,0,18),
                new Vector3(-326,6,322),new Vector3(-270,6,386),new Vector3(-330,6,380),
                CanyonLayout.StationPoint(-40,0,0),CanyonLayout.StationPoint(40,0,0),CanyonLayout.StationPoint(0,8,-30)});
            var valid=new List<Vector3>();
            foreach(var point in raw)
            {
                if(!GroundedLayoutPoint(point,out var floor))continue;
                bool cache=false;foreach(var c in CanyonLayout.CachePositions)if(Vector3.ProjectOnPlane(floor-c,Vector3.up).magnitude<12)cache=true;
                if(!cache && LayoutSeparated(floor,valid,8))valid.Add(floor);
            }
            return valid;
        }
        Vector3 PickLayoutPoint(List<Vector3> pool,List<Vector3> reserved,System.Random random,Predicate<Vector3> preferred=null)
        {
            var choices=new List<Vector3>();foreach(var p in pool)if(LayoutSeparated(p,reserved) && (preferred==null || preferred(p)))choices.Add(p);
            if(choices.Count==0 && preferred!=null)return PickLayoutPoint(pool,reserved,random);
            if(choices.Count==0)throw new InvalidOperationException("Not enough reachable, separated run-layout locations.");
            Vector3 point=choices[random.Next(choices.Count)];reserved.Add(point);return point;
        }
        void GenerateRunLayout()
        {
            EnsureAuthoredLayout();int seed=UnityEngine.Random.Range(1,int.MaxValue);
            if(seed==previousLayoutSeed)seed=seed==int.MaxValue-1 ? 1 : seed+1;previousLayoutSeed=seed;
            var random=new System.Random(seed);var pool=LayoutPool();var reserved=new List<Vector3>();
            var sites=new List<Vector3[]>{new[]{new Vector3(-294,6,338),new Vector3(-294,6,372)},
                new[]{new Vector3(0,0,-20),new Vector3(0,0,5)},
                new[]{new Vector3(-160,-4,420),new Vector3(-160,-3.65f,450)}};
            Shuffle(sites,random);Vector3 console=authoredBossConsole,boss=authoredBossSpawn;bool selected=false;
            foreach(var site in sites)
            {
                if(!GroundedLayoutPoint(site[0],out var c,5) || !GroundedLayoutPoint(site[1],out var b,5))continue;
                console=c;boss=b;selected=true;break;
            }
            if(!selected)throw new InvalidOperationException("No valid boss arena is reachable.");
            reserved.Add(console);reserved.Add(boss);
            var layout=new RunLayoutData{seed=seed,bossConsole=console,bossSpawn=boss,
                terminals=new Vector3[CanyonLayout.DownloadCount],chests=new Vector3[MoneyCrateCount],shops=new Vector3[3],challenges=new Vector3[3]};
            // Preserve exploration across the POIs, with three guaranteed central slots.
            for(int i=0;i<layout.terminals.Length;i++)
            {
                Predicate<Vector3> region=null;
                if(i==0)region=p=>p.x> -35 && p.z<70 && p.y<3;
                else if(i==1)region=p=>p.z>295 && p.z<420 && p.x> -100;
                else if(i==2 || i==4)region=p=>p.z>615;
                else if(i==3)region=p=>p.z<80 && p.y>3;
                else if(i==5)region=p=>p.x< -65 && p.x> -240 && p.z>120 && p.z<290;
                else if(i==6)region=p=>p.x< -65 && p.x> -240 && p.z>=290 && p.z<435;
                else if(i==7)region=p=>p.x< -65 && p.x> -240 && p.z>=435 && p.z<610;
                layout.terminals[i]=PickLayoutPoint(pool,reserved,random,region);
            }
            for(int i=0;i<layout.chests.Length;i++)layout.chests[i]=PickLayoutPoint(pool,reserved,random);
            for(int i=0;i<3;i++)layout.shops[i]=PickLayoutPoint(pool,reserved,random);
            for(int i=0;i<3;i++)layout.challenges[i]=PickLayoutPoint(pool,reserved,random);
            ApplyRunLayout(layout);
            Debug.Log($"SWARM_RUN_LAYOUT: seed={seed} terminals={layout.terminals.Length} chests={layout.chests.Length} shops=3 boss={BossLocationName} candidates={pool.Count}");
        }
    }
}
