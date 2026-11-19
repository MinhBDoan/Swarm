# Central flats grind rails

November 18, 2026. Build: `Builds/Swarm-GrindRails/Swarm.exe`.

Six fixed cyan rails cross the central flats: south straight (-115,150-190), south diagonal (-205,225 to -175,255), middle straight (-120,300-345), middle curve (-210,400 to -165,435), north diagonal (-120,470 to -150,500), and north curve (-145,540 through -120,555 to -95,540). Coordinates are X/Z. Rails follow the ground with low supports and do not obstruct ordinary movement or enemy navigation.

Press E near a rail to enter. Entry direction follows approach velocity, or facing when nearly stationary. Flat rails preserve entry speed (minimum 4 m/s for a slow entry); uphill/downhill rails respond to gravity. Mouse left/right shifts the balance marker, countering instability and curve forces. The camera follows rail heading while horizontal mouse input controls balance. Vertical mouse still adjusts camera pitch.

Space jumps off with forward speed and normal jump lift; Ctrl/C drops off with speed. Finishing a rail also releases naturally. Losing balance slips off the indicated side, retaining 75% of forward speed plus a small sideways impulse. A short regrab cooldown prevents immediate accidental attachment. Enemy knockback detaches the player. Pause and upgrade menus freeze normal input and grinding.

Balance sensitivity, drift, curve forces, speed floor, rail placements, and failure penalty are initial tuning. Review whether short lines are too easy, curves too abrupt, and entry reach feels forgiving. Try slide-hop into grind, grind-jump into wall bounce or another rail, and intentional recoveries after slipping. Six-player input feel and rail traffic need human playtesting; transform replication uses the existing owner movement system.

## Verification

Windows development build passed. Automated grind checks verify all six grounded/clear lines, entry and reverse direction, momentum preservation, mouse balance correction, jump/crouch exits, recoverable failed balance, regrab delay, enemy impulse release, obstruction/end release, reset, and menu pause. Existing movement, zipline and randomized-layout probes passed on this build. Results: `Docs/Playtest/GrindRails/`. Layout placement now reserves central terminal slots before other terminals and falls back within the central flats. Rail feel and camera behavior still need hands-on review; no new remote multiplayer rail test or hardware benchmark is claimed.
