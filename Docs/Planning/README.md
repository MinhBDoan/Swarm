# Project review pack

Drafted November 14, 2026. These documents are a review draft, not a newly approved production scope.

Read in this order:

1. [Product requirements](PRODUCT_REQUIREMENTS.md): the intended game and decisions to review.
2. [Project status](PROJECT_STATUS.md): what exists, its limitations, and how to run it.
3. [Milestone backlog](MILESTONE_BACKLOG.md): a proposed next milestone and bounded assignments.
4. [Art and asset guide](ART_AND_ASSETS.md): current references and a repeatable asset workflow.

Next milestone working document: [Canyon map audit](CANYON_MAP_AUDIT.md), with source measurements, regional recommendations, and a proposed central-flats first pass.

## How to review

For each document, mark anything you want to keep, change, remove, or leave undecided. Start with the product requirements' review questions and the milestone's scope. We can revise the implementation plan after the direction is agreed.

**Confirmed** means explicitly requested or accepted in the conversation. **Proposed** means a recommendation awaiting approval. **Open** means no settled decision. **Implemented** describes source or files inspected, not a guarantee that all behavior is correct.

These documents separate intended design from implementation. Code does not silently turn a prototype choice into a permanent design requirement. A review draft does not override previous user decisions. When a conflict is found, record it and resolve it explicitly.

## Maintenance

- Update requirements when a design decision is accepted; retain stable requirement IDs.
- Update status when a feature is integrated, tested, or found broken. Distinguish source inspection, automated checks, and player feedback.
- Update the backlog as assignments start and finish; attach the actual validation evidence.
- Update asset guidance when an asset completes the pipeline and establishes a useful standard.
- Existing [README](../../README.md) and [PROGRESS](../../PROGRESS.md) retain historical detail. Some older sections are superseded; see the status document before relying on an old number or limitation.

No additional agents were started while drafting this pack.

Repository setup and task naming: [Version control policy](VERSION_CONTROL.md).

Latest map pass: [Grounded middle path and central chests](CENTRAL_SLOPES_LOOT_PLAYTEST.md).

Latest encounter pass: [Randomized layouts and personal NPC shops](RANDOM_ENCOUNTERS_SHOPS.md).
