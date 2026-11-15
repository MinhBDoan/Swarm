using System.Collections.Generic;
using UnityEngine;
namespace Swarm
{
    public partial class CombatPlayground
    {
        public const int WeaponSlots = 4;
        readonly Dictionary<BoltUpgrade,int> weapons = new Dictionary<BoltUpgrade,int>();
        readonly Dictionary<BoltUpgrade,float> weaponClocks = new Dictionary<BoltUpgrade,float>();
        readonly Dictionary<BoltUpgrade,Material> weaponMaterials = new Dictionary<BoltUpgrade,Material>();
        public int RerollsRemaining { get; private set; }
        public int EquippedWeaponCount => 1+weapons.Count;
        public int WeaponRank(BoltUpgrade weapon) => weapons.TryGetValue(weapon,out int rank) ? rank : 0;
        float powerMultiplier = 1;
        int recoveryRank;
        static readonly BoltUpgrade[] ExtraWeapons = { BoltUpgrade.Boomerang, BoltUpgrade.PulseField, BoltUpgrade.RocketLauncher, BoltUpgrade.PiercingBow };
        string LoadoutStatus
        {
            get
            {
                string text = $"Weapons {EquippedWeaponCount}/4: Auto gun";
                int index = 0;
                foreach (var pair in weapons) { text += (index++ == 0 ? "\n" : " / ")+ $"{WeaponName(pair.Key)} {pair.Value}"; }
                return text+$"\nPower x{powerMultiplier:0.00} / Recovery {recoveryRank}/5 / Rerolls {RerollsRemaining}";
            }
        }
        static string WeaponName(BoltUpgrade weapon)
        {
            switch (weapon)
            {
                case BoltUpgrade.Boomerang: return "Boomerang";
                case BoltUpgrade.PulseField: return "Pulse field";
                case BoltUpgrade.RocketLauncher: return "Rockets";
                default: return "Piercing bow";
            }
        }
        void ResetLoadout()
        {
            ResetRarity();
            weapons.Clear(); weaponClocks.Clear(); RerollsRemaining = 3; powerMultiplier = 1; recoveryRank = 0;
            if (weaponMaterials.Count == 0)
            {
                Color[] colors = { Color.cyan, new Color(.2f,1,.4f), new Color(1,.35f,.1f), new Color(.8f,.5f,1) };
                for (int i=0;i<ExtraWeapons.Length;i++)
                {
                    var material=MakeMaterial(colors[i]); material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",colors[i]*.65f);
                    weaponMaterials.Add(ExtraWeapons[i],material);
                }
            }
        }
        void AddLoadoutOffers(List<BoltUpgrade> pool)
        {
            foreach (var weapon in ExtraWeapons)
                if (WeaponRank(weapon) < 5 && (WeaponRank(weapon) > 0 || EquippedWeaponCount < WeaponSlots)) pool.Add(weapon);
            pool.Add(BoltUpgrade.Power);
            if (recoveryRank < 5) pool.Add(BoltUpgrade.Recovery);
        }
        bool ApplyLoadoutUpgrade(BoltUpgrade upgrade)
        {
            if (upgrade == BoltUpgrade.Power) { powerMultiplier *= 1.15f; Damage *= 1.15f; return true; }
            if (upgrade == BoltUpgrade.Recovery) { recoveryRank = Mathf.Min(5,recoveryRank+1); return true; }
            if (System.Array.IndexOf(ExtraWeapons,upgrade) < 0) return false;
            if (WeaponRank(upgrade) == 0 && EquippedWeaponCount >= WeaponSlots) return true;
            weapons[upgrade] = Mathf.Min(5,WeaponRank(upgrade)+1);
            if (!weaponClocks.ContainsKey(upgrade)) weaponClocks.Add(upgrade,0);
            return true;
        }
        public void SkipUpgrade()
        {
            if (IsCoopClient) { SendCombatCommand("skip"); return; }
            if (!ChoosingUpgrade || Health <= 0 || Won) return;
            FinishUpgrade();
        }
        public void RerollUpgrades()
        {
            if (IsCoopClient) { SendCombatCommand("reroll"); return; }
            if (!ChoosingUpgrade || Health <= 0 || Won || RerollsRemaining <= 0) return;
            RerollsRemaining--; RollUpgradeChoices();
        }
        string LoadoutDescription(BoltUpgrade upgrade)
        {
            if (upgrade == BoltUpgrade.Power) return "PASSIVE: Power — all weapon damage +15%\nUses no weapon slot.";
            if (upgrade == BoltUpgrade.Recovery) return $"PASSIVE: Recovery {recoveryRank+1}/5 — regenerate {(.6f*(recoveryRank+1)):0.0} HP/s\nUses no weapon slot.";
            if (System.Array.IndexOf(ExtraWeapons,upgrade) < 0) return null;
            int rank = WeaponRank(upgrade);
            string prefix = rank == 0 ? $"NEW WEAPON ({EquippedWeaponCount+1}/4 slots)" : $"WEAPON UPGRADE {rank+1}/5";
            string detail = upgrade == BoltUpgrade.Boomerang ? "Spins out and returns toward you; hits once each way.\nRanks increase damage and throw rate." :
                upgrade == BoltUpgrade.PulseField ? "Pulses damage in a circle around you.\nRanks increase radius and damage." :
                upgrade == BoltUpgrade.RocketLauncher ? "Slow rockets burst on impact; splash respects cover.\nRanks increase damage and blast radius." :
                "Strong single arrows pierce multiple enemies.\nRanks increase damage and piercing.";
            return $"{prefix}: {WeaponName(upgrade)}\n{detail}";
        }
        Enemy VisibleTarget(Vector3 origin, float range)
        {
            Enemy target = null; float best = range*range;
            foreach (var enemy in enemies)
            {
                Vector3 delta = enemy.body.transform.position+Vector3.up-origin;
                if (delta.sqrMagnitude >= best || Physics.Raycast(origin,delta.normalized,delta.magnitude,WorldMask)) continue;
                target = enemy; best = delta.sqrMagnitude;
            }
            return target;
        }
        void TickLoadout(float dt)
        {
            foreach (var weapon in ExtraWeapons)
            {
                int rank = WeaponRank(weapon);
                if (rank == 0) continue;
                weaponClocks[weapon] -= dt;
                if (weapon==BoltUpgrade.PulseField) UpdatePulseIndicator(rank,weaponClocks[weapon]);
                if (weaponClocks[weapon] > 0) continue;
                Vector3 origin = player.transform.position+Vector3.up*.3f;
                if (weapon == BoltUpgrade.PulseField)
                {
                    float radius = 3.5f+(rank-1)*.5f;
                    Ring(origin,radius,weaponMaterials[weapon]);
                    foreach (var enemy in enemies.ToArray())
                    {
                        Vector3 delta = enemy.body.transform.position+Vector3.up-origin;
                        if (delta.sqrMagnitude <= radius*radius && !Physics.Raycast(origin,delta.normalized,delta.magnitude,WorldMask))
                            WeaponHit(enemy,(12+rank*6)*powerMultiplier*WeaponRarityPower(weapon));
                    }
                    weaponClocks[weapon] = 1.4f;
                    continue;
                }
                var target = VisibleTarget(origin,weapon == BoltUpgrade.Boomerang ? 18 : 30);
                if (target == null) continue;
                Vector3 direction = (target.body.transform.position+Vector3.up-origin).normalized;
                var body = CreateWeaponProjectile(weapon,origin,direction);
                bool rocket = weapon == BoltUpgrade.RocketLauncher, boomerang = weapon == BoltUpgrade.Boomerang;
                bullets.Add(new Bullet { body = body, velocity = direction*(rocket ? 20 : boomerang ? 24 : 55),
                    owner = player, life = boomerang ? 4 : 2, damage = (rocket ? 40+rank*10 : boomerang ? 16+rank*7 : 40+rank*15)*powerMultiplier*WeaponRarityPower(weapon),
                    rocket = rocket, boomerang = boomerang, arrow = weapon==BoltUpgrade.PiercingBow, blast = rocket ? rank : 0, pierces = !rocket && !boomerang ? 2+rank : 0 });
                ShotsFired++;
                weaponClocks[weapon] = rocket ? 2.8f : boomerang ? 2.4f-rank*.15f : 1.8f;
            }
        }
        void BeginReturn(Bullet bullet,bool wall=false)
        {
            bullet.returning = true; bullet.hitEnemies.Clear();
            var properties=new MaterialPropertyBlock(); properties.SetColor("_Color",new Color(.45f,.65f,1));
            bullet.body.GetComponent<Renderer>().SetPropertyBlock(properties);
            var trail=bullet.body.GetComponent<TrailRenderer>(); if (trail) trail.SetPropertyBlock(properties);
            Vector3 home=(bullet.owner ? bullet.owner.transform.position : player.transform.position)+Vector3.up*.3f;
            bullet.returnStart=bullet.body.transform.position;
            bullet.returnForward=wall ? (home-bullet.returnStart).normalized : bullet.velocity.normalized;
            bullet.returnAge=0; bullet.returnDuration=Mathf.Clamp(Vector3.Distance(home,bullet.returnStart)/32+.12f,.18f,1.2f);
        }
    }
}
