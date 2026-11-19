# Elite enemies and challenge spots

November 18, 2026. Windows development build: `Builds/Swarm-EliteChallenges/Swarm.exe`.

## First-pass behavior

- Ordinary Chaser, Runner, Spitter and Charger variants: 1.5x health, 1.25x attack damage, gold collar and ELITE label. Their normal attack behavior remains intact. Sentinel is separate.
- First periodic elite pack at 45 active seconds; subsequent packs roughly every 35 seconds when enemy capacity allows. Packs contain 1-3 elites depending on party size.
- Ordinary elite kills have a 20% chance to leave a free Rare chest. These are persistent interactable rewards, not automatic upgrades.
- Three pink challenge beacons are randomized along with other run encounters. Press E nearby to activate; one active at a time, each spot once per run.
- Challenges call 10 elites solo, plus two per extra player, maximum 20. Two-enemy packs arrive over roughly ten seconds. All reinforcements must arrive and die before completion. Ordinary waves and the area timer continue.
- Completion leaves one guaranteed free Epic chest at the spot. Challenge guards do not also roll ordinary elite chest drops. There is no challenge timeout or arena lock.
- Press E at a free chest: no wallet charge; every member gets independent upgrade choices. The party and area timer pause until eligible choices finish. Downed teammates retain the queued reward under the current revive policy.

All numeric values, tiers and marker visuals above are provisional tuning.

## Implementation limits

Host validates interaction sender, current run, proximity and active state. Claimed chest IDs stay consumed for the run, preventing duplicate commands from awarding again. Ordinary free chest records are bounded (32 unopened, 128 over a run), with three separate reserved challenge rewards. Challenges can temporarily use up to 20 enemy slots above the ordinary live cap, preventing ordinary waves from starving objective reinforcements. These limits do not establish a low-end hardware performance target.

## Hands-on review

1. Begin a run; fight until a gold-marked elite arrives. Compare durability and attacks with its ordinary counterpart.
2. Find a pink beacon. Press E, move freely and kill the entire squad. Check the arrival/alive counter; clearing an early pack must not finish the challenge.
3. Return to its chest. Open it and select an Epic upgrade. The chest must cost nothing and never reward twice.
4. In co-op, let another player activate/open. Both players should get their own choice and remain paused until everyone finishes. Check the normal waves resume afterward.
5. Review whether challenges are worth the risk, and whether random Rare drops are too frequent. Test movement readability under real six-player combat before accepting balance.

## Automated verification

Results are recorded in `Docs/Playtest/EliteChallenges/`. Solo probes cover all four variant stat/damage paths, overtime retention, elite timing/pause/caps/reset, grounded randomized placements, challenge pacing/full-clear gating, chest tiers/claims/reserved capacity, and drop exclusions. Paired probes cover client activation validation, replicated challenge state, duplicate commands, party rewards/pause, queued downed rewards and cleanup. Tests use local processes, not six human players or internet conditions.

Final build completed successfully with no C# compile errors. Four solo checks and three host/client pairs passed (ten process results, exit 0, no logged exceptions). Coverage includes shared randomized challenge locations on retry. Numeric balance and visual readability still require player review.
