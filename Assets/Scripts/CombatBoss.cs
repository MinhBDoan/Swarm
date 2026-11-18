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
        readonly Dictionary<Transform,Vector3> bossPropOffsets = new Dictionary<Transform,Vector3>();
        public string BossLocationName => CanyonLayout.Area(CanyonLayout.BossConsole).Split('/')[0].Trim();

        // Run-layout application calls this on host and clients. Gameplay already
        // reads the live console/spawn coordinates; relocate the authored props too.
        // Offsets are captured once against the authored yard site, never against
        // the previous randomized location, so retries/repeated snapshots cannot drift.
        public void RefreshBossLocation()
        {
            Vector3 authored = CanyonLayout.PumpCenter + new Vector3(6,0,-12);
            const string sign = "SENTINEL CONSOLE / E TO START";
            var props = new List<Transform>();
            var rings = new List<LineRenderer>();
            foreach (var prop in FindObjectsOfType<Transform>(true))
            {
                if (!prop.gameObject.scene.IsValid()) continue;
                if (prop.name == "Sentinel activation console" || prop.name == sign || prop.name == "Loop sign / "+sign)
                {
                    props.Add(prop);
                    if (!bossPropOffsets.ContainsKey(prop)) bossPropOffsets[prop] = prop.position-authored;
                }
                else if (prop.name == "Sentinel interaction ring")
                {
                    var ring = prop.GetComponent<LineRenderer>();
                    if (ring) rings.Add(ring);
                }
            }
            // Capture every original position before moving any parent transform.
            foreach (var prop in props) prop.position = CanyonLayout.BossConsole+bossPropOffsets[prop];
            foreach (var ring in rings)
            {
                ring.useWorldSpace = true;
                ring.loop = true;
                for (int i=0;i<ring.positionCount;i++)
                {
                    float angle = i*Mathf.PI*2/Mathf.Max(1,ring.positionCount);
                    ring.SetPosition(i,CanyonLayout.BossConsole+Vector3.up*.09f+
                        new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*BossInteractRange);
                }
            }
        }

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
                    Notice = $"Sentinel vote expired. Use the {BossLocationName.ToLowerInvariant()} console to try again.";
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
            HudLabel(new Rect(rect.x+16,35,276,26),"SENTINEL / "+BossLocationName,hudSmall,color);
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
                        detail = $"Console {Vector3.Distance(player.transform.position,CanyonLayout.BossConsole):0} m away\nLocation: {BossLocationName.ToLowerInvariant()}";
                    break;
            }
            HudLabel(new Rect(rect.x+16,72,276,70),detail,hudText);
        }
    }
}
