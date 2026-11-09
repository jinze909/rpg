# 001 · Validate the ORK bootstrap

Status: Verified in Unity 6000.6.8f1

## Outcome

Open the project's startup scene and confirm that ORK 3.22.0 with Makinom 2.21.0 initializes using the project's saved settings.

## Prerequisites

- Unity 6000.6.8f1 with an active license.
- The pinned ORK package installed using the [repository setup](../../README.md#本地启动).

## Acceptance

| Check | Evidence |
| --- | --- |
| Unity 6.6 resolves bundled UGUI with TextMesh Pro, imports the project and compiles without project errors. Record the generated package lock. | Passed. Unity resolved UGUI 2.6.0 and verified the TMP type. The generated lockfile is committed. |
| Running Tools > RPG > Wire ORK Bootstrap adds exactly one ORK Game Starter with the saved Project asset. Repeating the command leaves the scene with one starter. | Passed. The second command preserved the saved scene and its sole Starter. |
| Running the [batch validation](../../README.md#批处理启动验收) on the candidate commit returns exit code 0 and logs `RPG_BOOTSTRAP_VALIDATED`. | Passed. Exit code 0 and the required marker were recorded. |
| With Reload Domain and Scene set, enter and exit Play Mode twice. Both runs load the ORK project without Console errors. | Passed. Both reload cycles initialized the saved Makinom and ORK project. |

## Scope

Verified revision `6ea56a9746106559d32dbcd0456cfec17fce29d2`. [Clean-checkout Unity verification](https://github.com/jinze909/rpg/actions/runs/42551491951/job/128948965800)

Use `Assets/Game/Scenes/Bootstrap.unity` as the entry point. Preserve the project's ORK settings and scene metadata. The next playable prototype is [002](002-soundwell-signal.md).
