# Random encounters and personal shops

November 17, 2026.

Build: `Builds/Swarm-RandomEncountersShops/Swarm.exe`. The canyon remains the only implemented area.

## Shared randomized layout

Terrain, routes and POIs remain fixed. Each new run chooses a shared layout for eight terminals, seven chests, three shops, and one boss console/arena. The extra three terminal slots prefer southern, middle, and northern central-flats locations. The first five slots retain their existing rarity identities, but rarity is no longer inferred from a terminal's slot number.

The host chooses positions and replicates the exact layout to clients. New retries generate a new layout; objects do not move during an active run. Candidates come from authored areas, with ground, clearance, navigation, activity separation and cache-distance checks. Boss sites are validated as arenas rather than arbitrary points. Placement randomness can repeat an individual site; it does not guarantee that every object moves on every retry.

Terminal props/rings and boss console/sign/ring move with the layout. Chest/NPC visuals use the existing host-owned combat replication. Practice mode restores the authored layout.

## Shops

- Three lightweight NPC shops. Initial prototype tiers/prices: **Uncommon 15, Rare 30, Epic 75** party money. The player confirmed these tiers and shared payment; price values remain initial tuning.
- Approach and press **E**. Each player receives their own random catalog, drawn from eligible upgrades at that shop's tier. It is rolled once, so closing/reopening does not grant free rerolls.
- Buy one upgrade, using **1/2/3** or a button. Only the buyer gains it; that shop becomes purchased for that player for the rest of the run. Other members can still visit and buy independently.
- Spending uses the shared wallet. Purchases are validated by the host for funds, range, run/request order, catalog revision, eligibility, and prior purchase.
- Shop browsing stops only that player's controls. Combat, enemies, hazard cycles and the area clock continue. **Escape** closes the shop; it does not open a party-pause menu on that same press.
- Downing, leaving range, stopping/retrying, or a group upgrade/pause closes the browser. The ordinary Escape menu and group XP/chest/terminal choices retain their party-wide pause.

This is the explicit exception to the earlier all-group upgrade policy. Shops do not create an XP level-up, group bonus queue, or selection heal.

## Scope and validation

Chest position randomization is implemented here. Random chest tiers and purchase-driven chest price inflation remain separate backlog items; existing chest tiers/prices remain.

Windows development build succeeded. All targeted functional checks passed:

- Three randomized retries: supported/reachable, separated actors; central terminal slots; stable active-run layout; changed physical placements; boss prop/ring alignment.
- Host/client: exact shared layouts across two runs, stable snapshot application, and relocated console activation.
- Personal shops: three distinct initial offers, stable reopening, insufficient funds, stale/capped/duplicate rejection, personal-only reward, shared single charge, independent host entitlement, out-of-range/down/reset closure, and non-pausing browsing. Gravity and knockback continue while controls are occupied.
- All eight terminals: correct tier duration/reward, progress archive, single reward, enemy packs, reset. Host/client tier replication passed.
- Existing co-op boss voting/fight/victory and overlapping party menus/upgrade pauses passed.
- Six local instances passed the economy regression, including shared wallet, duplicate requests, and rewards queued for a downed member. The old teleport fixture now waits for the host to observe the buyer in range before sending the purchase; production range validation is unchanged.
- Shop UI visually reviewed at 1600x900.

Results: [validation-results.json](../Playtest/RandomEncountersShops/validation-results.json). Detailed logs remain in local `AppData/Local/SwarmRandomEncounterTests`. Use the same build for every party member. Local automated instances do not establish internet-latency behavior or replace human playtesting. NPC art and shop presentation are prototype assets; target-PC performance and final balance still require playtesting.

Rebuild the scene/terminals and executable using `Swarm.Editor.ProductionBatchBuild.BuildRandomEncounters`. Use `BuildCurrentRandomEncounters` for code-only rebuilds. Opt-in development flags: `-swarm-run-layout-test`, paired `-swarm-run-layout-role host/client`, `-swarm-shop-test`, paired `-swarm-shop-role host/client`, and `-swarm-download-tier-test`.
