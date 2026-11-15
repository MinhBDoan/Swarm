#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
namespace Swarm
{
    public class LoadoutSmokeProbe : MonoBehaviour
    {
        static readonly BindingFlags Flags = BindingFlags.Instance|BindingFlags.NonPublic;
        CombatPlayground combat;
        MomentumMotor motor;
        void Invoke(string name, params object[] args) => typeof(CombatPlayground).GetMethod(name,Flags).Invoke(combat,args);
        void Property(string name, object value) => typeof(CombatPlayground).GetProperty(name).SetValue(combat,value);
        void Setup()
        {
            combat.AutoSpawn = combat.AutoFire = false; combat.Begin();
            motor.SetSpawn(new Vector3(0,1.1f,-15)); motor.enabled = false;
        }
        void Freeze()
        {
            foreach (var agent in FindObjectsOfType<NavMeshAgent>()) { agent.speed = 0; agent.acceleration = 0; }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-swarm-loadout-test") >= 0)
                new GameObject("Loadout Smoke Probe").AddComponent<LoadoutSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            yield return null;
            combat = CombatPlayground.Instance; motor = FindObjectOfType<MomentumMotor>();
            Setup();
            Invoke("ApplyUpgrade",BoltUpgrade.Boomerang); Invoke("ApplyUpgrade",BoltUpgrade.RocketLauncher); Invoke("ApplyUpgrade",BoltUpgrade.PiercingBow);
            Invoke("ApplyUpgrade",BoltUpgrade.PulseField);
            bool slots = combat.EquippedWeaponCount == 4 && combat.WeaponRank(BoltUpgrade.PulseField) == 0;
            for (int i=0;i<8;i++) Invoke("ApplyUpgrade",BoltUpgrade.Boomerang);
            bool cap = combat.WeaponRank(BoltUpgrade.Boomerang) == 5;
            Invoke("OpenBonusUpgrade");
            bool offers = true;
            for (int i=0;i<20;i++)
            {
                Invoke("RollUpgradeChoices");
                offers &= combat.UpgradeChoices.Count == 3 && new HashSet<BoltUpgrade>(combat.UpgradeChoices).Count == 3 &&
                    !new List<BoltUpgrade>(combat.UpgradeChoices).Contains(BoltUpgrade.PulseField) &&
                    !new List<BoltUpgrade>(combat.UpgradeChoices).Contains(BoltUpgrade.Boomerang);
            }
            for (int i=0;i<4;i++) combat.RerollUpgrades();
            bool rerolls = combat.RerollsRemaining == 0 && combat.ChoosingUpgrade;
            Property("Health",50f); int level = combat.Level;
            combat.SkipUpgrade();
            bool skip = !combat.ChoosingUpgrade && combat.UpgradeCount == 0 && combat.Health == 50 && combat.Level == level;
            // Skipping a queued XP reward must still deliver a waiting cache reward.
            typeof(CombatPlayground).GetField("xp",Flags).SetValue(combat,20);
            typeof(CombatPlayground).GetField("bonusPending",Flags).SetValue(combat,1);
            Invoke("OpenUpgrade"); combat.SkipUpgrade(); combat.SkipUpgrade();
            bool queue = combat.ChoosingUpgrade && combat.BonusUpgrade;
            combat.SkipUpgrade(); queue &= !combat.ChoosingUpgrade;
            Setup();
            bool reset = combat.EquippedWeaponCount == 1 && combat.RerollsRemaining == 3;
            bool[] weapons = new bool[4];
            var kinds = new[] { BoltUpgrade.Boomerang,BoltUpgrade.PulseField,BoltUpgrade.RocketLauncher,BoltUpgrade.PiercingBow };
            for (int i=0;i<4;i++)
            {
                Setup(); yield return null;
                Invoke("ApplyUpgrade",kinds[i]);
                if (i == 0)
                {
                    combat.SpawnEnemy(new Vector3(0,0,-9));
                    // Survive the outbound hit so the return leg can hit again.
                    var list = (IList)typeof(CombatPlayground).GetField("enemies",Flags).GetValue(combat);
                    var enemy = list[0]; enemy.GetType().GetField("health").SetValue(enemy,200f);
                }
                else if (i == 1) combat.SpawnEnemy(new Vector3(0,0,-12.5f));
                else
                {
                    combat.SpawnEnemy(new Vector3(0,0,-9));
                    combat.SpawnEnemy(i == 2 ? new Vector3(1,0,-9) : new Vector3(0,0,-7));
                }
                Freeze();
                Invoke("TickLoadout",.02f);
                float limit = Time.realtimeSinceStartup+1.5f;
                while (Time.realtimeSinceStartup < limit) yield return null;
                if (i == 1) Invoke("TickLoadout",1.5f);
                weapons[i] = i == 0 ? combat.DirectHits >= 2 : i == 1 ? combat.Kills >= 1 : i == 2 ? combat.ExplosionHits >= 1 : combat.PierceHits >= 1 && combat.Kills == 2;
                Debug.Log($"SWARM_LOADOUT_TRACE: {kinds[i]} pass={weapons[i]} kills={combat.Kills} hits={combat.DirectHits} splash={combat.ExplosionHits} pierce={combat.PierceHits}");
            }
            bool passed = slots && cap && offers && rerolls && skip && queue && reset && Array.TrueForAll(weapons,x=>x);
            Debug.Log($"SWARM_LOADOUT_TEST: {(passed ? "PASS" : "FAIL")} slots={slots} cap={cap} offers={offers} rerolls={rerolls} skip={skip} queue={queue} reset={reset}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
