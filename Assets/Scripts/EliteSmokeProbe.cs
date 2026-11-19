#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace Swarm
{
    public class EliteSmokeProbe : MonoBehaviour
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-elite-test")>=0)
                new GameObject("Elite probe").AddComponent<EliteSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=120;yield return null;
            var combat=CombatPlayground.Instance;combat.AutoSpawn=combat.AutoFire=false;combat.enabled=false;
            var motor=FindObjectOfType<MomentumMotor>();motor.enabled=false;
            var type=typeof(CombatPlayground);
            var field=type.GetField("enemies",Private);
            IList Enemies() => (IList)field.GetValue(combat);
            object Get(object enemy,string key) => enemy.GetType().GetField(key).GetValue(enemy);
            float Number(object enemy,string key) => (float)Get(enemy,key);
            void Set(object enemy,string key,object value) => enemy.GetType().GetField(key).SetValue(enemy,value);
            void Clock(float value) => type.GetField("elapsed",Private).SetValue(combat,value);
            void Health(float value) => type.GetProperty("Health").SetValue(combat,value);
            void ClearHurt() => type.GetField("hurtClock",Private).SetValue(combat,0f);
            bool passed=true;
            foreach(EnemyKind kind in new[]{EnemyKind.Chaser,EnemyKind.Runner,EnemyKind.Spitter,EnemyKind.Charger})
            {
                combat.Begin();Clock(120);motor.SetSpawn(new Vector3(0,1.1f,0));
                bool spawned=combat.SpawnEnemy(new Vector3(0,0,10),false,kind) && combat.SpawnEliteEnemy(new Vector3(3,0,10),kind);
                if(!spawned){passed=false;Debug.Log($"SWARM_ELITE_KIND: kind={kind} spawn=false");continue;}
                object normal=Enemies()[0],elite=Enemies()[1];
                float healthRatio=Number(elite,"maxHealth")/Number(normal,"maxHealth");
                float damageRatio=Number(elite,"damageScale")/Number(normal,"damageScale");
                bool identity=(bool)Get(elite,"eliteVariant") && !(bool)Get(elite,"elite") && (EnemyKind)Get(elite,"kind")==kind;
                bool visuals=((GameObject)Get(elite,"body")).transform.Find("Elite gold collar") && ((GameObject)Get(elite,"body")).name.StartsWith("ELITE ");
                bool stats=Mathf.Abs(healthRatio-1.5f)<.001f && Mathf.Abs(damageRatio-1.25f)<.001f;
                float normalHit=0,eliteHit=0;
                if(kind==EnemyKind.Chaser || kind==EnemyKind.Runner)
                {
                    var normalAgent=(NavMeshAgent)Get(normal,"agent");var eliteAgent=(NavMeshAgent)Get(elite,"agent");
                    normalAgent.Warp(new Vector3(0,0,.4f));Health(100);ClearHurt();
                    type.GetMethod("TickEnemies",Private).Invoke(combat,new object[]{0f});normalHit=100-combat.Health;
                    normalAgent.Warp(new Vector3(0,0,10));eliteAgent.Warp(new Vector3(0,0,.4f));Health(100);ClearHurt();
                    type.GetMethod("TickEnemies",Private).Invoke(combat,new object[]{0f});eliteHit=100-combat.Health;
                }
                else if(kind==EnemyKind.Spitter)
                {
                    Set(normal,"attackClock",0f);Set(elite,"attackClock",0f);
                    var tick=type.GetMethod("TickSpitter",Private);
                    tick.Invoke(combat,new object[]{normal,0f,true,motor.transform.position});
                    tick.Invoke(combat,new object[]{elite,0f,true,motor.transform.position});
                    var shots=(IList)type.GetField("enemyShots",Private).GetValue(combat);
                    if(shots.Count==2){normalHit=Number(shots[0],"damage");eliteHit=Number(shots[1],"damage");}
                }
                else
                {
                    var hit=type.GetMethod("ApplyEnemyKnockbackHit",Private);
                    Health(100);ClearHurt();hit.Invoke(combat,new object[]{normal,(ulong)0,18f,Vector3.zero});normalHit=100-combat.Health;
                    Health(100);ClearHurt();hit.Invoke(combat,new object[]{elite,(ulong)0,18f,Vector3.zero});eliteHit=100-combat.Health;
                }
                bool damage=normalHit>0 && Mathf.Abs(eliteHit/normalHit-1.25f)<.001f;
                Clock(630);
                var overtime=type.GetMethod("ApplyOvertimePressure",Private);overtime.Invoke(combat,new[]{normal});overtime.Invoke(combat,new[]{elite});
                bool retained=Mathf.Abs(Number(elite,"maxHealth")/Number(normal,"maxHealth")-1.5f)<.001f &&
                    Mathf.Abs(Number(elite,"damageScale")/Number(normal,"damageScale")-1.25f)<.001f;
                bool ok=identity && visuals && stats && damage && retained;passed &= ok;
                Debug.Log($"SWARM_ELITE_KIND: kind={kind} pass={ok} healthRatio={healthRatio:0.00} damageRatio={damageRatio:0.00} normalHit={normalHit:0.00} eliteHit={eliteHit:0.00} overtimeRetains={retained}");
            }
            combat.Begin();motor.SetSpawn(new Vector3(0,1.1f,0));
            bool boss=combat.SpawnEnemy(new Vector3(0,0,12),true);
            object sentinel=Enemies()[0];
            type.GetMethod("PromoteEnemyToElite",Private).Invoke(combat,new[]{sentinel});
            boss &= (bool)Get(sentinel,"elite") && !(bool)Get(sentinel,"eliteVariant") && combat.EliteVariantsSpawned==0;
            combat.Begin();motor.SetSpawn(new Vector3(0,1.1f,0));combat.AutoSpawn=true;
            var schedule=type.GetMethod("TickEliteWaves",Private);
            Clock(44);schedule.Invoke(combat,null);bool timing=combat.EliteVariantsSpawned==0;
            Clock(45);schedule.Invoke(combat,null);timing &= combat.EliteVariantsSpawned==1 && combat.EliteWavesSpawned==1 && Mathf.Abs(combat.NextEliteWaveAt-80)<.01f;
            Clock(79);schedule.Invoke(combat,null);timing &= combat.EliteVariantsSpawned==1;
            Clock(80);schedule.Invoke(combat,null);timing &= combat.EliteVariantsSpawned==2 && combat.EliteWavesSpawned==2;
            Clock(115);type.GetProperty("PartyPaused").SetValue(combat,true);schedule.Invoke(combat,null);
            bool pause=combat.EliteWavesSpawned==2;type.GetProperty("PartyPaused").SetValue(combat,false);
            int limit=(int)type.GetProperty("SwarmEnemyLimit",Private).GetValue(combat);
            for(int i=Enemies().Count;i<limit;i++)if(!combat.SpawnEnemy(new Vector3(0,0,15)))break;
            int before=combat.EliteVariantsSpawned;schedule.Invoke(combat,null);
            bool cap=Enemies().Count==limit && combat.EliteVariantsSpawned==before && !combat.SpawnEliteEnemy(new Vector3(0,0,15),EnemyKind.Chaser);
            bool challengeReserve=combat.SpawnEliteEnemy(new Vector3(0,0,15),EnemyKind.Charger,true) &&
                (bool)Get(Enemies()[Enemies().Count-1],"challengeElite");
            for(int i=Enemies().Count;i<limit+20;i++)
                if(!combat.SpawnEliteEnemy(new Vector3(0,0,15),EnemyKind.Chaser,true))break;
            challengeReserve &= Enemies().Count==limit+20 && !combat.SpawnEliteEnemy(new Vector3(0,0,15),EnemyKind.Chaser,true);
            combat.AutoSpawn=false;combat.Begin();
            bool reset=combat.EliteVariantsSpawned==0 && combat.EliteWavesSpawned==0 && Mathf.Abs(combat.NextEliteWaveAt-45)<.01f;
            passed &= boss && timing && pause && cap && challengeReserve && reset;
            combat.StopCombat();
            Debug.Log($"SWARM_ELITE_TEST: {(passed ? "PASS" : "FAIL")} bossSeparation={boss} schedule={timing} pause={pause} cap={cap} challengeReserve={challengeReserve} reset={reset}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
