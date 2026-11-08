# 001 · Validate the ORK bootstrap

Status: Pending

## Outcome

Open the project's startup scene and confirm that ORK 3.22.0 with Makinom 2.21.0 initializes using the project's saved settings.

## Prerequisites

- Unity 2022.3.53f1 with an active license.
- The pinned ORK package installed using the [repository setup](../../README.md#本地启动).

## Acceptance

| Check | Evidence |
| --- | --- |
| Unity imports the project and compiles without project errors. | Not run. Editor unavailable in the initialization environment. |
| Running Tools > RPG > Wire ORK Bootstrap adds exactly one ORK Game Starter with the saved Project asset. Repeating the command leaves the scene with one starter. | Not run. |
| Running the [batch validation](../../README.md#批处理启动验收) on the candidate commit returns exit code 0 and logs `RPG_BOOTSTRAP_VALIDATED`. | Not run. |
| Entering Play Mode in Bootstrap loads the ORK project without Console errors. | Not run. |

## Scope

Use `Assets/Game/Scenes/Bootstrap.unity` as the entry point. Preserve the project's ORK settings and scene metadata. Player spawning, movement, and a Makinom interaction are the next story after bootstrap passes.
