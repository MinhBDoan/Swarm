using System.Collections.Generic;
using UnityEngine;
namespace Swarm
{
    public partial class CombatPlayground
    {
        public enum UpgradeRarity { Common, Uncommon, Rare, Epic, Legendary }
        readonly List<UpgradeRarity> choiceRarities=new List<UpgradeRarity>();
        readonly float[] rarityWeaponPower={1,1,1,1};
        readonly List<int> bonusTiers=new List<int>();
        int currentBonusTier=-1;
        void QueueBonusUpgrade(int tier=-1)
        {
            while(bonusTiers.Count<bonusPending) bonusTiers.Add(-1);
            bonusTiers.Add(tier); bonusPending++;
            if(!ChoosingUpgrade && Health>0) OpenBonusUpgrade();
        }
        float criticalChance,rarityRecoveryAdjustment;
        int shockwaveRank,shockwaveCharge;
        public int CriticalHits { get; private set; }
        public int ShockwavesTriggered { get; private set; }
        public UpgradeRarity ChoiceRarity(int index) => index>=0 && index<choiceRarities.Count ? choiceRarities[index] : UpgradeRarity.Common;
        float RecoveryPerSecond => recoveryRank*.6f+rarityRecoveryAdjustment;
        public static float RarityStatBonus(UpgradeRarity rarity)
        {
            switch(rarity) { case UpgradeRarity.Uncommon:return .15f; case UpgradeRarity.Rare:return .25f; case UpgradeRarity.Epic:return .2f; case UpgradeRarity.Legendary:return .3f; default:return .1f; }
        }
        static UpgradeRarity RollRarity()
        {
            float roll=Random.value*100;
            return roll<50 ? UpgradeRarity.Common : roll<78 ? UpgradeRarity.Uncommon : roll<93 ? UpgradeRarity.Rare : roll<99 ? UpgradeRarity.Epic : UpgradeRarity.Legendary;
        }
        void ResetRarity()
        {
            bonusTiers.Clear(); currentBonusTier=-1;
            choiceRarities.Clear(); criticalChance=rarityRecoveryAdjustment=0; shockwaveRank=shockwaveCharge=CriticalHits=ShockwavesTriggered=0;
            for(int i=0;i<4;i++) rarityWeaponPower[i]=1;
        }
        float WeaponRarityPower(BoltUpgrade weapon)
        {
            int index=System.Array.IndexOf(ExtraWeapons,weapon); return index>=0 ? rarityWeaponPower[index] : 1;
        }
        void ApplyTieredUpgrade(BoltUpgrade upgrade,UpgradeRarity rarity)
        {
            float damage=Damage,interval=FireInterval,power=powerMultiplier,bonus=RarityStatBonus(rarity);
            int oldRank=WeaponRank(upgrade); ApplyUpgrade(upgrade);
            if(upgrade==BoltUpgrade.HeavyBolts) Damage=damage*(1+bonus);
            else if(upgrade==BoltUpgrade.RapidFire) FireInterval=Mathf.Max(.08f,interval/(1+bonus));
            else if(upgrade==BoltUpgrade.Power) { powerMultiplier=power*(1+bonus*.6f); Damage=damage*(1+bonus*.6f); }
            else if(upgrade==BoltUpgrade.Recovery)
            {
                float gain=rarity==UpgradeRarity.Common ? .3f : rarity==UpgradeRarity.Uncommon ? .45f : rarity==UpgradeRarity.Rare ? .6f : rarity==UpgradeRarity.Epic ? .75f : 1;
                rarityRecoveryAdjustment+=gain-.6f;
            }
            else if(IsExtraWeapon(upgrade))
            {
                if(WeaponRank(upgrade)>oldRank) rarityWeaponPower[System.Array.IndexOf(ExtraWeapons,upgrade)]*=1+bonus;
            }
            else Damage=damage*(1+bonus*.5f);
            if(rarity==UpgradeRarity.Epic) criticalChance=Mathf.Min(.5f,criticalChance==0 ? .2f : criticalChance+.05f);
            if(rarity==UpgradeRarity.Legendary) shockwaveRank++;
        }
        void WeaponHit(Enemy enemy,float damage)
        {
            if(!enemies.Contains(enemy)) return;
            Vector3 impact=enemy.body.transform.position+Vector3.up;
            if(criticalChance>0 && Random.value<criticalChance) { damage*=2; CriticalHits++; Spark(impact,.15f,.65f,.18f,sparkMaterial); }
            Hit(enemy,damage);
            if(shockwaveRank<=0 || ++shockwaveCharge<Mathf.Max(3,7-shockwaveRank)) return;
            shockwaveCharge=0; ShockwavesTriggered++; Ring(impact,3,blastMaterial);
            foreach(var target in enemies.ToArray())
            {
                Vector3 delta=target.body.transform.position+Vector3.up-impact;
                if(delta.sqrMagnitude<=9 && !Physics.Raycast(impact,delta.normalized,delta.magnitude,WorldMask)) Hit(target,damage*.5f);
            }
        }
        public static Color RarityColor(UpgradeRarity rarity)
        {
            switch(rarity) { case UpgradeRarity.Uncommon:return new Color(.4f,1,.5f); case UpgradeRarity.Rare:return new Color(.35f,.65f,1); case UpgradeRarity.Epic:return new Color(.8f,.45f,1); case UpgradeRarity.Legendary:return new Color(1,.72f,.2f); default:return new Color(.8f,.85f,.9f); }
        }
        string TieredDetail(BoltUpgrade upgrade,UpgradeRarity rarity)
        {
            float bonus=RarityStatBonus(rarity)*100;
            string text;
            if(upgrade==BoltUpgrade.HeavyBolts) text=$"Gun damage +{bonus:0}%.";
            else if(upgrade==BoltUpgrade.RapidFire) text=$"Gun firing rate +{bonus:0}%.";
            else if(upgrade==BoltUpgrade.Power) text=$"All weapon damage +{bonus*.6f:0.#}%. Uses no slot.";
            else if(upgrade==BoltUpgrade.Recovery)
            {
                float gain=rarity==UpgradeRarity.Common ? .3f : rarity==UpgradeRarity.Uncommon ? .45f : rarity==UpgradeRarity.Rare ? .6f : rarity==UpgradeRarity.Epic ? .75f : 1;
                text=$"Regeneration +{gain:0.00} HP/s. Rank {recoveryRank+1}/5. No slot.";
            }
            else if(IsExtraWeapon(upgrade)) text=$"{(WeaponRank(upgrade)==0 ? "Equip weapon" : "Increase weapon level")}. {WeaponName(upgrade)} damage +{bonus:0}% in addition to its level stats.";
            else if(upgrade==BoltUpgrade.MoreBolts) text=$"Fire {ProjectileCount+1} bolts per volley. Gun damage +{bonus*.5f:0.#}%.";
            else if(upgrade==BoltUpgrade.Ricochet) text=$"Ricochet level {RicochetRank+1}/3: chain to extra targets. Gun damage +{bonus*.5f:0.#}%.";
            else if(upgrade==BoltUpgrade.Piercing) text=$"Pierce level {PierceRank+1}/3: pass through extra targets. Gun damage +{bonus*.5f:0.#}%.";
            else text=$"Blast level {BlastRank+1}/3: hits splash for 50% damage. Gun damage +{bonus*.5f:0.#}%.";
            if(rarity==UpgradeRarity.Epic) text+=$"\nPERK (all weapons): +{(criticalChance==0 ? 20 : Mathf.Min(5,(.5f-criticalChance)*100)):0}% crit chance (50% cap). Critical direct hits deal 2x damage.";
            if(rarity==UpgradeRarity.Legendary) text+=$"\nPERK (all weapons): every {Mathf.Max(3,6-shockwaveRank)} hits, a 3m shockwave for 50% hit damage. No self-chaining.";
            return text;
        }
    }
}
