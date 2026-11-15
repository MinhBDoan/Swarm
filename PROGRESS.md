# Checkpoint — October 23, 2026

## Agreed direction
- PC game in Unity, Blender for eventual asset editing, Codex for development.
- Adventure survivor for 1–6 players, absurd builds, shared XP planned.
- Canyon first, cyberpunk city next; waves and bosses unlock maps.
- Builds carry between maps within a run.
- Complete movement toolkit at spawn. Momentum and terrain enable player-discovered tricks.
- Walls remove incoming momentum while preserving tangential and falling motion.
- Flat slides preserve speed, slopes influence speed, slide jumps carry horizontal momentum.
- Prototype with primitives before importing production art.

## Implemented
- Unity 2022.3.23f1 project, canyon scene, third-person camera, movement and speed HUD.
- Run/jump/slide/air steering/wall bounce on ordinary collision geometry.
- Offline practice plus direct-IP multiplayer host/join, six-player limit, colored avatars.
- Solo combat: chasers with runtime navigation, auto-targeted bolts, health, XP drops,
  upgrade choices, defeat/retry, and optional return to movement practice.
- F starts combat; opening pack of three appears ahead. Enemy count, shot count,
  weapon status, and pause status make the combat state visible.

## Validation
- Windows build succeeded.
- Automated six-instance local networking test: six players and remote motion observed
  on all instances; seventh rejected; offline practice restored after sessions ended.
- Latest combat integration test passed: projectile kills, XP collection, upgrade,
  damage, defeat, reset, cleanup, opening pack, and opening automatic fire.
- Automated checks do not replace hands-on movement/combat feel testing.

## Resume here
Connected canyon loop greybox (November 2, 2026): third POI is PUMPING YARD,
center(-300,6,350), 92x104m floor. Relay deck with walking ascent and broad return
slide, two equipment bounce faces, a raised deck-transfer zip and three open mouths.
CanyonLoopSetup.Install runs after CanyonJourneySetup from DrillBowlSetup, so full
scene rebuilds retain it. Idempotent Western Loop root replaces the basin west
boundary with an opening at z10..30. Existing upper basin routes preserved.
Outer western ridge connects station west apron to yard north; return gully connects
yard south to basin west. One narrower central cut connects winding narrows/shelter
at(-40,1,390) to yard east, with a broad opening in existing west canyon faces.
Gold stripes mark outer routes, teal stripes the central shortcut. Rock boundaries,
bends and height changes separate areas. All connections support ordinary walking
in either direction; no gates or movement upgrades required. Ridge has an optional
momentum zip. Floor meshes are WesternridgeFloor, ReturngullyFloor, CentralcutFloor.
CanyonLayout contains route arrays, PumpCenter and new area/hint names. Runtime
navigation bounds expanded to include western map. Sentinel fixed-position spawns
now require proximity in X as well as Z; western routes use reachable local spawns.
This is a traversal greybox: no new yard objective, dedicated encounter, boss
activation or extra loot economy added yet.
Validation: Windows build success. LoopSmokeProbe passed all three connections
both directions, yard north/south, deck ascent/return, basin/shelter/station mouths,
complete enemy navigation and actual enemy spawn in yard. Outer legs take roughly
33-35 seconds at ordinary running speed, middle cut20 seconds, excluding yard travel.
Both new zips passed actual grab/ride/completion/ground recovery. First tests caught
reverse-direction ramp obstructions (removed), mast blocking deck (moved sideways),
and zip catching deck edge (raised upper endpoint). Final actual movement checks
pass. Existing layout, original 46.82s journey/bridge/recovery, and route features
(quarter pipe, bank, ledges, zip, enemy access) regression probes also passed.
Inspected screenshots: Docs/Playtest/loop-overview.png, pumping-yard.png,
central-shortcut.png. Latest build: Builds/Swarm-CanyonLoop/Swarm.exe.
Backup: Backups/Swarm-canyon-loop-2026-11-02.zip.
To test: stop Play, reopen CanyonPlayground and Play; take the signed west exit
from the basin (ground level z20), or the shelter shortcut, or station west apron.

## Previous checkpoint: shared money
Shared money and paid supply crates (October 31), plus shorter terminal times:
CanyonLayout.DownloadSeconds now10/15/20/30/40 seconds for Common through Legendary.
Scene download labels rebuilt to match. Team multiplier/grace/decay remain as before.
CombatEconomy partial adds global host-authoritative SharedMoney, reset per run:
normal enemy kill5, sentinel kill40, breakable salvage10. Kill credit is immediate
for the whole party. Six gold salvage containers use E to break at <=2.8m; these
are interaction props rather than weapon targets. Four once-per-run paid crates:
Rare30 money at(-22,5.5,26) and(-15,-2,322), Epic75 at(23,7,38) and station
(36,13.5,689). Positions in CombatEconomy.MoneyCrateFloors/SalvageFloors. Bodies are
nonblocking runtime primitives under Combat Entities, blue/purple crates and gold
salvage, replicated through existing visual snapshots. Used crates shrink/gray;
salvage breaks into a burst and disappears. Existing free guard caches retained.
A purchase deducts once from shared wallet and grants every member an independent
matching-tier bonus choice, including outside players and downed players on revive.
Uses typed bonus queue, retaining tier on rerolls. Host validates run, living sender,
choice state, proximity, unopened flag and funds. Repeated/concurrent messages cannot
charge twice; insufficient funds have no effect. Purchase indices and fixed prices
are validated on host. World labels, nearby objective panel with cost/reward/action,
and persistent PARTY MONEY panel above health display make the transaction clear.
E interaction priority: nearby salvage/paid crate, then terminal/cache, then zip.
Snapshots include wallet, purchase count, all opened/broken flags. Stop/Begin clears
states/props; materials are cached and released with the combat component.
Validation: final Windows build success; solo passes actual salvage income, normal
and boss kill bounties, insufficient funds, purchase/deduction, Rare/Epic exact
choices and rerolls, duplicate prevention and reset. Six real processes passed
remote Rare/Epic purchases, shared wallet snapshots and flags, duplicate requests
charged once, outside party rewards and downed player's queued Epic reward on revive.
Fixture removes ordinary XP drops to isolate wallet choices; waits1s after long
remote teleport before E so host position validation is not tested against stale
interpolation. Original .4s long-teleport fixture rejected Epic requests correctly;
fix was test synchronization, not relaxing gameplay validation. Existing download
math/queue/decay, all five terminal tier durations/rewards, free cache rewards, full
two-process combat/upgrade/weapon/revive and full37-kill wave/boss run passed. Paid
crate/money HUD preview inspected: Docs/Playtest/paid-supply-crate.png.
Latest standalone: Builds/Swarm-SharedMoney/Swarm.exe.
Checkpoint: Backups/Swarm-shared-money-2026-10-31.zip.
Stop Play > Swarm > Open Canyon Playground > Play for new scene labels. Press F for
combat; earn money from enemies, press E near gold salvage or blue/purple paid crates.
Next: user playtest all additions; balancing/prices are initial prototype values.
Older build paths/coordinates/times below are historical checkpoints.

Tiered download terminals (October 31): five color-coded terminals, each granting
an upgrade choice of its EXACT tier to every party member. Solo times: Common20s,
Uncommon30s, Rare40s, Epic55s, Legendary75s. Existing +10% per additional living
occupant (cap1.5x), 7m diameter,2s empty grace,2 percentage points/sec decay retained.
One active download at a time; each terminal once per run. Finished states/progress
remain independently archived until Stop/Begin. An abandoned zero-progress attempt
can be switched away from and retried. Current selected index defaults2 (old Rare
maintenance deck), preserving existing DownloadCenter/default probe behavior.
Locations in CanyonLayout.DownloadCenters: Common(12,0,-5), Uncommon trail shelter
(-36,-1.9,342), Rare(0,12,692), Epic upper basin terrace(23,7,53), Legendary west
station platform(-30,8,650). DownloadSetup editor installer owns a separate root
Objectives / Download Terminals, invoked by DrillBowlSetup after station relocation
and canyon installation; full rebuild preserves all five. Removed old lone deck
console/zone from station template. Five unlit tier materials, colored console,
boundary and text labels with tier/time; white progress arc, red empty arc, green
completed arc preserve the tier boundary color. Ground samples cached at Awake
make circle/arc follow sloped trail ground, ignoring console and overhead canopy.
CombatDownload properties now refer to selected terminal with arrays of states and
progress. Snapshot includes selected index/all states/progress for every peer.
Pressure spawn candidates surround selected terminal and use existing reachable
navmesh/safety checks. Each of five locations passed actual guard spawning.
Added a per-player typed bonus queue alongside pending count: -1=random cache,
0..4=fixed terminal tier. Completion/revival/XP overflow retain the reward tier;
rerolls keep all choices at that exact tier. Cache bonuses remain random. Metadata
is saved/restored per player and replicated; no tier is accepted from client input.
Objective HUD shows viewed terminal tier/base solo time and promised reward. Nearby
inactive terminals can be inspected without changing the active one.
Validation: final Windows build succeeded. Five-terminal solo probe passed rates,
empty decay, exact choices/rerolls, once-only completion, archived states, concurrent
start prevention, guards at all five positions and reset. Two real processes passed
sequential completion/reward of all five tiers, outside teammate rewards and all
completed states replicated. Six real processes passed five/six-player speed,
empty grace/decay, reentry, dead-occupant exclusion, guaranteed Rare choices for all,
downed distant player's queued exact tier on revival. Existing download math/queue,
cache rewards, loadout cap/reroll/skip/queue, basin layout and station wall/zip/deck/
slide/overlook movement passed. Legendary HUD/console preview inspected and saved at
Docs/Playtest/legendary-terminal.png. Latest build: Builds/Swarm-TerminalTiers/Swarm.exe.
Checkpoint: Backups/Swarm-terminal-tiers-2026-10-31.zip.
Stop Play > Swarm > Open Canyon Playground > Play to reload the changed map.
Next: user playtest timing/reward tradeoffs. Older paths/coordinates below historical.

Upgrade rarity (October 31): all normal level/cache/download bonus offers now
roll one rarity per card, owned/validated by the host alongside the existing offer
and choice revision. Five tiers: Common/Uncommon/Rare/Epic/Legendary, initial weights
50/28/15/6/1 percent. Rerolls refresh types and tiers; skip forfeits both. Weapon
levels/rank caps and four slots remain separate from rarity.
CombatRarity.cs implements stat bonuses: focused gun damage/fire rate and extra
weapon damage +10/15/25/20/30% respectively by tier. Extra weapons also gain their
normal level stats, with per-weapon rarity multipliers saved independently. Global
Power uses60% of those bonuses (6/9/15/12/18%) so focused gun damage retains value;
gun utility upgrades add their usual rank/projectile plus half the bonus as gun
damage (5/7.5/12.5/10/15%). Recovery adds .3/.45/.6/.75/1 HP/s per accepted rank,
max5 ranks, and HUD now displays actual accumulated regeneration.
Every Epic pick also grants the shared-build perk: first20% critical chance, later
+5%, capped50%; primary projectile and pulse hits may deal2x. Every Legendary pick
adds shockwave rank: every6 direct weapon hits initially, then5/4/3 with further
ranks, creates3m burst for50% triggering hit damage (including a critical's damage).
Shockwaves and existing splash use raw secondary damage and cannot trigger more
shockwaves/criticals; terrain blocks the burst. Effects/counters are per player,
projectile damage uses its owner context, and all snapshots include rarity offers,
weapon multipliers, regen adjustment, crit and shockwave state/counters. Epic and
Legendary perks apply to all equipped weapons and are labeled explicitly.
ChooseUpgrade uses ApplyTieredUpgrade; direct ApplyUpgrade retains base behavior
for setup helpers/legacy probes. ResetLoadout resets rarity. Existing bonus queue,
healing20, caps, independent choices and upgrades stay intact. Cards now use tier
colors, separate rarity/category headings, accurate numeric bonuses and perk text.
Bonus heading generalized from CACHE REWARD to BONUS UPGRADE. HUD shows unlocked
crit chance and shockwave charge. No scene rebuild required for this code update.
Validation: Windows build success. Rarity probe passed five damage tiers, independent
weapon level/multiplier stacking, critical damage, six-hit nonrecursive shockwave,
weighted rolls/valid offers, real choice application and fresh-run reset.3000-card
sample counts1483/872/461/162/22. Two-process rarity test passed replicated offers,
independent Epic/Legendary builds and actual owner-specific crit/shock combat.
Existing loadout slots/caps/rerolls/skip/queue, cache rewards, download math/queued
rewards, projectile pierce/ricochet/splash/wall blocking and full two-process co-op
combat/choices/revival checks passed. Card screenshot visually inspected at
Docs/Playtest/rarity-upgrades.png. Latest standalone: Builds/Swarm-UpgradeRarity/Swarm.exe.
Checkpoint: Backups/Swarm-upgrade-rarity-2026-10-31.zip.
Validation environment note: old TEMP SwarmNetworkValidation cache had lost package
metadata and Burst binaries. Package metadata repaired from main project's intact
PackageCache, but stale Burst hashes still blocked build. A fresh persistent clone
at C:/Users/ronny/AppData/Local/SwarmValidation/Rarity20261031 built and passed tests.
Use that validation project for subsequent builds; original project never needs a
batch build while its editor is open. No dependency/version changes in main project.
Next: user playtest upgrade choices/balance; money and priced crates are a possible
future discussion. Stop Play, let Unity compile, then Play for the code update.
Older paths/coordinates below are historical checkpoints.

Maintenance deck download terminal (October 30): center CanyonLayout.DownloadCenter
(0,12,692), radius3.5m; console at(0,12.45,695), sign and cyan circle. In a combat
run press E inside to start, once per run. DownloadTerminal scene component draws
boundary/progress arc independently on each peer from replicated combat state.
CombatDownload partial is host authoritative. Solo rate2.5 percentage points/sec
(40s), +10% per additional living occupant, max1.5x at6 (~26.67s). Empty zone has2s
grace then loses2 percentage points/sec, clamped0. Reentry resumes progress; no
reset/failure at0. At empty0 pressure guards stop and normal waves resume; global
objective yields to other HUD tasks until near the terminal or engaged again.
Occupancy uses horizontal center within3.5m and vertical band[-.3,+3.2] relative
to deck, excluding dead players/zips/under-deck and high overhead players, allowing
normal jumps. When occupied or positive progress remains, ordinary waves wait;
terminal spawns one reachable enemy every4s after1.5s, max6 live download guards
and24 total enemies, with actual placements>=12m from every living player. Mix of
runner/chaser/spitter from deck edges/ramps. Enemies remaining at completion stay.
At100%, all party members receive one independent bonus upgrade choice regardless
of location; downed members retain it for revival. Bonus pending changed frombool
toint in solo/per-player snapshots so simultaneous cache/download rewards queue
without loss; updated loadout probe accordingly. Completion and rewards occur once.
New E command validates sender/context/run and terminal state. Snapshots carry
state/progress/occupants/empty clock/spawn totals. HUD shows progress bar, count,
multiplier/decay, shared reward; pause/leave controls moved below the taller panel.
Reset/Stop clears terminal state and tracked guards; practice shows idle circle.
Validation: final Windows build success. Solo exact10s->25%, height/jump band,
2s grace,3s->6point decay, resume, upgrade pause, zero clamp/abandon, actual guard
spawn, reward queue with another bonus, once-only/reset/cleanup passed. Six real
processes passed remote activation, five/six player rates1.4/1.5x, empty grace and
decay, one-player resume, exclusion of a downed occupant, outside-player reward,
downed distant player's queued reward on revival, replicated100% and independent
choices. Existing cache rewards, loadout queue/skip/rerolls, two-process co-op combat
and station zip/deck/overlook checks passed. Active/empty HUD/ring captures visually
inspected at Docs/Playtest/download-active.png and download-empty.png.
Latest standalone: Builds/Swarm-DownloadZone/Swarm.exe.
Checkpoint: Backups/Swarm-download-zone-2026-10-30.zip.
Stop Play > Swarm > Open Canyon Playground > Play. Start combat with F, go north
to the drilling station maintenance deck, enter circle and press E.
Next: user playtest all accumulated additions. Older paths/coordinates below are
historical checkpoints.

Canyon route encounters and trail shelter (October 29): CombatJourney.cs adds
host-authoritative once-per-run encounters at(28,-2,200) and(7,1,535). Triggered
by any living party member within20 horizontal metres during combat (AutoSpawn).
Practice remains movement-only; F begins combat from the basin. Each gets1.6s
warning, then staggered arrivals .75s apart:5 bridge scouts,4 station outriders,
mixture of runners/chasers and one spitter each. Placements are sampled onto
reachable navmesh and checked >=12 horizontal metres from every living player.
One route encounter at a time; ordinary wave scheduling waits during warnings,
arrivals and fighting, then resumes. No gates/countdowns requiring all players.
Normal damage, enemy pursuit, kills, XP and shared party collection apply; no extra
bonus choice. Unreachable/unsafe placements retry without exhausting spawn slots,
with12s arrival timeout so a failed placement cannot stall the run. Encounters
reset and release tracked enemies on Stop/Begin. Nearby players can keep traveling.
Combat snapshot includes both stages/timers/alive counts/totals, and objective HUD
shows CANYON PASSAGE, warnings and remaining enemies. Normal cache/drill priorities
retained. Sentinel now prefers the current POI; passage uses reachable nearby
spawn fallback instead of always spawning back in the basin.
Greybox shelter after bridge at(-36,-2,342): marked side pocket, canopy and low
cover, physically open to players/enemies. It is a place to regroup, not a safe
zone or healing station. Existing quarter pipe includes the20% taller curve.
Validation: final Windows build success; actual solo gun kills cleared all9 route
enemies, normal XP pickups, warning/pause, once-only, reset/cleanup and nearby
station sentinel passed. Six real processes passed remote trigger of each shared
encounter, counts/state replication, clearing and no duplicate/revisit spawning.
Co-op fixture uses normal kills and removes XP drops to isolate encounter states;
solo probe collects actual drops. Existing two-process co-op combat/loadout/choice/
revive tests and both-cache solo rewards passed. Full37-kill normal wave/boss run
and complete46.82s walking route/bridge/recovery/zip/reveal passed. Ambush HUD preview
visually checked at Docs/Playtest/route-ambush.png (test fixture500-damage gun).
Latest standalone: Builds/Swarm-RouteEncounters/Swarm.exe.
Checkpoint: Backups/Swarm-route-encounters-2026-10-29.zip.
Stop Play > Swarm > Open Canyon Playground > Play. Press F for combat, head north
past quarter pipe, bridge scouts, shelter, bank, ledges/zip and station outriders.
Next: user wants to test all additions together later; await playtest feedback.
Older coordinates/build paths below are historical checkpoints.

Quarter pipe height adjustment (October 29): vertical curve scaled1.2, increasing
maximum rise from5.37m to6.45m. Width, length and tapered ends unchanged. Latest
standalone remains Builds/Swarm-RouteFeatures/Swarm.exe, refreshed for this change.
Next proposed work: a small canyon enemy encounter to test combat during travel;
not yet authorized or implemented. Await user feedback on taller quarter pipe.

Expanded canyon route / one-sided quarter pipe (October 29): new geometry in
CanyonJourneySetup, rebuilt automatically with the station/full prototype.
- Mountainside quarter pipe on RIGHT of first downhill: along18..44m from
  JourneyPoints[2], inside edge5m from center, 7m wide, radius7.247m, curve0..75deg,
  about5.37m high, tapered ends and rock backing. Ordinary mesh-collider terrain.
- Banked turn on LEFT at z360..422.5, seven-meter band with outer edge3.2m above
  the trail, tapered ends; ordinary slope gravity permits carving.
- Three raised rock ledges near z463..506 on right side, tops6/6.7/5.8m, width6,
  length12, 4m staggered gaps, entry/exit slopes. Continuous lower bypass remains.
-18m/s narrows zip from(-23,4.5,412) toNarrowsPoint(10,5,8.8), landing on first
  ledge with room to steer; revised endpoint avoids overshooting the landing.
- Split rock arch marks the final approach. Nonblocking route signs added.
Quarter pipe exposed a steep-ground sticking bug: MomentumMotor now records steep
support normals and applies downhill gravity/vertical slope projection on steep
supported faces, even without crouch. Longer steep-only ground query accounts for
capsule contact beside the central ray, with distance limited by capsule size and
surface normal; ordinary flat/runnable ground snap still uses its original reach.
No special quarter-pipe tags, launch boosts or item requirements.
Validation: final Windows build success. Real motor entered quarter pipe at18m/s,
rose5.65m and rolled back inside edge. Bank carve passed (peak12.93m/s), complete
narrows zip+first ledge landing, both ledge jumps, missed-jump ground recovery,
enemy nav access via entry/exit slopes passed. Entire walking bypass still46.82s;
original bridge jump/recovery/transfer, downhill run and station reveal passed.
Slope uphill rollback, flat/jump carry, timed slide hop/no air stacking/high-speed
preservation, original wall/zip chain, drill damage/knockback/navigation and all
station zip/deck/slide/overlook checks passed after the motor change. Screenshots
in Docs/Playtest/route-*.png visually inspected. Static map additions; networking
protocol unchanged (last six-player cache test passed on preceding journey map).
Latest standalone: Builds/Swarm-RouteFeatures/Swarm.exe.
Checkpoint: Backups/Swarm-route-features-2026-10-29.zip.
Stop Play > Swarm > Open Canyon Playground > Play. Take north exit; quarter pipe
is on right of first descent, then bridge, banked turn, zip/ledges and rock arch.
Next: user movement feel playtest. Older coordinates/builds below are historical.

Long canyon journey (October 29): station center moved from (0,0,145) to
(0,0,650), preserving the whole station, hazard footprint, signs, cache and zip
endpoints. CanyonLayout.StationCenter/StationPoint now provide runtime positions;
station builder places the established template as a unit and explicitly shifts
world-space line positions/endpoints. Old temporary connection removed.
CanyonJourneySetup installs a continuous mesh-collider trail from z76 to618,
28m across with a 44m-wide bridge gully, winding high rock walls, gold markers,
nonblocking signs and a station entry slope. Entry rises6m then descends8m for a
momentum run; bridge is one feature early in the journey, followed by winding
narrows and the station reveal. No gate/countdown. Nav bounds expanded to cover
both POIs and the whole passage. Practice HUD area/hints distinguish route stages.
Upper bridge from(28,8,215) to(-25,8,300), width7m, gap5.5m; walking approaches
at each end, ordinary bounce wall and16m/s transfer zip. The dry gully below is
walkable; missing the bridge gap lands on the lower route, no forced respawn.
Validation: real-controller full lower route walk46.82 simulation seconds without
slide hops/zip, all waypoints reached. Downhill slide peak21.89m/s. Bridge jump,
missed-jump recovery, full zip transfer, nav connection and rock-wall visibility
occlusion/reveal passed. Existing relocated drill crossing/damage/knockback/enemy
kill and station wall/zip/deck/slide/overlook walk+gap jump passed. Basin layout
and full37-kill wave/boss run passed. Six-process cache co-op passed remote
station activation, replicated state, independent party reward, subsequent basin
reward and once-only/concurrent challenge rules at the relocated station.
Final build succeeded. Graphical bridge/reveal/overview inspected; previews in
Docs/Playtest/journey-*.png (fog disabled only for documentation overview capture).
Latest standalone: Builds/Swarm-CanyonJourney/Swarm.exe.
Checkpoint: Backups/Swarm-canyon-journey-2026-10-29.zip.
Stop Play > Swarm > Open Canyon Playground > Play, then take the basin north exit.
Next: user playtest distance/pace/route readability; production canyon art and
encounter progression still future work. Older coordinates/build paths below
are historical checkpoints.

Station supply overlook and route signs (October 29): side platform x30..42,
z179..193, floor13.5, cache(37,14.25,187). Walking ramp from deck(23,12,181) to
(30,13.5,181); alternate 4m gap jump at z189 with low takeoff lip. East parapet
has separate openings for both approaches. Three nonblocking physical route signs
mark safe rim, return slide and optional supply overlook. Existing landmark count4.
Each POI now has an independent once-per-run cache: 4 guards /35s /party bonus
upgrade. One challenge at a time, either order, completed/failed states preserved
until new run. Host validates nearby activator; snapshots replicate selected index
and both states. Client requests stay host-authoritative. Nearby HUD gives cache
prompt priority and active challenge shows remaining guards/time. Basin cache stays.
Validation: actual solo gun kills cleared both caches in one run, each reward once;
pause, timeout, cleanup and reset passed. Real motor passed overlook walk and gap
jump, station bounce/zip/deck/return slide, and existing drill/deck navigation tests.
Six real processes passed remote station activation, shared rewards with independent
choices, subsequent basin reward, prevention of concurrent challenges and reuse,
state replication and cleanup. Co-op reward fixture removes normal XP drops to
isolate bonus rewards; solo probe uses normal XP and queued choices. Build succeeded;
station overview visually inspected. Latest build: Builds/Swarm-StationCache/Swarm.exe.
Checkpoint: Backups/Swarm-station-cache-2026-10-29.zip.
Stop Play > Swarm > Open Canyon Playground > Play to reload the scene.
Next: user playtest; no additional unsolicited implementation.

Station movement loop (October 29): east bowl escape zip from (10,8,157) to
(10,17,184), speed14; ordinary bounce wall at (7,7,158) with cyan face marker.
West deck return slide at x=-10 from z179/y12 to z167/y8, then onto the natural
bowl. Existing safe routes and direct bowl exit remain open. Zip uses existing
look-directed travel, jump momentum and crouch-drop mechanics. No motor changes.
Latest standalone: Builds/Swarm-StationLoop/Swarm.exe.
Validation: Windows build succeeded. New real-controller station probe passed
bounce-to-grab, complete ride/deck landing, jump momentum, drop, and return slide
(peak21.85m/s). Drill crossing/damage, all deck approaches/enemy navigation and
original wall/zip chains passed. Overview visually inspected.
Checkpoint: Backups/Swarm-station-loop-2026-10-29.zip.
Stop Play > Swarm > Open Canyon Playground > Play to reload updated geometry.

Drill head widened again at user request: radius8.8m, diameter17.6m (twice the
previous width). Damage cone uses the same radius; generated warning ring radius
9.5m. Height, timing and damage unchanged. Latest standalone remains
Builds/Swarm-Station/Swarm.exe, refreshed with this adjustment.

Drilling station expansion (October 29): north maintenance deck at (0,12,187),
52x16m, side parapets, rear wall, two large equipment bounce faces and low cover.
Two 8m-wide outer approaches from the safe rim, plus a 10m direct bowl exit ramp.
West rim now has route stripes and a 12x18m staging platform for the party.
No gathering gate. Drill timing, damage and the uninterrupted bowl stay intact.
Still greybox; connecting canyon routes/bridge, zip connection and optional station
cache remain future work. Latest standalone: Builds/Swarm-Station/Swarm.exe.
Validation: Windows build succeeded; all three deck approaches passed real motor
walking; navigation reaches deck and west platform. Drill crossing, damage, enemy
kill and original basin layout checks passed. Overview inspected and saved at
Docs/Playtest/station-overview.png.
Stop Play, use Swarm > Open Canyon Playground to reload the updated scene, then Play.

Timed slide hop: jump0.08-0.28s after grounded slide entry while moving at least
80% of runSpeed. Adds7% at run speed, diminishing toward2x runSpeed (default22m/s).
Only landing rearms it; air inputs cannot stack. Terrain speed above22 stays intact.
Ordinary slide jumps retain momentum. Practice/combat show a brief SLIDE HOP bonus
readout. Validated11 ->11.77m/s, repeated landing chain, no airborne stacking,
expired/standing jumps, fade at20, preservation at30. Existing slope rollback,
flat carry, ordinary jump, drill crossing/damage and wall/zip chain checks passed.
Latest standalone: Builds/Swarm-SlideHop/Swarm.exe. Code-only update; stop Play and
let Unity compile before testing. Next: user feel test on flat ground and the bowl.

Drill bowl greybox (October 29): installed north of the basin at (0,0,145). The
north wall has a 16m opening and a temporary wide ramp to the bowl; finished canyon
routes/broken bridge and the rest of the drilling station remain future work.
Mesh-collider bowl: radius24m, depth8m, safe rim24-32m, recovery apron32-50m, four
outer recovery ramps. Native gantry, piston and cone; new assets in Assets/Geometry
and Materials/DrillSteel, DrillSignal. Full PrototypeBuilder rebuild preserves it.
Drill cycle: raised4.5s, amber warning1.8s, descent0.65s, buried1.2s, rise2s.
Swept cone strikes players for30 and enemies for100, once per actor per plunge;
player knockback10 outward+7 upward, enemy survivors displaced on navmesh. Ordinary
XP/kill credit uses the existing combat path. Nearby HUD shows phase and countdown;
warning footprint/beacon use an unlit runtime material for clear color states.
Works in movement practice with separate100HP (knockout respawns/restores health),
and in combat with real player HP. Host runs contacts and sends independent drill
clock/practice health/numbered impulses even outside a combat run. Upgrade pauses
freeze the drill. Motor external impulses have a short launch window; normal slide,
slope and jump rules remain unchanged. Navigation bounds cover both POIs/access.
Validation: Windows build success; actual rim-to-rim slide crossed at peak19.7m/s;
rim/access navigation, one-hit damage, ground launch and enemy kill all passed.
Two-process drill test passed practice/combat damage, client impulse/state and
upgrade pause/resume. Existing movement, basin layout (four landmarks now), complete
37-kill boss run, and six-instance co-op regression passed on the extended scene.
Warning/down graphics inspected; previews in Docs/Playtest/drill-warning.png and
drill-down.png. Scene/material/mesh assets copied to source; latest standalone is
Builds/Swarm-DrillBowl/Swarm.exe. Checkpoint: Backups/Swarm-drill-bowl-2026-10-29.zip.
Next: user playtest bowl crossing/timing/scale before adding the rest of the station
or replacing temporary access with the canyon routes and broken bridge.

Weapon feedback and readable HUD pass (October 28): CombatHud.cs replaces the dense
combat panels with large health/XP bars, a four-card loadout with icons/ranks/charge,
wave header, sorted party health, cache objective, and large categorized upgrade
cards. Reroll/skip buttons remain explicit. Practice retains the detailed movement
readout; combat hides floating landmark debug hints. Player labels avoid HUD panels
and modals. Mouse recapture respects menu buttons; Escape menu keeps co-op live.
WeaponPresentation.cs adds native meshes (V boomerang, finned rocket, fletched arrow),
colored launch/hit/trail cues, full-radius blast rings, and a persistent pulse-range
outline with charge arc. Boomerangs turn on a short cubic curve toward the player's
current position; return color changes to blue. Damage values/base movement unchanged;
boomerang hit width follows its visible body better. In co-op only the local player's
persistent range/charge outline is rendered; other players' actual pulses remain.
Client snapshots preserve custom meshes/trails/colors. Cached tint materials fix black
client lines. Departed players' range indicators are pruned safely.
Validation: final Windows build succeeded. Loadout, gun combinations/cover, complete
37-kill boss run and new moving-boomerang/pulse-follow/cleanup checks passed. Final
six-instance co-op passed on host and all five clients, including custom weapon meshes,
line colors, own-range filtering, XP/choices/pause, down/revive, stop cleanup and a
pulse-owner leaving mid-run. Revive tests allow immediate passive regeneration above
50 HP. Graphical captures inspected at 1280x720, 1600x900 and 1920x1080; text and cards
fit, landmark overlap and description clipping fixed. Previews are in Docs/Playtest.
Fresh build: Builds/Swarm-HUD-2026-10-28/Swarm.exe (the old standalone instance
was open and locked the usual build folder). Source checkpoint: Backups/Swarm-hud-weapons-2026-10-28.zip.
Next: user playtest of HUD reading, boomerang return steering, pulse clarity and
weapon feedback while moving; no extra systems added during the polish batch.

Shared co-op combat (October 28): host-authoritative enemies, damage, shared XP,
cache, waves and boss; independent player health/loadouts/offers/rerolls/skips.
Host alone starts/stops/retries; all players must join before combat begins.
Any living player's XP pickup credits everyone. Any pending living-player upgrade
pauses party combat/movement until all choices finish. Escape releases the mouse
without pausing co-op. Downed players revive at their own spawn with 50 HP at the
next wave break; all down permits host retry. +55% enemy HP per extra player.
CoopCombat.cs explicitly saves/loads individual player simulation contexts and
replicates primitive visuals/personal state over NGO named messages at 10 Hz.
Clients interpolate visuals; movement remains owner-authoritative. Commands check
sender, run and choice revision. Party HUD shows health/down/choosing status.
No mid-run join, Relay or host migration. Snapshot replication is a LAN prototype.
Validation: Windows build succeeded. Two-player probe passed; final six-instance
probe passed on host and all five clients: both firing, shared collection/XP,
independent rerolls/choices, invalid/duplicate pick rejection, synchronized pause,
individual downing, revival, replicated visuals, host-stop cleanup and offline
restoration. Host also verified slam damage on every in-range player and a projectile
hitting only its first struck player. Solo loadout, gun build/cover, combat loop,
enemy attack, cache and full 37-kill boss victory regression checks passed.
Next: hands-on two-window test (Unity + Builds/Swarm/Swarm.exe, join 127.0.0.1,
host presses F after joining), then tune co-op pressure and readability.

Four-slot loadouts and four additional weapon types (October 28): starter auto gun
occupies one slot; select up to three of boomerang, pulse field, rockets, piercing
bow. Extra weapons rank up to five; capped/new-at-full offers are excluded. Gun
modifiers remain gun-specific; Power and Recovery are separate passives. Three
rerolls per run (T), unlimited skips (X); skips forfeit rewards and healing, queued
XP/cache rewards still appear. Retry resets all loadout state. HUD shows equipped
weapons and ranks. New runtime code is in CombatLoadout.cs (partial class).
Validation: Windows build succeeded. Loadout probe passed actual boomerang return
hits, pulse kills, rocket splash, bow piercing, slot limits, capped/unique offers,
reroll budget, skip/queued rewards and reset. Existing gun combination/cover checks,
cache challenge, and full 37-kill mixed-wave boss victory passed.
Next: user playtest weapon feel, offer frequency and balance. Combat remains solo.

Hoist perch: added a 6x6m open deck at (-7,10,18 surface height), beside the upper
lift. It supports a lift-launch landing, a jump toward the crossing zip, and a
southbound return-slide slope to the yard. No roof or immunity; the slope provides
enemy access. Area HUD identifies the perch. Reopen the scene after refreshing.
Validation passed: actual-controller lift launch onto the deck, airborne crossing-line
grab, downhill slide to the yard, and complete enemy navigation up the ramp. Existing
wall-to-zip chains, crouch release, crossing clearance, and mixed waves still pass.
Next: hands-on playtest of landing room and route flow before adding more structures.

Hoist bounce practice: added an angled ordinary wall west of the vertical line,
with a cyan visual stripe and clear ground beneath. Intended chains are wall bounce
to zip grab, and zip hop to wall bounce back to zip. No special bounce tags or new
movement rules. The crossing line must remain unobstructed.
Validation passed for wall-to-zip and zip-to-wall-to-zip chains using the scene
geometry, safe ground below, existing zip/crouch behavior, crossing clearance, and
the mixed-wave encounter. Reopen the canyon scene to load the wall; hand-tune spacing
from the user's playtest next.

Crouch now drops from zip lines: Ctrl or C releases without a jump impulse and
preserves the current line velocity. Existing regrab delay still applies.

Mining zip lines: added a cyan hoist lift and a crossing line from the west upper
route to the east conveyor. E grabs/releases; Space hops off. Facing on grab selects
up/down or either end, looking around does not reverse travel, and line velocity
carries into the hop. Baselines are 9 m/s vertical and 14 m/s crossing; extra incoming
speed along the chosen direction decays gently. Cache gets E-interaction priority.
The footprint is now labelled as one mining basin with yard, pit/hoist, and conveyor
subareas, reflecting the user's scale correction. Separate POIs are deferred.
Validation passed: line directions and perpendicular fallback; stable direction while
looking around; fast-entry and hop momentum; regrab delay; endpoint exit; collision
blocking; actual installed crossing path. Uphill/flat/jump movement and cache reward
checks still pass. Reopen the canyon scene after refresh to load the two lines.

October 25 connected layout: camp, mining basin, and broken aqueduct are now grouped
POIs with distinct silhouettes. Ground routes loop around the mesa; the west route
has wall-bounce faces and a five-meter aqueduct gap; the east ramp provides continuous
access to the cache terrace. Cache moved to (23,7.75,46); guards spawn on the terrace.
Area/route hints and landmark labels help orientation. The sentinel prefers the basin
with a reachable fallback. Original project snapshot saved in Backups before rebuilding.
Next hands-on test: explore both routes without combat, try the jump and return slide,
then activate the aqueduct cache while fighting. Camp is a regrouping area, not immune.
Validation passed: complete navigation paths through the basin and up to the cache;
underpass; route heights and three landmarks; actual controller run up the east ramp
and jump across the aqueduct gap; cache activation/reward/reset on its new terrace;
mixed-wave victory and bolt combinations. Reopen the canyon scene after refreshing
Unity to load the rebuilt geometry. Overview image is in Docs/CanyonOverview.png.

October 25 exploration step: added an optional supply cache on the right of the
canyon entrance. E within 3.5m activates a 35-second guard fight while normal waves
continue. Clearing the guards grants one extra upgrade choice; timeout loses the
bonus and keeps the run going. Menus/upgrades pause the timer. Bonus choices queue
behind normal level-ups. Cache objective labels remain visible with names toggled
off. The marker and challenge reset on retry and remain solo-only.
Validation passed: activation range and duplicate rejection; real guard kills and
single bonus choice; pause during selection; timeout without ending the run;
cleanup and retry reset. Mixed waves and bolt combinations still pass. Next hands-on
test: start combat, explore right from the entrance, find the gold cache, press E,
and judge whether the optional fight and reward feel worth the detour.

October 25 enemy variety: runners (small/leaning/yellow/fast) and spitters
(wide/purple/ranged) now join the existing red chasers and orange sentinel.
Spitters hold range and telegraph a locked aim line before firing a slow dodgeable
projectile. Names appear above nearby visible enemies; N or a panel checkbox toggles
them. Wave 1 introduces runners, wave 2 adds spitters, wave 3 mixes the types.
Solo combat remains separate from the multiplayer movement test.
Validation passed: runner movement and appearance configuration; spitter windup,
projectile damage, locked-shot dodge, wall blocking, cleanup; all mixed waves through
victory (13 runners and 7 spitters across the three waves); existing bolt combinations.
Hands-on readability testing with names toggled on/off is the next step.

October 25 build expansion: three bolt modifiers (ricochet, piercing, explosive hits)
now stack with one another and with the basic upgrades. Level-ups roll distinct
choices and guarantee an available modifier; shortcuts 1/2/3 select choices.
Current build is shown in the HUD. Hit flashes, trails, explosion rings, and defeat
bursts add feedback. Single-volley integration scenarios cover real hit combinations
and wall blocking. Hands-on playtesting of build feel remains necessary.
Validation passed: each modifier alone; all three on one bolt (four kills from one
shot); wall blocking; unique offers; XP overflow through two upgrade selections;
rank cap and run reset. Existing combat and all-wave victory tests also passed.
Windows playtest build was refreshed with these changes.

October 25 follow-up: user approved uphill slide rollback and a short encounter
sequence. Uphill slide steering now cannot reverse downhill momentum back toward
held uphill input; direct support probing supplies slope gravity and slope-following
vertical motion keeps slides grounded. Combat now has 8/12/16-enemy waves, eight-second
breaks, and a 320-HP orange Canyon Sentinel with a 1.1-second warned area slam.
Defeating it gives a victory screen. These changes need a hands-on feel test.
Validation: actual-controller slope test passed uphill slowdown and rollback while
holding forward, flat slide carry, and jump carry. Accelerated encounter integration
passed all 37 kills across waves 1–4 and the victory screen. Spawn positions now
require a complete navigation path to prevent enemies on disconnected ledges from
stalling wave completion. Windows build succeeded.

October 25 playtest feedback: automatic aiming/shooting, upgrades, and the combination
of movement and combat feel good to the user. XP attraction is liked, but its reach
felt too long. Reduced attraction radius from 8 to 5 meters; collection distance and
attraction speed are unchanged. Next playtest should assess this shorter radius.

The user initially did not see enemies or firing. The latest fix adds F to start,
three enemies ahead, and explicit weapon status; it passed automated checks but
has not yet been confirmed by the user in a hands-on playtest.

Stop Play, Assets > Refresh, wait for compilation, press Play, click Game, press F.
WASD move; mouse look; Space jump/bounce; Ctrl/C slide; R reset; Esc unlock/pause solo combat.
First test: enemies appear, auto-bolts fire, collecting green XP opens upgrades,
and fighting while sliding/jumping feels good. Tune from user feedback next.

## Current limits
- Combat is solo only; hosting/joining ends the combat test.
- Multiplayer uses direct IPv4 / UDP 7777, no Relay or internet invite flow.
- Movement is client authoritative; no host migration or server reconciliation.
- No production boss, actual next map, or build carryover implementation yet.
- Primitive assets, no production art or sound pass.
- Windows playtest: Builds/Swarm/Swarm.exe. Scene: Assets/Scenes/CanyonPlayground.unity.
- No Git repository was initialized at this checkpoint. A source ZIP backup is saved
  in Backups; it excludes generated caches and the Windows build.


Drill size adjustment: widened the cone from 7m to 8.8m across (about 25%),
keeping its height/cycle/damage unchanged. Strike radius is now4.4m; warning ring
radius5.1m. Builder reads runtime head dimensions so visuals and damage stay aligned.

Double-width drill validation: build success; bowl crossing, all deck approaches, navigation, player/enemy damage and knockback passed. Updated overview visually checked.

Taller quarter pipe verification: Windows build success; ride up and return passed (6.51m rise from18m/s entry), bank/zip/ledge/recovery/navigation and entire46.82s walking bypass passed. Source mesh/scene and standalone updated.
