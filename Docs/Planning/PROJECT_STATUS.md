# Current project status

**Snapshot:** November 14, 2026 · source/file inspection and conversation history.

Initial document snapshot was source inspection only. **November 15 first parallel batch:** terrain, timer/overtime and group-menu pauses are integrated; a Windows development build and automated solo/host-client/six-instance checks passed. See [first batch playtest](FIRST_BATCH_PLAYTEST.md) for exact evidence and limits; player feel review remains pending.

## Environment and entry points

- Unity **2022.3.23f1**, per `ProjectSettings/ProjectVersion.txt`.
- Main scene: `Assets/Scenes/CanyonPlayground.unity`.
- Netcode for GameObjects **1.8.1** with Unity Transport; direct IPv4 / UDP 7777.
- Blender **5.3.0** is installed locally; it has been used to prepare the Charger.
- Local Git was initialized on November 15, 2026 on `main`, with `origin` pointing to `https://github.com/MinhBDoan/Swarm.git`. The owner approved public visibility, and the initial checkpoint was uploaded to GitHub on November 15, 2026. See [version control policy](VERSION_CONTROL.md) for exclusions and naming.

Open the scene, enter Play, click the Game view, and press **F** to start combat. Use **Swarm > Open Canyon Playground** if needed. **Swarm > Build Windows Playtest** builds to `Builds/Swarm/Swarm.exe`; the build method is `Swarm.Editor.MultiplayerSetup.BuildWindows`.

Controls: WASD move; mouse camera; Space jump/bounce or zip exit; Ctrl/C slide or zip drop; E interact/grab zip; Escape release cursor; R reset position; 1/2/3 choose upgrade; T reroll; X skip. Follow in-game prompts where interactions compete. R is a prototype aid.

For two local instances, use editor plus a standalone build: Host in one, Join `127.0.0.1` in the other. Join all players before starting combat. The host starts/stops/retries. Latest randomized encounter/shop pass: `Builds/Swarm-RandomEncountersShops/Swarm.exe`; see [random encounters and shops](RANDOM_ENCOUNTERS_SHOPS.md). Previous map/reward pass: `Builds/Swarm-CentralSlopesLoot/Swarm.exe`; see [central slopes and chests](CENTRAL_SLOPES_LOOT_PLAYTEST.md). Previous combined playtest: `Builds/Swarm-TerrainTimerPause/Swarm.exe`. The older SentinelMoves build is retained as a previous checkpoint.

## Implemented systems and evidence

| Area | Current state | Evidence / limitation |
|---|---|---|
| Movement | Momentum controller, slope slides, slide hops, air steering, ordinary-wall bounces, directional zip entry/exit | `MomentumMotor.cs`, `MiningZipline.cs`; player has accepted several feel passes. |
| World | Mining basin, drilling station, pumping yard, connecting routes, central flats, movement props and hazards | `CanyonLayout.cs`, editor setup scripts; greybox, not finished environment art. |
| Combat | Automatic weapons, XP pickup attraction, level-ups, damage/defeat/retry | `CombatPlayground.cs`; player accepted the basic movement/combat combination. |
| Builds | Four slots; gun plus four optional weapons; rank limits, passives, skips and rerolls | `CombatLoadout.cs`, `BoltUpgrade.cs`; balance is provisional. |
| Rarity | Common, Uncommon, Rare, Epic, Legendary; stats plus critical/shockwave mechanics | `CombatRarity.cs`; rarity rules need review, not an assumed finalized progression curve. |
| Economy/objectives | Shared wallet, seven fixed-price Rare/Epic crates, salvage interactions, guard caches, eight terminal slots across five rarity tiers | `CombatEconomy.cs`, `CombatDownload.cs`, `CombatJourney.cs`; random crate pricing is not implemented. |
| Downloads | Automatic zone start, extra occupants add 10% each up to +50%, 2-second empty grace then 2 percentage points/second decay; calls packs while occupied | `CombatDownload.cs`; rewards currently go to the party, not just finish-line occupants. |
| Enemies | Chaser, Runner, Spitter, Charger; navigation and nearby reachable spawning | `CombatPlayground.cs`, `CombatCharger.cs`; primitive runtime visuals. |
| Difficulty | Pack sizes, live caps, spawn pacing, health/damage tuning depend on time and party size | Inspect current formulas before balancing; old README health percentages are superseded. No loadout-based scaling is intended. |
| Boss | Console, discovery/readiness states, Sentinel charge+pound, sweep, shockwave, double pound | `CombatBoss.cs`, `CombatSentinel.cs`; final encounter feel still needs hands-on validation. |
| Co-op | Up to six; host-owned combat and wallet, shared XP, independent builds, replicated visuals, party upgrade pause | `MultiplayerSession.cs`, `CoopCombat.cs`; player previously confirmed a two-instance test and simultaneous upgrades. Six-human play is not established. |
| HUD | Persistent combat information and classic fallback | `CombatHud.cs`; player confirmed visibility, still wants visual polish. |
| Charger art | One-creature export, roughly 12k-triangle reduced source, preliminary 15-bone rig | Latest file below; player says v4 looks better. No production animation or Unity integration. |

## November 17 encounter/shop checkpoint

Randomized per-run chest, terminal, boss and shop locations are implemented on fixed terrain, with one host-chosen layout shared by all clients. There are eight terminals and three personal NPC shops. Shops use the confirmed Uncommon/Rare/Epic tiers and shared wallet; prices 15/30/75 are initial tuning. Each member gets personal offers and one purchase per shop, with no world pause. Group XP/chest/terminal rewards and ordinary Escape menus still pause the party. See the latest playtest guide for functional checks and limits.

## Charger checkpoint — paused

Latest reviewed experiment: [Charger_Skeleton_Test_v4.blend](../ArtReferences/ChargerSingleWork/Charger_Skeleton_Test_v4.blend).

- Original GLB: approximately 1.95 million triangles; embedded textures; no original rig/animations.
- Initial cleaned reduction: 11,999 triangles. Later rig experiments split vertices and add seam faces; recount before setting a final asset budget.
- v4 holds the head rigid at a 12-degree test bend. A check of 810 upper-back vertices measured zero displacement during that head test.
- Neck/shoulder joins still pinch. This is an experiment, not an animation-ready production mesh. Rigid-head checks do not validate all geometry, skin weights, or all poses.
- No final run, charge, hit, death animations, LODs, material optimization, or gameplay prefab integration.
- Earlier versions are preserved. The first three-creature export was unsuitable because the turnaround sheet became multiple creatures.

## Latest confirmed design and implementation

- Run structure: **three areas, each with its own 10-minute countdown**. The canyon is the only implemented area; its multiple POIs share one area clock. Waves increase over time. After expiry, overtime keeps raising enemy strength and wave numbers in an overwhelming escalation intended to eventually defeat delaying players. Implemented November 15 for the canyon: a 600-second active-simulation countdown and increasing overtime pack/strength pressure. Expiry and later escalation passed the runtime probe; tuning remains provisional.
- Shared money and shared XP are confirmed for now, matching the existing shared wallet and XP implementation. Chests and terminals are now confirmed to reward the whole party, matching the prototype. Each player selects independently. Personal shop purchases are now the explicit individual-upgrade exception, with no party pause.
- Synchronized shared-XP upgrades and whole-party terminal/chest rewards pause gameplay and the active area countdown until the group finishes its choices. Both the group upgrade pause and new area clock are implemented and their pause/resume behavior was checked. XP/chest/terminal upgrades are group events; shop upgrades are personal and non-pausing. Escape now explicitly opens/closes a menu that pauses the party and area clock. Overlapping menus/choices and paused disconnect cleanup passed host/client checks.
- Boss activation is confirmed to leave the area countdown and escalating waves running. Expiry during the fight starts overtime; activating after expiry does not cancel overtime. Implemented November 15: wave spawning continues during boss fights; the timer probe checks this path. Menu pauses are confirmed; post-defeat clock handling remains open. Bosses have no separate duration limit: fight until killed or the run ends.
- New chest direction: randomized tiers, fixed starting prices by tier, and escalating prices as the party buys more chests. Current code still has fixed placements and Rare/Epic prices (30/75); the new pricing/tier selection is not implemented. Tier odds, growth formula, affected tiers, placement randomization, and area/run reset scope remain open.
- The third area setting is open; cyberpunk city is the intended second setting.

## Current next milestone

Player confirms the movement/co-op/adventure vision but expects more tuning. Next priority is fleshing out the existing canyon, reviewing its possibly excessive size, and reducing empty/low-value spaces while preserving useful combat and movement room. A map audit should determine whether to add purposeful content or compress sections. This is a map milestone, not a commitment to complete every proposed gameplay system next.

The game should not be PC-intensive. Specific target hardware and budgets remain unchosen. Third area setting remains undecided. Charger work stays paused at v4. The November 15 first batch implements a central terrain pass, group menus, and canyon timer/overtime; the map milestone remains in progress pending player review.

## Known limitations and documentation conflicts

- No second playable map, map transition, or build carryover yet.
- No internet invite/matchmaking flow, mid-run join, host migration, or production server reconciliation. Movement is owner-authoritative; combat is host-authoritative.
- Replication uses prototype snapshots. Six local instances passed shared-combat checks, but remote-network bandwidth, representative CPU/GPU frame time, VFX readability, and lower-end hardware performance are not established.
- Map flow, accidental holes, navigation on new terrain, balance, and optional reward value need repeatable playtests.
- Older `PROGRESS.md` sections claim combat is solo-only; this is superseded by `CoopCombat.cs` and later player tests. Older wave/boss/terminal timings in `README.md` are historical, not authoritative tuning values.
- Local version control is initialized. The initial checkpoint is uploaded. Prepare isolated working copies before concurrent agent edits.

## Existing validation tools

Development probes exist for movement, networking/co-op, loadouts, economy, downloads, routes/layout, central pursuit, Charger, and boss behavior. See `Assets/Scripts/*SmokeProbe.cs` and their command-line argument parsing before running them. Some probes accelerate time or modify stats; do not treat their results as balance or performance evidence.

For each new change record: source revision/build identifier, scenario, party size, pass/fail, relevant logs/screenshots, and limitations. Use real gameplay for feel; use profiler measurements for performance.
