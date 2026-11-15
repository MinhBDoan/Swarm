# Version control and parallel work

November 15, 2026.

Repository: https://github.com/MinhBDoan/Swarm

## Naming

Use descriptive task names, such as `map/central-flats`, `gameplay/area-timer`, and `fix/group-pause`. Per the project owner's preference, do not include `codex` or `ai` in branch names, commit subjects, pull request titles, or other task labels. Existing source text and tool/provenance documentation need not be renamed.

## What is backed up by Git

Track Unity source assets and their `.meta` files, packages, project settings, approved reference PNGs, and project documents. Exclude Unity caches, generated builds, local user settings, backup ZIPs, credentials, and the high-resolution Charger experiment folder. No excluded local files are deleted by this policy.

The original Charger GLBs and experimental Blender files are **not backed up by GitHub**. They remain on this computer. Before resuming asset production, establish a separate verified archive or an explicit Git LFS policy. The local links to these files in planning documents will not resolve in a fresh checkout until those art sources are supplied.

## Parallel implementation

Create a branch and isolated worktree per bounded assignment. Do not share a Unity editor cache or edit the same scene/prefab concurrently. Assign one owner to integrate scene-builder changes and regenerate the saved scene. Carry `.meta` files with asset changes so GUID references survive.

Use ordinary commits describing the player-facing change. Review staged files before committing. Push completed branches and review the changes before integrating into `main`. An initial repository checkpoint does not mean the current gameplay has been freshly validated.
