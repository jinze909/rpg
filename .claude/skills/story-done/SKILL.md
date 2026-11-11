---
name: story-done
description: Review the evidence for one RPG story and close it only when every required acceptance check passes.
argument-hint: "[story-path]"
disable-model-invocation: true
---

Review the story at `$ARGUMENTS` when supplied. Otherwise use the current story in `AI2-STATUS.md`.
Follow **Verify** and **Handoff** in `production/workflow.md`. Ask the QA lead to review evidence where available and permitted, or perform the same review in this session.
If a required check lacks passing evidence, keep the story open and record the exact missing check. If all required checks pass, mark the tested candidate Done and update AI2-STATUS.md and BACKLOG.md.

User playtesting, media review and GitHub feedback are not required acceptance gates. Agents perform available verification and record defects or evidence gaps in `production/KNOWN_ISSUES.md`; do not claim subjective enjoyment from automation. Framework and configuration changes are authorized after the isolated final-SHA validation defined in `production/workflow.md`. Handoff includes implementation and agent-verification next steps.
