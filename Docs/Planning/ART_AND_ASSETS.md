# Art direction and asset workflow

**Status:** review draft · November 14, 2026. Approved creature designs exist; a complete art direction is still being selected.

## Current direction

**Confirmed:** sci-fi setting with people and monsters/aliens; canyon first, cyberpunk city later. Consistent assets matter more than generating many unrelated designs. Blender is the editable master workspace; generated/free assets may supply starting material. The player is learning basic asset editing and has a drawing tablet.

**Approved references:**

- Charger: stout four-legged creature with blue-gray flesh, orange/ochre layered armor, wedge-shaped armored head, and orange eyes. [Single-image reference](../ArtReferences/charger-single-v1.png), [turnaround](../ArtReferences/charger-turnaround-v1.png).
- Sentinel: large bipedal brute with oversized armored forearms suitable for ground pounds. Reddish flesh, sandstone armor, turquoise seams, orange eyes. [Turnaround](../ArtReferences/sentinel-turnaround-v1.png).

**Proposed style rules:** readable silhouettes, broad material/color groups, deliberate chunky forms, restrained tiny surface detail. Prioritize readability at gameplay distance and differentiation between enemy roles. References guide shape and palette; they do not establish a final lighting, texture-resolution, or shader standard.

**Open:** player design, remaining enemy families, environment style references, stylization level, UI style, animation character, final palette, audio direction, and how canyon materials connect visually to the city.

## Asset production stages

1. **Design:** approve one subject's silhouette, role, palette, proportions, and attack needs. Preserve the approved references.
2. **Generate or source:** use one creature per single-image input. Use separate view slots only when the generator actually supports them; never send a combined turnaround as a single subject image again. Inspect shape before spending more credits on downstream tools.
3. **Archive:** retain original GLB and embedded textures, generation reference, service/settings/date, and evidence of the applicable license/paid plan. API and website billing are separate considerations; do not assume a subscription covers API use.
4. **Inspect in Blender:** front/side/back/underside, number of creatures, limbs, floating pieces, texture seams, normals, dimensions, and orientation.
5. **Prepare game mesh:** preserve silhouette; reduce or rebuild geometry; keep useful joint topology. Bake high-resolution detail where helpful. Decimation alone is not proof of animation readiness.
6. **Rig and test:** fit joints; keep plates rigid where intended; blend soft tissue at joints. Test visible and far-side limbs, head, neck, crouch, and attack extremes. Compare revisions at the same angles.
7. **Animate:** build the minimum clips required by existing behavior; validate transitions and foot contact. Agree whether clips are in-place before integration; gameplay remains responsible for collision and actual displacement.
8. **Integrate:** export mesh, skeleton, and clips; assign materials; verify scale, facing, root placement, warnings and collider alignment in Unity. Keep the original source untouched.
9. **Validate:** compare at gameplay distance, profile repeated enemies, test on a client, and review cleanup on defeat/retry. Add LODs only with measured need and visual checks.

**Confirmed performance direction:** keep PC requirements modest. Map density must have a purpose; decorative clutter, heavy materials, and high-poly sources should not become the default way to fill open space. Set budgets through measurement on an agreed target PC, which is still undecided.

## Working budgets — proposals, not guarantees

| Property | Starting proposal | How to approve |
|---|---|---|
| Charger closest mesh | About 5k–15k triangles; current first reduction is about 12k | Compare silhouette, deformation, and repeated-enemy frame time. Recount after rigging edits. |
| Materials | Aim for one material per ordinary enemy where practical | Verify shading needs and draw cost. |
| Textures | Try a 1k or 2k set; retain original source textures | Compare at gameplay distance, then measure memory and quality. |
| Skeleton | Keep only useful deform/control/export bones | Test deformation and animation cost; the current test has 15 bones, not a final budget. |
| Sentinel | Potentially higher budget than ordinary swarm enemies | Establish from encounter count and on-screen size. No fixed budget yet. |

A triangle reduction ratio is not a performance benchmark. Materials, shadows, skinning, VFX, AI, navigation, and network replication also matter.

## Source and export conventions — proposed

- High-resolution GLB is archival input; `.blend` is the editable master; FBX is the initial proposed Unity mesh/rig/animation exchange format. Confirm export/import settings with a real round-trip before treating them as standard.
- Unity working units are meters. Apply a deliberate scale/orientation conversion once and verify in-game; the generator's normalized dimensions are not the desired enemy size.
- Names should describe subject, stage, and revision, such as `Charger_GameMesh_v01.blend` and `Charger_Run_v01.fbx`. Avoid replacing accepted files silently.
- Keep sources outside `Assets` until they are intended for Unity import. Current experiments are in `Docs/ArtReferences/ChargerSingleWork`; this is a working location, not a final asset-library structure.
- Before Git tracking, choose a source-art/LFS or external archive policy for large `.blend`/`.glb` files. Do not put all experimental binaries into the initial commit by default.
- Maintain an asset record: source/license, references, tool versions, triangle count, materials/textures, scale, skeleton/clips, known issues, and Unity validation result.

## Charger handoff

Current file: [Charger_Skeleton_Test_v4.blend](../ArtReferences/ChargerSingleWork/Charger_Skeleton_Test_v4.blend). Player feedback: head motion looks better and back movement is improved. Work is intentionally paused.

Resume by cleaning the neck/shoulder connection and reviewing a full pose range. Existing seam strips are experimental and must be checked for pinching, degenerate faces, shading artifacts, and export compatibility. They should not be accepted as final topology just because they close a rendered gap.

The current test sequence is `Inspection_Bends_Not_Final_Walk`. It is not a locomotion animation. No Charger art prefab has been integrated into gameplay. Retain placeholder colliders and attack logic when introducing the art.

## Licensing gate

Every external asset or generator must have its actual license checked for intended commercial distribution, including territorial restrictions, attribution, and separate model/dependency terms. Store that evidence with the asset. An application's open-source license does not automatically cover its model weights or outputs. A generator being paid does not guarantee suitable topology, animation quality, or rights for every use.
