# Swarm — product requirements

**Status:** review draft v0.9 / November 18, 2026

## Vision

**Confirmed:** a PC sci-fi survival adventure for one to six players. Players create absurd combat builds while exploring connected spaces together. Movement has a skateboard-like sense of momentum, terrain expression, and player-discovered tricks. People and alien/monster designs belong in the setting; the final player roster and story are open.

Megabonk and League of Legends Swarm inform the survival/build loop. Apex informs expressive movement and directional zipline travel. These are references for particular qualities, not requirements to reproduce their systems exactly.

**Vision review:** the player confirms that the movement/co-op/adventure direction matches so far. The implementation still needs tuning to fully deliver it; confirmation of direction is not acceptance of current movement, map flow, or balance as finished.

## Experience pillars

| ID | Status | Required experience |
|---|---|---|
| P1 | Confirmed | Movement is fun before obtaining any items. Skilled players can push its boundaries. |
| P2 | Confirmed | Friends survive together while developing individual, sometimes absurd builds. |
| P3 | Confirmed | Traversal feels like a shared adventure through distinct places, with routes worth exploring. |
| P4 | Confirmed | Threats grow over time and reward dodging, positioning, movement, and build decisions. |
| P5 | Proposed | Combat and rewards remain readable when multiple players, weapons, and enemies overlap. |

## Core loop and progression

**Confirmed direction:** explore and traverse → fight enemy waves/packs → collect XP and money → select upgrades or optional reward encounters → challenge a boss → advance to another map with the build retained within that run.

**Confirmed run structure:** three areas per run, each with its own provisional **10-minute countdown**. The current canyon is area one; its basin, drilling station, pumping yard, and central flats are POIs within that area, not separate countdown areas. A cyberpunk city is the intended second setting; the third area's setting is undecided. Map-to-map transitions and build carryover are requirements for the broader game, not existing functionality or mandatory scope for the next milestone.

Three areas provide up to 30 minutes of pre-overtime countdown time if every countdown is fully used. This is not a fixed 30-minute run length: early boss activation, boss duration, overtime, and pauses can change actual session length.

**Confirmed pacing direction (November 14 review):** each of the three areas uses a fresh countdown, initially **10 minutes** per area as a provisional tuning value. Waves become stronger and more pressuring as time passes. At **00:00**, enter an overwhelming overtime phase. Enemy strength and wave numbers keep increasing as overtime continues, with the intent to eventually overwhelm a party that delays too long. Overtime is not a fixed-strength wave or a sustainable farming phase. Huge numbers describe the gameplay pressure; the implementation must preserve frame-time and network budgets rather than create unlimited live entities. Expiry does not immediately end the run or automatically start the boss. Players are encouraged to activate the boss before the countdown expires rather than farm indefinitely.

**Confirmed boss timing:** activating the boss does not stop or reset the area countdown, freeze pressure, or end overtime. Waves keep escalating throughout the fight. If the countdown reaches zero during the boss fight, overtime starts; if the boss is activated after expiry, overtime continues escalating. Whole-party upgrade selection still pauses gameplay and the active timer.

**Confirmed menu behavior:** opening the ordinary game/Escape menu pauses gameplay and the active area timer for the whole group. Shop browsing is an explicit exception: it affects only the browsing player and does not pause combat or the area timer. Close/resume only when no player still has a pause-causing menu or unresolved upgrade choice. This overrides the current co-op Escape behavior, which only releases the cursor. The specific menus covered and host/disconnect edge cases must be validated during implementation.

**Confirmed boss duration:** after summoning, a boss remains an encounter until it is killed or the run ends through defeat/exit. There is no separate boss-fight time limit; the area clock and escalating waves still continue except during group pauses.

**Open:** the exact point after boss defeat when the area clock ends/transitions, total elapsed run length including bosses/overtime/pauses, and persistent progression between runs. The 10-minute value is an area exploration deadline, not a boss time limit. Whole-party upgrade selection pauses the active countdown together with gameplay.

## Functional requirements

| ID | Status | Requirement and observable acceptance |
|---|---|---|
| MOV-01 | Confirmed | Run, jump, slide, wall bounce, and zipline traversal are available without items. A fresh character can use the intended canyon routes. |
| MOV-02 | Confirmed | Slides and jumps carry momentum. Uphill sliding loses speed and can roll backward; downhill sliding gains speed. Held input must not provide endless uphill sliding. |
| MOV-03 | Confirmed | Mistimed tricks generally lose some momentum and remain recoverable. Collisions remove velocity naturally, including losing forward speed in a direct wall impact. |
| MOV-04 | Confirmed | Ordinary collision terrain supports tricks when its geometry and movement rules permit them. No special wall-bounce tagging is required. Designers may suggest movement lines. |
| MOV-05 | Confirmed | Zipline entry direction follows facing/look direction; vertical lines support up/down travel. Lines provide a baseline speed and support momentum-carrying exits. Crouch drops the player off. |
| MOV-06 | Confirmed | A well-timed slide hop provides a modest speed advantage. It should be a worthwhile skill without making ordinary movement unusable. Exact tuning remains adjustable. |
| COOP-01 | Confirmed | Support solo and parties of up to six total players. Players can develop distinct loadouts while sharing the adventure. |
| COOP-02 | Confirmed | XP is shared; an inefficient build does not reduce that player's XP entitlement. Shared XP level-ups are synchronized group upgrade events, while each player chooses their own upgrade. |
| COOP-03 | Confirmed | Shared-XP level-ups, chests, and terminals reward the whole party. Pause gameplay and the active area timer for selection, then resume after all eligible players finish or skip their queued choices; disconnects must not strand the pause. Personal shop purchases are the explicit individual-upgrade exception: they grant only the buyer an upgrade with no party pause. Players select their own upgrades and maintain independent builds. |
| COOP-04 | Confirmed | Opening the ordinary game/Escape menu pauses the game and area timer for the entire party. Shop browsing is excluded. Overlapping menus/upgrade selections must not resume prematurely; disconnecting a paused player must not leave the party permanently paused. |
| BUILD-01 | Confirmed | Four weapon slots create meaningful choices. Include the auto gun, boomerang, surrounding area pulse, explosive rockets, and strong piercing bow as the initial set. |
| BUILD-02 | Confirmed | Support skipping unwanted offers and rarity-based upgrades. Rarity can improve stats or add effects; exact distributions and effect rules remain tuning decisions. |
| COMBAT-01 | Confirmed | Automatic targeting and firing support movement-focused combat. Enemy roles should be recognizable through appearance and behavior. |
| COMBAT-02 | Confirmed | Swarm pressure and enemy strength increase with elapsed time. Difficulty must not directly scale to the players' current weapons or damage output. Party-size tuning may be used. |
| COMBAT-03 | Confirmed | Elite versions periodically join ordinary waves, retain their archetype, and have modestly higher health and attack damage. Ordinary elites occasionally drop a free chest. |
| COMBAT-04 | Confirmed; initial value provisional | Each of the three run areas starts its own countdown at 10:00; POIs within an area share that countdown. The current canyon is the only implemented area. Display the current area and its remaining time. Waves intensify with elapsed time. At 00:00, enter ongoing overwhelming overtime waves whose strength and numbers continue increasing to eventually defeat players who delay too long. Do not treat expiry as an automatic loss or boss summon. Validate the transition and continued arrivals in solo and co-op. |
| COMBAT-03 | Confirmed | Chargers are relatively tanky and apply directional knockback on charge contact. The Sentinel cycles through several readable attacks, including a distance-limited charge followed by a ground pound and knockback. |
| WORLD-01 | Confirmed | Connected POIs have distinct activities and movement opportunities. Travel should feel substantial without becoming an empty corridor. |
| WORLD-02 | Confirmed | The canyon has an outer loop, cross-connections, and a large accessible central area with only a slight dip. Players can cross the middle rather than always circling the perimeter. |
| WORLD-03 | Confirmed | Terrain includes ramps/quarter pipes, bounce surfaces, ziplines, alternate lines, and recovery routes. The drill damages players and enemies. |
| WORLD-04 | Confirmed | Close accidental map holes. Preserve the deliberately accepted quarter-pipe fall-off as a skill-check exception; its exact location and recovery behavior need documenting. |
| OBJ-01 | Confirmed | Download terminals activate by entering the zone. One occupant progresses the download; additional occupants modestly accelerate it. Progress decays slowly when empty. |
| OBJ-02 | Confirmed | Terminals have color-coded reward tiers and tier-dependent times. They call enemy packs while occupied, making the reward a risk decision. |
| OBJ-03 | Confirmed | Optional activatable challenge spots spawn many elites. Killing the complete challenge squad earns one free chest whose upgrade choices reward the whole party and pause gameplay until resolved. |
| OBJ-03 | Confirmed; pricing details open | Keep shared party money and shared XP. Chests/crates and terminals grant upgrade rewards to the whole group; each player makes their own selection. Paid crates use the shared wallet, and higher prices can offer higher tiers. Chest tiers are randomized. Each tier has a fixed starting price, and chest prices rise as the party buys more chests. Tier probabilities, starting values, growth formula, affected tiers, and purchase-counter reset scope remain open. |
| BOSS-01 | Confirmed | Include an interactable boss console with a clear activation area and understandable feedback. Players can choose to activate the boss before the 10-minute deadline; overtime pressure makes delaying that choice dangerous. The countdown and wave escalation continue during the boss fight, including overtime if reached. Activating the boss late does not cancel overtime. There is no separate boss timer: fight until the boss is killed or the run ends. |
| WORLD-05 | Confirmed | Keep terrain/routes/POIs fixed while randomizing chest, terminal, boss-console/arena and shop locations each run. Choose reachable authored candidates with room for the activity. The host chooses one shared layout; it stays fixed during the run and is identical for all party members. |
| SHOP-01 | Confirmed; prices provisional | Three NPC shops per area. Each has one rarity tier and three personal random offers within that tier. Each player visits independently, buys one upgrade per shop, then that shop closes for that player. Offers remain stable on reopening. Purchasing uses shared money but rewards only the buyer; neither browsing nor buying pauses the fight. Confirmed tiers: Uncommon, Rare, Epic, paid from shared money. Initial prices 15/30/75 remain provisional for balance feedback. |
| HUD-01 | Confirmed | During combat, persistently show health, XP/level, money, equipped weapons and levels, and relevant objective/boss information at a readable scale. |

## Prototype policies awaiting explicit long-term approval

These exist in the prototype but should not be mistaken for permanent requirements:

- Three rerolls per run, unlimited skips, and weapon choices locked until retry.
- Fixed scene crate locations/prices are prototype behavior. The confirmed replacement is randomized chest tiers, tier-based starting prices, and prices that rise with party purchases. Spawn placement randomization and the price escalation formula still need definition.
- Downed-player revival at wave breaks; no mid-run joining or host migration.
- Boss voting and preparation countdown details remain provisional. Area countdown and escalating waves continuing during the boss fight are confirmed.

## Confirmed next milestone: flesh out the canyon

**Confirmed priority:** flesh out the existing canyon map. It has too many open areas without enough activity or terrain interest, and its total size may be excessive. Assess its footprint, route lengths, empty stretches, and POI density before choosing to retain or shrink parts. Do not assume that filling every empty space with props is the answer.

Retain useful combat space and movement lines while adding purposeful cover, elevation, traversal links, encounters/objective placement, and landmarks where playtesting shows a need. Recommend targeted compression or removal of low-value stretches if that improves flow. Specific layout changes and the final size require playtest review.

**Confirmed technical direction:** avoid a PC-intensive game. Prefer readable, economical geometry/materials, purposeful scene density, and measured enemy/VFX budgets. The minimum PC, target resolution/frame rate, and quantitative budgets are undecided; do not assume a high-end development laptop represents the audience.

**Proposed deferrals during this map milestone:** second/third areas, additional weapon families/bosses, full art production, and further Charger polish. Timer/economy/menu changes remain confirmed design requirements in the backlog, but are not automatically the next implementation priority. Existing systems must continue working while map changes are integrated.

## Map milestone success criteria — proposal for review

- A fresh character can traverse between major canyon areas along an ordinary route; skilled routes offer useful alternatives and recoverable misses.
- A player can find enemies and meaningful choices in the central area and on routes without repeated empty travel.
- Each retained major space has a deliberate purpose; necessary open combat areas remain usable rather than being cluttered for density.
- Compare travel time, meaningful activity, and navigation in a baseline route before/after edits. Recommend whether the footprint should shrink based on that review.

The broader game must also satisfy these confirmed directions as their systems are implemented:

- Later encounters show increased pressure at a fixed test loadout; build strength is never an input to difficulty scaling.
- The countdown visibly reaches 00:00 and triggers sustained overwhelming waves. Later overtime checkpoints show both stronger enemies and increased wave numbers; overtime must not plateau into safe indefinite farming. The party can choose to start the boss before expiry. Test expiry during a boss fight and late boss activation: both must retain escalating overtime pressure. Group upgrade selection pauses both combat and the area clock.
- Every Sentinel attack has a recognizable warning and a practical evade opportunity.
- Shared XP and independent upgrade choices work correctly. Money uses the shared party wallet. Chests and terminals grant each party member an independent upgrade choice. Every upgrade award is party-wide and uses a shared selection pause. Spending and boss activation happen once.
- The agreed HUD information remains visible during normal combat and at reviewed window sizes.
- A representative solo and full-party scenario meets a performance budget that we will establish by measurement.

These criteria require player testing as well as automated checks. Total run duration, target hardware, resolution, frame rate, and enemy-density budgets are still open; no benchmark has been promised.

## Priority review questions

1. Does the movement/co-op/adventure balance above match your vision?
2. Which empty canyon spaces should be filled, reworked, compressed, or removed after a full-route review? Map fleshing-out is the confirmed next milestone.
3. Exactly when after boss defeat does the area timer end and the transition begin? Menus and upgrade selections pause the whole party and clock; boss combat otherwise leaves both running.
4. How should queued group rewards interact with downed players and revival? XP/chests/terminals are party-wide; shops grant personal upgrades without pausing. Independent choices and group-event pauses remain confirmed.
5. What are chest tier probabilities, tier starting prices, and the purchase-driven price increase formula? Does each purchase raise all tiers or only that tier, and does the purchase counter reset each area or each run? Randomized tiers, tier-based starting prices, shared spending, and group rewards are confirmed.
6. What minimum PC, resolution, and frame-rate target should we support? Boss duration is until killed, and the third-area setting stays undecided.

## November 18 elite encounter tuning

Implemented starting values, subject to playtesting: elites have 1.5× health and 1.25× attack damage, with gold collars and labels; periodic packs begin at 45 seconds and recur roughly every 35 active seconds when capacity allows. Ordinary elite kills have a 20% chance of a free Rare party chest. Three challenge spots use the shared randomized run layout. One challenge runs at a time; each spot is usable once per run, sending 10 elites solo plus two per extra player (maximum 20), in paced packs. Clearing the full squad earns one guaranteed Epic free party chest. Challenge guards do not roll additional random chests. These numbers, tiers, pacing, and visual markers are provisional implementation choices rather than finalized balance.
