# Central path slopes and reward pass

November 17, 2026.

## Player-facing changes

- The middle shortcut is rebuilt as a terrain crest with broad sloped shoulders joining the central flats. Players can approach from the sides rather than encountering the edge of a floating strip. The route still connects the shelter and pumping yard.
- Three additional chests are spread across the central area: south Rare (30 party money), middle Rare (30), and north Epic (75). Opening one grants each party member their own upgrade choice, using the shared selection pause.
- Existing terminals and the original four chests remain. This pass retains the current fixed chest prices; randomized tiers and purchase-driven price increases remain separate backlog work.

## Try it

Build: `Builds/Swarm-CentralSlopesLoot/Swarm.exe`. Press **F** for combat and reward interactions. Movement practice can be used to explore the terrain first.

Enter the central flats and approach the middle shortcut from both north and south, especially toward its western rise to the pumping yard. Try walking up, sliding down, and crossing diagonally. Check whether it reads as terrain rather than a suspended walkway, and whether the shoulders leave enough combat room.

Chest locations in world coordinates, for development reference:

| Chest | Position | Reward/cost |
|---|---|---|
| South flats | (-125, -2.75, 190) | Rare / 30 |
| Middle flats | (-183, -4, 395) | Rare / 30 |
| North flats | (-135, -2.6, 525) | Epic / 75 |

## Validation

Windows development build succeeded. Runtime checks passed:

- Actual controller side approaches from both directions at three sections; slope surfaces and navigation to the crest.
- Existing three terrain folds, ordinary loop connections in both directions, safe edge support, enemy navigation/spawning, and central-area pursuit.
- All new chest ground heights, navmesh access, shared deductions, duplicate rejection, reward tiers, and reset behavior.
- New central chests in a real host/client pair: remote purchases, opened-state replication, independent party choices, and restart cleanup.
- Existing economy regression with one host and five local clients, including rewards queued while downed.

The route audit caught unsupported mouth corners and an east entrance detour during integration. The final map trims unsupported shoulders and adds an actual 96-triangle terrain apron connecting the flats and canyon trail; both regression checks now pass. The main middle path uses 540 triangles and one mesh collider. Images are in `Docs/Playtest/CentralSlopesLoot`.

Machine-readable results: [validation-results.json](../Playtest/CentralSlopesLoot/validation-results.json). Detailed logs remain in the local `AppData/Local/SwarmCentralSlopesTests` folder. The six-instance check is local automation, not six-human or internet testing. Automated checks are separate from player judgment of movement feel and reward placement. This pass does not establish a minimum-PC performance target or finish the map milestone.
