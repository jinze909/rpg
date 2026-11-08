---
name: story-done
description: Review the evidence for one RPG story and close it only when every required acceptance check passes.
argument-hint: "[story-path]"
disable-model-invocation: true
---

Review the story at `$ARGUMENTS` when supplied. Otherwise use the current story in `AI2-STATUS.md`.
Follow **Verify** and **Handoff** in `production/workflow.md`. Ask the QA lead to review evidence where available and permitted, or perform the same review in this session.
If a required check lacks passing evidence, keep the story open and record the exact missing check. If all required checks pass, mark the tested candidate Done and update AI2-STATUS.md and BACKLOG.md.
