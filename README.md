# Swarm movement prototype

Latest map/reward playtest: **Builds/Swarm-CentralSlopesLoot/Swarm.exe**. See
[the central slopes and chests guide](Docs/Planning/CENTRAL_SLOPES_LOOT_PLAYTEST.md).

Previous combined playtest: **Builds/Swarm-TerrainTimerPause/Swarm.exe**. See
[the November 15 playtest guide](Docs/Planning/FIRST_BATCH_PLAYTEST.md) for terrain,
countdown/overtime, whole-party menus, and validation details.

For the current design review, implementation snapshot, proposed milestone, and
asset workflow, start with [the project review pack](Docs/Planning/README.md).
The detailed notes below include historical tuning and behavior that may have
been superseded; see the review pack's status document for current limitations.

Unity 2022.3.23f1. Open `Assets/Scenes/CanyonPlayground.unity` and press Play.
If the scene is missing, use **Swarm > Build Canyon Playground**.
The editor sets the canyon as its Play Mode start scene automatically. To open it
for editing, use **Swarm > Open Canyon Playground**. Click inside the Game tab
after pressing Play so keyboard and mouse input go to the game.

WASD: movement. Mouse: camera. Space: jump / wall bounce. Ctrl or C: slide.
R: return to spawn. Escape: release mouse. Click: capture mouse.

The first prototype focuses on third-person movement, slope slides, air steering,
and wall bounces on ordinary collision geometry. All moves are available at spawn.
The speed HUD helps evaluate momentum. Cyan surfaces mark suggested practice routes,
but ordinary walls use the exact same movement rules.

Slides have no artificial friction on flat ground. Downhill gravity adds speed and
uphill gravity reduces it. Jumping carries horizontal velocity into the air, including
when movement input is released on the launch frame. Release slide on the ground
without movement input to brake normally; walls still remove incoming momentum.

## Multiplayer playtest

Uses Netcode for GameObjects 1.8.1 with Unity Transport, direct IPv4 / UDP port 7777.
Press Play for offline practice. Press Escape to use the co-op panel, then Host or
enter the host computer's LAN IPv4 and Join. Click the left/center of the Game view
to capture the mouse again. While the cursor is released, local movement pauses.
Leave / cancel returns to offline practice. The party limit includes the host: six total.

For two instances on one PC, use **Swarm > Build Windows Playtest**, then run
`Builds/Swarm/Swarm.exe` alongside the editor. Host in one, join `127.0.0.1` in the
other. On a LAN, use the host's local address and allow the game through Windows
Firewall if prompted. Internet invites, Relay, matchmaking, and host migration are
not implemented. Ending the host session disconnects the clients.

The network player prefab is created automatically after package import. If needed,
use **Swarm > Prepare Multiplayer**. Movement is owner authoritative for this
prototype: the controlling client simulates movement, NGO synchronizes transforms,
and remote controllers are disabled. This prioritizes movement feel and is not an
anti-cheat or server reconciliation implementation. Player-to-player collisions are
disabled so party members do not obstruct each other's movement lines.

Development builds include an opt-in headless integration probe, activated only by
`-swarm-test-role host` or `-swarm-test-role client`. It moves the local avatar and
checks that six avatars, including remote avatars, change position. Use
`-swarm-test-seconds 35` for its duration; `-swarm-test-reject true` expects a full
party rejection instead. Normal play never activates this probe.

## Combat test

Press Play, click the Game view, then **F** to start, or Escape and **Start combat**
in the right-hand panel. An opening pack spawns ahead of you. Enemy count, total shots,
and weapon status distinguish idle targeting from firing or paused combat. The automatic
weapon targets the nearest visible enemy within 26 meters. Red chasers navigate
around terrain and deal contact damage. Green XP drops attract within five meters;
collect them to level up. Choose damage, firing rate, more bolts, or a bolt modifier. Each
upgrade heals 20 health. Defeat opens a retry button; retry starts a fresh build.

Escape pauses solo combat while using menus. Upgrade selection also pauses combat
and movement. **Stop combat / movement practice** clears enemies and pickups.
Hosting/joining ends the current solo run. Once everyone has joined, the host can
start a shared combat run with F. The encounter sequence is described below. Manual aim, a second
map, and build carryover between maps are not implemented yet.

Enemies use a runtime navmesh built from the canyon's world colliders. Projectiles
and pickup attraction respect walls. Player collision shapes do not block bullets.
All visuals remain primitive placeholders.

The development-only `-swarm-combat-test` argument runs an integration check covering
navigation, auto-fire, projectile kills, XP drops/collection, upgrade selection,
contact damage, defeat, retry, and entity cleanup.

Next: playtest fighting while moving, then tune the weapon and enemy pressure before
expanding the build system, waves, and bosses.

Design direction: PC co-op adventure survivor for 1–6 players; canyon followed by a
cyberpunk city and later regions; waves and a boss unlock each next map; builds carry
between maps in one run. Momentum mastery and player-discovered tricks are central.
Collisions remove incoming velocity without arbitrarily erasing tangent or falling motion.

## Uphill slides and encounter sequence

Holding forward during a slide no longer steers a downhill rollback back uphill.
Low-speed slides follow gravity; faster slides steer only into the forward half
of their current momentum. Grounded slides follow the slope vertically to stay
in contact with it. Flat slide speed and horizontal jump carry remain intact.

The solo test now has three waves (8, 12, then 16 mixed enemies), with eight-second
collection breaks after each clear, followed by the Canyon Sentinel. The larger
orange sentinel has 320 health and a four-meter area slam: it stops and warns
for 1.1 seconds, giving time to leave the orange circle or jump high enough.
Defeating it ends the test with a canyon-clear screen. This is a prototype elite
encounter, not the final production boss. Retry resets the run and its upgrades.

Development builds accept `-swarm-movement-test` for actual controller checks of
uphill slowdown/rollback, flat slide carry, and jump carry; `-swarm-wave-test`
accelerates encounter scheduling and weapon stats to check all waves through victory.

## Bolt builds and hit feedback

Level-ups offer three distinct random choices from eligible weapon unlocks, weapon
upgrades, gun modifiers, and passives. Pick with **1 / 2 / 3** or click; the current build
appears in the bottom-left HUD. Retry resets all upgrades.

- **Ricochet:** up to three jumps to new targets within 10 meters, respecting walls.
- **Piercing:** pass through up to three additional enemies. Remaining ricochets can
  redirect the bolt after piercing is used up. One bolt never directly hits the same
  enemy twice.
- **Explosive hits:** every direct hit deals 50% bolt damage to nearby enemies. Radius
  starts at 2.5 meters and gains 0.6 meters per extra rank (three ranks maximum).
  Splash respects walls and does not trigger recursive explosions.
- **Heavy bolts / Rapid fire / More bolts:** boost damage, rate, or volley count.
  Volley count caps at six, rate bottoms out at an 0.08-second interval, and capped
  choices leave the pool. Even volleys retain a central aimed projectile.

Modifiers combine: a bolt can pierce, explode on each hit, then ricochet to another
enemy and explode there. Enemies flash white on damage; bolts leave short trails;
explosions show expanding rings and kills show brief bursts. Cosmetic effects have
a bounded live count and are cleared when combat ends.

The development-only `-swarm-build-test` runs single-volley integration scenarios for
each modifier, all three combined, wall blocking, upgrade offers, queued level-ups,
rank limits, and fresh-run reset.

## Enemy recognition and variety

Names appear above nearby enemies when they are visible from the camera. Press
**N** or use **Enemy names** in the combat panel to toggle them; the sentinel keeps
its health label. Labels are meant to help compare recognition with and without text.

- **Chaser:** red, medium capsule, steady approach.
- **Runner:** yellow, smaller/narrower, forward-leaning silhouette; faster and more fragile.
- **Spitter:** purple, wider body with a muzzle; holds range, stops and shows an aiming
  line for 0.8 seconds, then fires a slow projectile toward the position it marked.
  Move, slide, or jump away from that line. Projectiles respect walls, deal 12 damage,
  and pause during upgrade selection and solo menu pauses.
- **Sentinel:** large orange silhouette and the existing warned area slam.

Wave 1 introduces runners, wave 2 adds spitters, and wave 3 mixes all three ordinary
types. The quotas and total XP remain unchanged. New projectiles are cleaned up on
combat stop/retry and when expired; enemy fire does not damage other enemies.

`-swarm-enemy-test` checks actual runner movement, shape/color configuration, spitter
warnings, projectile damage, dodging a locked shot, wall blocking, and cleanup.
The accelerated wave test also checks that runners and spitters occur in the mix.

## Optional supply cache

During combat, explore the aqueduct terrace for a gold supply cache with a vertical
beacon. Either side ramp leads to the upper routes. The panel shows its distance. Approach within 3.5
meters and press **E** before the sentinel encounter to start a 35-second challenge.
Defeat its marked guards for one extra upgrade choice. Normal waves keep running,
and you can move freely after activating it; this is an optional added fight.

The timer pauses with menus and upgrade choices. Ordinary XP and bonus choices are
queued separately so a level-up cannot overwrite the cache reward. The bonus does
not consume XP or increase the level, and can only be earned once per run. Cache
guards retain objective labels even when ordinary enemy names are hidden.

If time runs out, the bonus is lost and remaining guards stay in combat; the main
run continues. Retry resets the cache. It is not required to reach the sentinel or
win. In co-op, any living player can activate it; success awards every player a separate bonus choice.

`-swarm-cache-test` checks range/duplicate activation, real guard kills, the single
bonus reward, timer pause, timeout without ending the run, cleanup, and fresh-run reset.

## Connected canyon POIs

The current test footprint is now treated as **one mining basin POI**. The yard,
pit/hoist, and broken ore conveyor are its subareas; separate canyon POIs come later.

The scene now has three grouped greybox areas:

- **Abandoned Camp:** spawn and regrouping space, shelters/crates along the edges,
  and an open central gathering area. This is not an invulnerable safe zone.
- **Mining Basin:** a ground-level loop around the mesa, a tall mining hoist as a
  landmark, and open combat space. The sentinel prefers the basin when reachable.
- **Broken Aqueduct:** raised walkways, supports you can pass underneath, a broken
  span with a five-meter jump, and the supply cache on the east terrace.

The east ramp is a continuous route from ground level to the cache. The west route
adds wall-bounce faces and the optional gap jump. Both reconnect through the ground
basin, and the east slope doubles as a downhill slide route back toward the camp.
No movement items are required. Marks on the ground are visual only, so they do not
introduce collision bumps. The movement HUD shows your current area and a route hint;
landmark labels help identify destinations while exploring.

`-swarm-layout-test` checks navigation between areas, route surface heights, the
underpass, all three landmarks, an actual controller ascent of the east ramp, and
a running jump across the broken span. Geometry remains primitive and needs hands-on
testing for flow and sightlines.

## Mining zip lines

Two cyan lines support movement chaining: a vertical hoist lift beside the pit
(-11, 22 on the ground), and a sloped crossing line from the west upper route to
the east conveyor terrace. They work during movement practice, solo combat, and co-op combat.

- **E near a line:** grab it. Where you look when grabbing chooses the direction.
  Look up/down on the lift, or toward either end on the crossing line. A nearly
  perpendicular view uses incoming movement; at rest it consistently uses A → B.
- **Look around while riding:** direction remains unchanged until you detach/regrab.
- **Space:** hop off, carrying line velocity with a small jump/outward impulse.
- **E while riding:** release with the current velocity. Ends release automatically.
- **Ctrl or C while riding:** drop off without a jump impulse, keeping travel momentum.

The lift supplies 9 m/s and the crossing supplies 14 m/s. Faster incoming velocity
along the chosen direction is retained and gently decays toward line speed. Opposing
momentum does not reverse the look-selected direction. Regrab delay is 0.18 seconds.
Terrain blocks attachment and travel; the line sweep cannot teleport through walls.
Cache interaction takes priority over grabbing a line when close to the cache.
Movement pauses during solo menus and upgrade choices, including while riding.

`-swarm-zipline-test` checks both directions, look-around stability, perpendicular
fallback, entry/exit momentum, regrab delay, automatic exit, terrain blocking, and
travel along the installed crossing line.

## Hoist wall-to-zip practice

A slightly angled wall now sits west of the vertical hoist line, with a cyan stripe
suggesting a bounce face. It is ordinary solid terrain, using the same bounce rules
as every other wall. The floor beneath is open for recoverable misses.

Try jumping into the wall, bouncing toward the line, looking up, and pressing E to
catch it. You can also look toward the wall while riding, hop off with Space, bounce
back, and catch the line again. The crossing line remains clear of this wall.
`-swarm-wallzip-test` exercises both chains using the actual controller and scene wall.

## Hoist landing perch

A six-by-six-meter open perch sits beside the upper part of the lift, above the pit.
Ride the vertical line near the top, look diagonally toward the deck, then hop off
and steer onto it. From its north-west edge, jump toward the crossing line and catch
it while falling. The southern edge joins a broad slope returning to the entry yard;
slide down it to carry speed back into the ground loop.

The perch has no roof or healing/safe-zone effect. Enemies can navigate up its ramp,
and ranged attacks still use normal line-of-sight and collision rules. Missing a
landing returns you to the existing lower terrain rather than a lethal pit.
`-swarm-perch-test` checks an actual lift launch/landing, jump-to-line transfer,
downhill return slide, and enemy navigation to the deck.

Combat pacing pass (October 28): ordinary enemies arrive in packs of three, spaced
0.65 seconds apart, followed by a 4.5/4/3.5 second arrival break in waves 1/2/3.
Enemies already alive keep fighting during these breaks. Melee packs prefer a
shared side approach relative to your travel; spitters prefer a different angle
with clear line of sight. Placement keeps at least 14m of distance (12m horizontally),
avoids clustering, and still requires a complete walking path. The visible opening
pack, wave totals, cache guards, and sentinel remain as before.
`-swarm-pacing-test` checks actual placements from the entry yard, hoist perch, and
cache terrace, plus pack timing and navigation.


## Four-slot loadouts
The auto gun starts in slot one. Choose up to three additional weapons; picks are
permanent until retry. New weapons disappear from offers at four slots. Owned extra
weapons can reach rank five; capped upgrades disappear. Gun modifiers only affect
the gun. Power (+15% all weapon damage) and Recovery (0.6 HP/s per rank, max five)
are passives and consume no weapon slots.

- Boomerang: cyan spinning projectile, outbound then homing back toward your current
  position. Hits an enemy once per leg; cover turns it back or stops its return.
  Ranks increase damage and throw rate.
- Pulse field: green circular damage pulse centered on you every 1.4 seconds.
  Ranks increase damage and radius; walls block damage.
- Rockets: orange slow projectiles, direct damage plus nearby half-damage splash.
  Terrain impacts also burst. Ranks increase damage and blast radius.
- Piercing bow: purple fast, strong arrows, automatically aimed; pierce three enemies
  beyond the initial target at rank one. Ranks increase damage and piercing.

Choose with 1/2/3. T rerolls (three total per run, including cache rewards); X skips
without healing or saving the reward. Skips are unlimited. Queued XP and cache
rewards continue normally. Every accepted pick heals 20 HP. Retry resets loadout,
passives and rerolls. Combat and individual loadouts also work in co-op (see below).

`-swarm-loadout-test` exercises slot/cap eligibility, rerolls, skip/queued rewards,
reset and real projectile/pulse damage for every added weapon.


## Shared co-op combat
Join all players before starting the run. The host presses F or Start combat;
only the host can start, stop, or retry. Joining an active run is rejected with an
explanation. The host owns enemies, attacks, damage, pickups, cache, waves and
victory. Clients receive shared combat visuals and their personal build/UI state.
Movement remains owner-authoritative as above.

- Each player has separate health, four weapon slots, upgrades, skip/reroll budget,
  and random offers. Player weapons do not hurt teammates.
- Any living player can collect XP; every party member receives its full value.
- While any living player chooses an upgrade, the whole party and combat pause.
  Completing or skipping everyone's queued choices resumes the world. Disconnecting
  a choosing player removes their outstanding pause.
- Escape releases the mouse in co-op but does not pause the shared simulation.
- Downed players stop moving/firing and return at their own spawn with 50 HP at
  the next wave break. XP/cache rewards accumulated while down are offered after
  revival. If the entire party is down, the host can retry.
- Enemy health scales by +55% per extra connected player at spawn. Wave quotas
  remain unchanged for this first pacing pass.
- Host stop clears client visuals; leaving or host loss restores offline practice.
  No host migration or mid-run join/reconnect is implemented.

`CoopCombat.cs` keeps per-player simulation contexts and uses NGO named messages.
Reliable fragmented snapshots at 10 Hz carry shared primitive visuals and personal
state; clients interpolate transforms. This is prototype replication, intended for
local/LAN playtests, rather than a bandwidth-optimized entity protocol. Commands
are checked against sender identity, run and choice revision; stale/duplicate picks
cannot consume another reward. `-swarm-coop-role host/client` enables the actual
multi-process combat probe; `-swarm-coop-count 6` tests a six-player party.


## Readable combat HUD and weapon feedback
Combat uses a scaled HUD designed at 1600x900, with readable layouts checked at
1280x720, 1600x900 and 1920x1080. Large health and XP bars sit at bottom left;
four fixed weapon cards show names, ranks, empty slots and cooldown progress.
Wave progress stays at the top, party health at top left, and the optional cache
objective at top right. Upgrade cards distinguish new weapons, weapon ranks, gun
modifiers and passives; rerolls and skips have separate buttons. Escape opens the
combat menu; in co-op it releases the mouse without stopping the shared world.
The detailed movement/debug panels and floating landmark hint boxes stay in
movement practice, keeping the combat screen clear. Enemy labels still toggle
with N. Player labels avoid HUD panels and hide during modal choices.

Weapons now have consistent colored silhouettes, trails, launch/hit cues and icons:
cyan V-shaped boomerangs turn along a short curve and home toward the thrower's
current position (blue return trail); green pulse fields have a persistent range
outline and recharging arc (only your own persistent range is shown in co-op;
teammates' damage pulses remain visible); orange finned rockets show full-radius blast rings and
impact flashes; purple arrows have shafts, tips/fletching and piercing streaks.
Damage values, weapon slots, upgrades and base movement are unchanged. Boomerang
collision width better matches its visible shape. Terrain still blocks attacks.

Client snapshots preserve custom projectile shapes, trails and line colors.
Tinted proxy materials are cached and disposed with the combat instance.
`-swarm-weaponflow-test` checks return pursuit after the player repositions, pulse
range/charge following the player, and cleanup. The six-player co-op probe also
checks that all custom projectile types replicate and line colors remain valid.
`-swarm-hud-capture <folder>` is a development-only scripted graphical capture;
run with graphics enabled (without -batchmode/-nographics).


HUD previews: `Docs/Playtest/combat-1600.png`, `upgrades-1280.png`,
`upgrades-1600.png`, `upgrades-1920.png`, and `coop-1280.png`. These are actual
scripted playtest captures, not production art. Latest source checkpoint:
`Backups/Swarm-hud-weapons-2026-10-28.zip` (Assets, Packages, ProjectSettings, Docs,
README and progress notes; generated builds/caches excluded).

The HUD pass's complete standalone build is `Builds/Swarm-HUD-2026-10-28/Swarm.exe`.
The old standalone was open and locking the usual build folder during delivery;
use this fresh folder for the updated two-window playtest.


## Drill bowl greybox
The next POI's movement feature is now testable north of the mining basin. Pass
under/past the ore conveyor, through the opening in the north wall, and follow the
wide temporary ramp to the turquoise rim. This access ramp is a placeholder for
the later canyon routes and broken bridge, not the finished journey.

The bowl has a 24m inner radius, an 8m drop to the center, and an 8m-wide safe rim.
Run onto the slope, hold slide, and carry speed through the center toward the far
side. Walking back up is also possible; four outer ramps recover from the apron.
There is no gathering gate or countdown to enter.

The drill stays raised 4.5s, warns in amber for 1.8s, plunges over 0.65s, holds down
1.2s, then rises over 2s. A lit footprint, beacon, and nearby HUD status communicate
the phase. Its moving cone uses a swept strike volume: 30 damage to players and
100 to enemies, once per actor per plunge, with outward/upward player knockback.
Enemy survivors are displaced to nearby navigable ground. Drill enemy kills drop
ordinary XP and count toward the current combat wave.

The hazard also works in movement practice (100 practice HP; knockout returns to
spawn with practice health restored). In combat it uses each player's actual HP.
The host runs co-op hit detection and sends cycle state and numbered impulses;
clients apply each knockback once. Upgrade pauses freeze the drill with combat.
Base slope/slide behavior stays unchanged; a short airborne window lets external
knockback launch correctly off grounded terrain.

`DrillBowlSetup.InstallAndBuild` installs the greybox into the canyon scene. Full
canyon regeneration preserves it. Runtime navigation bounds include the new area.
`-swarm-drill-test` checks actual rim-to-rim slide motion, safe rim and access paths,
player damage/launch, one-hit protection and enemy kills. `-swarm-drillcoop-role
host/client` checks practice and combat hits, replicated impulse/cycle, and upgrade
pause/resume. `-swarm-drill-capture <folder>` captures warning/down views with graphics.

Latest drill-bowl build: `Builds/Swarm-DrillBowl/Swarm.exe`.
After refreshing Unity, stop Play and use **Swarm > Open Canyon Playground** to
reload the saved extension, then Play and walk north. Previews are in
`Docs/Playtest/drill-warning.png` and `drill-down.png`. Source checkpoint:
`Backups/Swarm-drill-bowl-2026-10-29.zip`.

Drill size tuning: the head is now8.8m across (4.4m radius); warning radius5.1m.
Its height, damage and cycle timing are unchanged.


## Timed slide hops
At running speed, start sliding and jump after a brief beat (0.08-0.28s after the
slide starts). A successful hop adds 7% horizontal speed near the normal11m/s run
speed. You must be moving at least80% of run speed. The bonus diminishes toward22m/s;
terrain-earned speed above that is preserved rather than clamped. Only a new landing
rearms the hop; airborne crouch/jump presses cannot stack bonuses. Ordinary slide
jumps outside the timing window still carry speed. A short SLIDE HOP readout confirms
the boost in practice and combat. `-swarm-slidehop-test` checks timing, actual landing
chains, no air stacking, expired/standing jumps and high-speed behavior.

Latest timed-hop standalone: `Builds/Swarm-SlideHop/Swarm.exe`.

### Drilling station expansion
The north maintenance deck has cover, bounce surfaces and three ramp approaches.
Follow the marked west rim for the safe route, or cross the bowl beneath the drill
for the direct route. The west staging platform offers room to regroup freely.
Latest standalone: `Builds/Swarm-Station/Swarm.exe`.

### Station movement loop
East bowl escape zip: E to grab, face along the line for direction, Space to hop,
Ctrl/C to drop. A cyan-marked wall beside the lower end supports bounce-to-grab.
The west deck slope returns to the bowl for another crossing.
Latest standalone: `Builds/Swarm-StationLoop/Swarm.exe`.

### Station supply overlook
An optional second supply cache sits off the deck's east side. Use the walking
ramp or jump the gap. In combat, press E nearby and clear four guards in 35 seconds
for an extra upgrade choice for everyone. Each POI's cache can reward once per run;
only one cache challenge runs at a time. Physical signs mark the station routes.
Latest standalone: `Builds/Swarm-StationCache/Swarm.exe`.

### Canyon journey
The basin north exit now leads through a longer canyon to the relocated drilling
station. The lower route takes about47 seconds in the controlled walking test.
Use the downhill run for momentum, take the upper broken bridge and zip, or walk
the dry gully below. Missed bridge jumps land on the lower route. Canyon bends
hide the station until the approach; all routes are open from the start.
Reload with Stop Play > Swarm > Open Canyon Playground > Play.
Latest standalone: `Builds/Swarm-CanyonJourney/Swarm.exe`.

### More canyon movement terrain
A one-sided quarter pipe sits on the right of the first downhill, followed later
by a banked turn, three staggered ledges, a zip shortcut onto the ledges, and a rock
arch. Carry speed into the quarter pipe while sliding and let the curve redirect
you; gravity rolls you back down. The walking trail remains continuous.
Steep support physics now follow the capsule's actual contact with curved slopes.
Latest standalone: `Builds/Swarm-RouteFeatures/Swarm.exe`.

### Route encounters and shelter
Press F to start combat, then explore north. Bridge scouts and station outriders
warn before approaching, once per run for the whole party. Regular wave arrivals
pause while a route fight is active. Enemies drop normal shared XP. A marked trail
shelter after the bridge provides cover and regrouping space. All routes stay open.
The sentinel now spawns near the party's current area.
Latest standalone: `Builds/Swarm-RouteEncounters/Swarm.exe`.

### Deck download terminal
During combat, enter the maintenance deck circle and press E. One player completes
the download in40 seconds; each extra living player inside adds10% speed (max50%).
Jumping within the circle counts. With nobody inside, progress has a2-second grace
period then loses2 percentage points per second. Reenter to resume. Enemies approach
while it is active. At100%, everyone gets an independent upgrade choice, including
players outside; downed players keep the reward for revival. Once per run.
Latest standalone: `Builds/Swarm-DownloadZone/Swarm.exe`.

### Upgrade rarity
Upgrade cards now roll Common, Uncommon, Rare, Epic or Legendary rarity independently
of weapon level. Higher tiers provide stronger stat bonuses; Epic trades some raw
stat strength for critical hits, and Legendary adds a repeatable shockwave perk.
Cards show exact bonuses and whether a perk affects all weapons. Rarity colors:
gray, green, blue, purple, gold. Rerolls and skips use the existing controls.
Initial tier chances:50/28/15/6/1%. First Epic grants20% critical chance (later+5%,
cap50%). First Legendary triggers a3m shockwave every6 direct hits; later ranks
reduce that to a minimum3 hits. Secondary shock damage cannot chain itself.
Weapon levels and the four-slot limit are unchanged. Stop Play and let Unity compile.
Latest standalone: `Builds/Swarm-UpgradeRarity/Swarm.exe`.

### Color-coded download tiers
Terminals guarantee a matching-tier upgrade choice for everyone, including outside
and downed teammates. Rerolling preserves the guaranteed tier. Solo durations:
Common(gray)20s; Uncommon(green)30s; Rare(blue)40s; Epic(purple)55s; Legendary(gold)75s.
Each extra living player inside adds10% speed, capped50%. Empty zones retain the
2s grace and2 percentage-point/sec loss. Each terminal rewards once per run, with
one download active at a time. Find terminals in the basin yard, trail shelter,
maintenance deck, upper basin terrace and west station platform respectively.
Reload via Stop Play > Swarm > Open Canyon Playground > Play.
Latest standalone: `Builds/Swarm-TerminalTiers/Swarm.exe`.

### Shared money and paid crates
Enemies give5 party money on defeat; the sentinel gives40. Press E near gold salvage
containers to break them for10. Blue Rare crates cost30 and purple Epic crates75;
prices/rewards show before purchase. Press E nearby to buy a matching-tier upgrade
choice for everyone, including distant or downed teammates. Crates and salvage work
once per run. The shared wallet resets on a new run. Free guard caches remain.
Download solo times are now10/15/20/30/40 seconds from Common through Legendary.
Reload via Stop Play > Swarm > Open Canyon Playground > Play.
Latest standalone: `Builds/Swarm-SharedMoney/Swarm.exe`.


### Connected canyon loop — November 2
Latest standalone: `Builds/Swarm-CanyonLoop/Swarm.exe`.
The new pumping yard sits west of the existing canyon. Reach it through the basin's
west ground exit, the drill station's west apron, or the trail shelter's central
shortcut. Gold stripes mark outer routes; teal marks the middle cut. The yard has
an elevated relay deck, walking ramps/return slide, bounce faces and a transfer zip.
All connections work in both directions without upgrades. This pass tests map
travel and connectivity; yard-specific objectives and boss activation are future work.
Stop Play and reopen CanyonPlayground before testing the updated saved scene.
