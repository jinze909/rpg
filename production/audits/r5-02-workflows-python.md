# R5.2 · GitHub workflows and Python tools — first evidence pass

Audited at `1ced4ecee34460dbe1ce15b63d4a6332d2d3560c` on 2026-11-15.

- Three workflows exist: `.github/workflows/unity-p0.yml`, `unity-p0-editor.yml`, and `close-merged-pending.yml`. The P0 workflow accepts push changes from `codex/dev` and `codex/pending/**` **only for the configured paths**, not narrative-only commits.
- Unity P0 concurrency is scoped to the branch ref, not a repository-global slot. Candidate checks must be associated with the exact head SHA; a successful older SHA does not validate a later edit.
- Nine Python files are present under `tools/`. The exact collector rejects unexpected test names, while the evidence reuse tool refuses missing or ineligible artifacts. No tool has been bypassed in this audit.
- Reproducible latest failure: [Run 43697514025](https://github.com/jinze909/rpg/actions/runs/43697514025) produced 40/40 passing Unity tests but failed the strict collector because `MissingMixerFallbackKeepsMutedBellSilentAndPreservesPreference` was absent from the test manifest. A follow-up fix is already in the dedicated M6 candidate.
- Next executable audit increment: add a Python-side manifest consistency regression to the existing collector test suite and run it in the existing workflow, then separately verify the branch cleanup's exact-SHA and no-running-CI preconditions. Do not disable strict evidence checks or change task schedules.

This file records observable evidence and the next check; it does not mark any runtime defect fixed.
