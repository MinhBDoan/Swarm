using UnityEngine;

namespace Swarm
{
    public partial class CombatPlayground
    {
        public const float FirstEliteWaveSeconds=45, EliteWaveIntervalSeconds=35;
        public const float EliteHealthMultiplier=1.5f, EliteDamageMultiplier=1.25f;
        float nextEliteWaveAt=FirstEliteWaveSeconds;
        Material eliteCollarMaterial;
        public int EliteVariantsSpawned { get; private set; }
        public int EliteWavesSpawned { get; private set; }
        public float NextEliteWaveAt => nextEliteWaveAt;

        void ResetElites()
        {
            nextEliteWaveAt=FirstEliteWaveSeconds;
            EliteVariantsSpawned=EliteWavesSpawned=0;
        }

        // An elite is an ordinary archetype with stronger stats, never the Sentinel.
        public bool SpawnEliteEnemy(Vector3 position,EnemyKind kind,bool challenge=false)
        {
            if(IsCoopClient || enemies.Count>=SwarmEnemyLimit+(challenge ? 20 : 0) || !SpawnEnemy(position,false,kind))return false;
            var enemy=enemies[enemies.Count-1];
            enemy.challengeElite=challenge;
            PromoteEnemyToElite(enemy);
            return true;
        }

        void PromoteEnemyToElite(Enemy enemy)
        {
            if(enemy==null || enemy.elite || enemy.eliteVariant)return;
            enemy.eliteVariant=true;
            enemy.health*=EliteHealthMultiplier;enemy.maxHealth*=EliteHealthMultiplier;
            enemy.damageScale*=EliteDamageMultiplier;
            enemy.body.name="ELITE "+enemy.kind.ToString().ToUpperInvariant();
            if(!eliteCollarMaterial)
            {
                eliteCollarMaterial=MakeMaterial(new Color(1,.78f,.12f));
                eliteCollarMaterial.EnableKeyword("_EMISSION");
                eliteCollarMaterial.SetColor("_EmissionColor",new Color(1,.55f,.04f)*.6f);
            }
            var collar=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            collar.name="Elite gold collar";collar.layer=2;collar.transform.SetParent(enemy.body.transform,false);
            bool runner=enemy.kind==EnemyKind.Runner,charger=enemy.kind==EnemyKind.Charger;
            float radius=runner ? .78f : charger ? 1.5f : enemy.kind==EnemyKind.Spitter ? 1.35f : 1.18f;
            collar.transform.localPosition=Vector3.up*(runner ? .85f : charger ? 1.2f : 1.1f);
            collar.transform.localScale=new Vector3(radius,.07f,radius);
            Destroy(collar.GetComponent<Collider>());
            collar.GetComponent<Renderer>().sharedMaterial=eliteCollarMaterial;
            EliteVariantsSpawned++;
        }

        void TickEliteWaves()
        {
            if(!AutoSpawn || !Running || Won || PartyPaused || ChoosingUpgrade || IsCoopClient || elapsed<nextEliteWaveAt)return;
            if(enemies.Count>=SwarmEnemyLimit)return;
            ulong original=contextId;
            if(CoopAuthority)SwitchToSwarmTarget();
            int count=Mathf.Min(3,1+ExtraPartyMembers/2);
            count=Mathf.Min(count,SwarmEnemyLimit-enemies.Count);
            int spawned=0;
            for(int i=0;i<count;i++)
            {
                if(!SpawnAroundPlayer())continue;
                PromoteEnemyToElite(enemies[enemies.Count-1]);
                waveSpawned++;spawned++;
            }
            if(CoopAuthority)SwitchContext(original);
            if(spawned>0)
            {
                EliteWavesSpawned++;
                nextEliteWaveAt=elapsed+EliteWaveIntervalSeconds;
                Notice="Elite pack approaching — gold collars mark stronger enemies.";
            }
            else nextEliteWaveAt=elapsed+1;
        }
    }
}
