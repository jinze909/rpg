---
name: dev-story
description: Implement and verify one RPG development story using the repository's Unity and ORK workflow.
argument-hint: "[story-path]"
disable-model-invocation: true
---

Use the story at `$ARGUMENTS` when supplied. Otherwise take the current story from `AI2-STATUS.md`.
Follow **Prepare**, **Implement**, and **Verify** in `production/workflow.md`. Select the relevant roles from `production/team.md`, including producer, lead-programmer, and qa-lead for their mandatory checks. Delegate only where available and permitted. The lead can perform the same checks in one session.
Apply the results to the story, AI2-STATUS.md, and BACKLOG.md when its status changes. Leave missing evidence visible and report the next acceptance check.
