# Proposed milestone and backlog

**Status:** proposal for review · November 14, 2026. No assignments have started and no additional agents are running.

## M1 — flesh out and right-size the canyon

**Confirmed next priority:** flesh out the existing canyon map. Many open areas lack purpose or activity; the footprint may be too large. The first deliverable is a route/space audit and a concrete layout proposal, followed by an integrated playtest of the selected map improvements. Do not expand the map by default.

Preserve combat room, recoverable movement lines, distinct POIs, and navigation. Improve terrain/activity density purposefully; compress or remove low-value stretches if evidence supports it. Props should serve movement, combat, navigation, or the setting. Keep geometry, materials, collision, and enemy access economical so the result is not PC-intensive.

The full run remains three areas with a provisional 10-minute clock each. This milestone covers the canyon, area one; its POIs share one clock. Cyberpunk city is intended second, third area undecided. Completing the timer/economy/menu redesign, finishing the Charger, and implementing later maps are follow-on work unless explicitly added to this milestone. The earlier proposal to finish every canyon-run system before calling the milestone complete is superseded.

## Work packages

First source audit complete: see [Canyon map audit](CANYON_MAP_AUDIT.md). It recommends a focused central-flats pass before deciding on targeted outer-route compression. This is not a fresh gameplay/performance validation or a map implementation.

Map work (M1-02) is the immediate implementation priority after review/baseline. Other rows are the broader follow-on backlog, not all exit requirements of this map milestone. All estimates and exact performance targets remain open. The rows below identify deliverables rather than authorizing simultaneous edits of shared files.

| ID / priority | Suggested owner | Deliverable and completion criteria | Dependencies / validation |
|---|---|---|---|
| M1-00 / first | Coordinator + player | Review the PRD, settle milestone scope, record unresolved questions without inventing answers. | User review. |
| M1-01 / first | Integration | Establish source control with sensible exclusions; inventory large art sources; capture a known build and baseline playtest. Confirm a restorable source checkpoint. | Review existing `.gitignore` before initial commit; choose large-file policy before adding GLBs/Blends. No remote publishing implied. |
| M1-02 / high | World | Audit basin, station, yard, central flats, and routes for empty space, travel time, purpose, sightlines, movement, and enemy access. Propose fill/rework/compress/remove choices; then implement the selected map changes. Close accidental holes and document the intentional quarter-pipe exception. | Record baseline route times and purposeful combat/traversal spaces. Judge whether the map is too large; do not assume more terrain or more props is needed. Validate ordinary routes, optional tricks, recovery, enemy paths, frame-time/memory impact, and player review after edits. |
| M1-03 / high | Gameplay | Implement a provisional 10-minute canyon countdown, escalating waves, and sustained overwhelming overtime waves at zero that keep increasing in both strength and numbers to eventually overwhelm delaying players. Tune time/party-based pressure against fixed representative weak/medium/strong builds. | Whole-party XP and group-reward upgrades pause the countdown and gameplay. Boss activation leaves the countdown and escalation running; expiry during the boss fight starts overtime, and late activation does not end it. Menus and upgrades pause the entire party and timer. Agree boss-defeat clock handling before dependent implementation. Test just before/at/after expiry, multiple later overtime checkpoints for continued strength/count growth, retry reset, and host/client clock agreement. Log counts and survival; no loadout-driven scaling. |
| M1-04 / high | Gameplay | Refine Sentinel warnings, counterplay, damage and recovery windows across all four moves. Console activation must clearly communicate discovery, readiness, and start/failure. | Early boss activation before expiry is confirmed. Keep countdown and escalating waves active throughout the boss fight; late activation retains overtime. Test expiry during the fight and late activation. Verify each attack and knockback in solo and host/client. No separate boss time limit: encounter lasts until killed or run ends. Player judges readability. |
| M1-05 / high | World + integration | Review existing caches, terminals, salvage, and paid crates as a route/reward circuit. Implement randomized chest tiers with fixed tier starting prices and purchase-driven price increases; fix inaccessible placements, duplicate spending/rewards, and unclear risk. | Retain shared money and shared XP. Chests and terminals reward the whole party; each member chooses independently. Personal non-pausing shop purchases are the explicit individual-upgrade exception. Agree tier probabilities, price growth, which tiers each purchase affects, and counter reset scope. Host must compute the current price, deduct once, increment the purchase count once, and replicate the next prices; validate simultaneous purchases. Preserve automatic terminal entry and enemy calls. Verify simultaneous interactions and rewards after down/revive under the current prototype policy. XP/chest/terminal upgrades pause everyone and the area timer until eligible choices resolve. Shops instead offer one personal purchase per player without a world pause. Add new activities only if the audit identifies a need. |
| M1-06 / medium | Integration/UI | Polish persistent health, XP/level, shared party money, the 10-minute countdown/overtime warning, weapon names/ranks, and objective warnings at agreed window sizes. | Capture normal combat, upgrade, boss, and six-player busy scenes. Player reviews scale/readability. Coordinate ownership of shared combat files. |
| M1-07 / medium | Asset | Refine Charger head/neck and limb deformation, create minimal movement/charge/recovery animations, optimize materials, export a Unity-ready test. | Resume v4. Inspect full pose range; count final triangles/materials/textures; check silhouette and artifacts. Do not hide deformation by shrinking the test angle. |
| M1-08 / after asset | Integration | Attach approved Charger art to the existing gameplay entity without changing collision, damage, telegraphs, or authority behavior. Replicate visible attack states. | M1-07. Solo and client must show movement, windup, rush, recovery, hit/death; retry cleans up. Reuse gameplay checks. |
| M1-09 / ongoing | Integration | Measure frame time, spikes, memory, and network load at representative enemy/party densities. Set a target with the player, then fix measured bottlenecks. | Baseline first; avoid promising a target based only on the developer laptop. Distinguish local simulated instances from real remote clients. |
| M1-10 / exit | Integration + player | Package a named build, test sheet, known-issues list, and updated project status. | Map/navigation/movement checks and affected-system regressions pass; fresh solo and host/client route sessions pass. Compare baseline and revised route/space density, obtain player feedback on map size and interest, and record performance measurements. Full-party validation coverage and any unavailable six-human testing are stated explicitly. |

## Suggested parallel batches

After review and baseline, prioritize the world audit/layout proposal and integration/performance checks. Schedule map edits by region with shared-scene ownership. Asset and gameplay expansion are optional parallel work only when explicitly scheduled and independent of the map work.

The Charger pipeline remains paused and is not a map-milestone blocker. Keep its current placeholder while improving layout unless asset work is separately scheduled.

## Agent working agreement — proposed

Start with three specialists: gameplay; world/assets; integration/testing. The coordinator controls priorities and integration. Do not create separate permanent agents for every feature by default.

1. Give each agent one bounded work package, concrete acceptance criteria, baseline revision, owned paths, and explicit shared-file restrictions.
2. Use isolated branches/worktrees after Git exists. Separate Unity project copies avoid sharing `Library`, `Temp`, and editor locks. Each worker must know how its build is validated.
3. Only one owner edits a shared scene, prefab, project setting, or file at a time. `CombatPlayground` partials still share runtime state; different filenames do not guarantee independent changes.
4. Plan scene-builder changes together: rebuilding a scene can overwrite hand edits. Integrate and verify generated scene changes under one owner.
5. Agents report actual edits, evidence, remaining risks, and unverified criteria. A script completing is not proof of gameplay correctness.
6. Integration reviews the change, applies it to the common baseline, and runs checks appropriate to the affected behavior before the next merge.
7. Stop feature expansion when a regression threatens the milestone. Resolve scope changes explicitly with the player.

## Assignment template

```text
Task ID and desired player-facing outcome:
Baseline revision / build:
Relevant requirement IDs:
Owned files and permitted edits:
Shared files requiring coordination:
Inputs and dependencies:
Explicit exclusions:
Acceptance criteria:
Validation scenarios and expected evidence:
Deliverables / integration notes:
Open decisions that require player input:
```

## Playtest rhythm

Agree a batch → implement and verify → integrate → deliver one focused playtest → address findings. Ask the player to judge movement feel, navigation, difficulty, reward value, and visual clarity, rather than perform repetitive setup or discover whether basic systems compile.

Track cycle time to a playable build, regressions found after integration, and player feedback. More agents are worthwhile only if they improve those outcomes.
