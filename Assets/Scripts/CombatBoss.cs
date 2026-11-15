using System.Collections.Generic;
using UnityEngine;

namespace Swarm
{
    public partial class CombatPlayground
    {
        enum BossPhase { Hidden, Discovered, Voting, Preparing, Fighting }
        BossPhase bossPhase;
        readonly HashSet<ulong> bossVotes = new HashSet<ulong>();
        float bossClock;
        int bossReady, bossEligible;
        bool localBossReady;
        Enemy consoleSentinel;
        public const float BossInteractRange = 5;

        void ResetBoss()
        {
            bossPhase = BossPhase.Hidden;
            bossVotes.Clear();
            bossClock = 0;
            bossReady = bossEligible = 0;
            localBossReady = false;
            consoleSentinel = null;
        }

        bool NearBossConsole(MomentumMotor motor, float range)
        {
            return motor && Vector3.Distance(motor.transform.position, CanyonLayout.BossConsole) <= range;
        }

        public bool BossConsoleInReach => player &&
            Mathf.Abs(player.transform.position.y-CanyonLayout.BossConsole.y)<3.2f &&
            Vector3.ProjectOnPlane(player.transform.position-CanyonLayout.BossConsole,Vector3.up).sqrMagnitude<=BossInteractRange*BossInteractRange;

        public bool TryBossConsole()
        {
            if (!Running || Won || Health <= 0 || ChoosingUpgrade || PartyPaused ||
                !BossConsoleInReach || player.IsZiplining) return false;
            if (bossPhase >= BossPhase.Preparing) return true;
            if (!NetworkActive)
            {
                bossPhase = BossPhase.Preparing;
                bossClock = 5;
                Notice = "Sentinel summoned. Get ready.";
                CaptureMouse();
                return true;
            }
            if (IsCoopClient)
            {
                SendCombatCommand("boss-console");
                return true;
            }

            if (bossPhase != BossPhase.Voting)
            {
                bossPhase = BossPhase.Voting;
                bossVotes.Clear();
                bossClock = 45;
            }
            return ReadyForBoss();
        }

        bool ReadyForBoss()
        {
            if (!Running || Won || Health <= 0 || ChoosingUpgrade || PartyPaused || bossPhase != BossPhase.Voting) return false;
            if (IsCoopClient)
            {
                SendCombatCommand("boss-ready");
                return true;
            }
            bossVotes.Add(contextId);
            return true;
        }

        void TickBoss(float dt)
        {
            if (bossPhase == BossPhase.Hidden)
            {
                var scout = CoopAuthority ? NearestLiving(CanyonLayout.BossConsole) : player;
                if (NearBossConsole(scout, 22)) bossPhase = BossPhase.Discovered;
            }
            if (bossPhase == BossPhase.Voting)
            {
                bossReady = bossEligible = 0;
                if (CoopAuthority)
                {
                    SaveContext();
                    foreach (var member in party.Values)
                    {
                        if (member.health <= 0) continue;
                        bossEligible++;
                        if (bossVotes.Contains(member.id)) bossReady++;
                    }
                }
                else
                {
                    bossEligible = 1;
                    bossReady = bossVotes.Count;
                }

                if (bossEligible > 0 && bossReady == bossEligible)
                {
                    bossPhase = BossPhase.Preparing;
                    bossClock = 5;
                    return;
                }
                bossClock -= dt;
                if (bossClock <= 0)
                {
                    bossPhase = BossPhase.Discovered;
                    bossVotes.Clear();
                    Notice = "Sentinel vote expired. Use the pumping yard console to try again.";
                }
            }
            if (bossPhase != BossPhase.Preparing) return;

            bossClock -= dt;
            if (bossClock > 0) return;
            ulong original = contextId;
            if (CoopAuthority) SwitchToNearest(CanyonLayout.BossSpawn);
            if (SpawnEnemy(CanyonLayout.BossSpawn, true))
            {
                consoleSentinel = enemies[enemies.Count-1];
                bossPhase = BossPhase.Fighting;
                Resting = false;
                Notice = "Sentinel active. Watch its charge lane, sweep and ground attacks.";
            }
            else
            {
                bossPhase = BossPhase.Discovered;
                bossVotes.Clear();
                Notice = "Sentinel could not reach the party. Return to the console and try again.";
            }
            if (CoopAuthority) SwitchContext(original);
        }

        bool ShowBossObjective => bossPhase >= BossPhase.Voting || NearBossConsole(player, 12) ||
            bossPhase == BossPhase.Discovered && !ShowDownloadObjective && !ShowSupplyObjective;

        void DrawBossObjective(float width)
        {
            var color = new Color(1,.62f,.24f);
            var rect = new Rect(width-330,22,308,143);
            HudPanelBox(rect,color);
            HudLabel(new Rect(rect.x+16,35,276,26),"SENTINEL / PUMPING YARD",hudSmall,color);
            string detail;
            switch (bossPhase)
            {
                case BossPhase.Voting:
                    bool ready = IsCoopClient ? localBossReady : bossVotes.Contains(contextId);
                    detail = $"{bossReady}/{bossEligible} ready / {Mathf.CeilToInt(bossClock)}s\n" +
                        (ready ? "Waiting for teammates" : "Y: Ready for the fight");
                    break;
                case BossPhase.Preparing:
                    detail = $"Sentinel arrives in {Mathf.CeilToInt(Mathf.Max(0,bossClock))}s";
                    break;
                case BossPhase.Fighting:
                    detail = SentinelAttackText+"\nDefeat it to clear the canyon";
                    break;
                default:
                    detail = BossConsoleInReach ? (NetworkActive ? "E: Propose Sentinel fight" : "E: Summon Sentinel") : "Enter the orange console ring";
                    if (player && !NearBossConsole(player,22))
                        detail = $"Console {Vector3.Distance(player.transform.position,CanyonLayout.BossConsole):0} m away\nUse a signed route to the yard";
                    break;
            }
            HudLabel(new Rect(rect.x+16,72,276,70),detail,hudText);
        }
    }
}
