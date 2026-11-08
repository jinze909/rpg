---
name: dev-story
description: Implement and verify one RPG development story using the repository's Unity and ORK workflow.
argument-hint: "[story-path]"
disable-model-invocation: true
---

Use the story at `$ARGUMENTS` when supplied. Otherwise take the current story from `production/session-state/active.md`.
Follow **Prepare**, **Implement**, and **Verify** in `production/workflow.md`. Use the producer, Unity specialist, and QA lead roles where available and permitted. The lead can perform the same checks in one session.
Apply the results to the story and session checkpoint. Leave missing evidence visible and report the next acceptance check.
