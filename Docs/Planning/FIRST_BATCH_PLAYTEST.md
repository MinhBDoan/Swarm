# First parallel batch — playtest

November 15, 2026. Built with Unity 2022.3.23f1.

## Run it

Launch `Builds/Swarm-TerrainTimerPause/Swarm.exe`, or open `Assets/Scenes/CanyonPlayground.unity` in Unity and enter Play. Click the game view. Movement practice works immediately; **F** starts combat. Join before starting for co-op; the host starts the run.

## What changed

- Three broad, tapered terrain folds in the central flats: south crossing, middle junction, and north shelf approach. They support diagonal carving, ordinary walking and downhill slide momentum. Offset bounce/landmark pieces and a walking shelf approach help connect existing features. The footprint is unchanged.
- Each fold has 384 triangles; all three total 1,152 triangles and three mesh colliders. Including the shelf ramp, net additional colliders are four. This is a geometry accounting result, not a hardware performance benchmark.
- A **10:00 canyon countdown** appears in the persistent and classic HUD. It is one clock for this entire area, including its POIs. Later maps remain unimplemented.
- After expiry, overtime increases wave size, frequency, health and damage. Existing ordinary enemies also strengthen while preserving their remaining-health fraction. Normal-wave admission has a bounded live cap up to 128; objective guards/boss may add entities beyond that admission limit. Strength keeps increasing beyond the body cap.
- Boss activation leaves the timer and ordinary wave pressure running, including overtime. The summoned boss keeps its spawn max health rather than continuously gaining health; its attacks and surrounding swarm still provide pressure.
- **Escape** opens/closes your menu and pauses the party, timer and drill. Resume closes only your menu: another member's menu or unresolved upgrade still keeps the world paused. Explicit menu intent avoids confusing an upgrade's released cursor with a menu request.

## Suggested player test

1. In movement practice, take the basin's west exit into the flats. Travel north through the rolling south crossing and middle junction, then try the northern fold and shelf approach. Try walking, diagonal slides, downhill launches and recovery after a missed line.
2. Judge whether the new slopes create useful movement choices, whether the middle still has enough swarm space, and which stretches still feel empty or too long. This is one map pass, not the completed canyon milestone.
3. Start combat and check that the area countdown remains visible. Activate the Sentinel when ready: the clock should keep running.
4. In co-op, open menus in both instances, then close only one. The game should stay paused until both close. Repeat while an upgrade is pending.
5. A normal run reaches overtime at ten active gameplay minutes. Automated probes exercise expiry immediately for development validation; there is no new normal-play skip button.

## Validation completed

Windows development build succeeded. Automated standalone checks passed:

| Scenario | Evidence |
|---|---|
| New slopes | Real controller walks over all three; no-input downhill slides accelerate to 16.7 / 14.8 / 13.8 m/s in the controlled scenarios; navmesh paths reach all crests. |
| Area timer | Initial/pre-expiry/expiry states, 16 enemies spawned at expiry, later pressure growth, existing-enemy strengthening, boss-state wave spawning, bounded admission, upgrade pause/resume, retry reset. |
| Existing behavior | Uphill rollback and flat/jump carry; all optional weapons/loadout choices; Sentinel's four moves and dodge cases; central-area enemy pursuit and reachable spawning. |
| Group menu pause | Host/client overlapping menus, timer and drill frozen, movement blocked, menu/upgrade overlap, resume, paused-client disconnect cleanup and retry reset. |
| Co-op boss | Host/client console range, votes, preparation, fight, and victory replication. |
| Shared combat | Two instances, then six local instances: shared XP, independent choices, replicated visuals, party resume, down/revive and cleanup all passed. |
| Visual review | Fresh before/after terrain images; countdown/overtime and pause HUD screenshots. A label-over-menu issue found during review was corrected. |

Compact machine-readable results: [validation-results.json](../Playtest/FirstBatch/validation-results.json). Full runtime logs remain under the local `AppData/Local/SwarmFirstBatchTests` folder. Timer probes advance the simulation clock directly, and terrain probes step the controller deterministically; these are functional checks, not balance/performance measurements.

## Limits and remaining work

- Six local processes do not replace six human players or internet-latency testing.
- The developer laptop is high-end; no minimum-PC claim or full GPU/CPU benchmark is made.
- Overtime values are an initial tuning pass. Live caps control admission, not a guarantee of steady frame time. Test how long real builds can survive.
- Existing minute-boundary party revival now also occurs during a boss fight because ordinary waves continue. This remains provisional and needs design review.
- Chest tier randomization and purchase-driven price inflation are not part of this batch; existing fixed crates remain.
- Map compression, more purposeful spaces and the third-area setting remain open. Charger production stays paused at v4.

## Rebuild and probes

`Swarm.Editor.ProductionBatchBuild.BuildIntegrated` regenerates the western loop/central region, saves the scene, captures updated images, and builds this playtest. Use `BuildCurrent` for code-only rebuilds without regenerating the scene. `CaptureBaseline` captures the saved scene before map edits.

Targeted development arguments: `-swarm-central-terrain-test`, `-swarm-area-timer-test`, and paired `-swarm-grouppause-role host` / `client`. These opt-in probes run assertions and quit; omit them for normal play.
