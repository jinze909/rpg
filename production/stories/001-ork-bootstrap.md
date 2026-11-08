# 001 · Validate the ORK bootstrap

Status: Pending

## Outcome

Open the project's startup scene and confirm that ORK 3.22.0 with Makinom 2.21.0 initializes using the project's saved settings.

## Prerequisites

- Unity 6000.6.8f1 with an active license.
- The pinned ORK package installed using the [repository setup](../../README.md#本地启动).

## Acceptance

| Check | Evidence |
| --- | --- |
| Unity 6.6 resolves UGUI 2.0 with TextMesh Pro, imports the project and compiles without project errors. Record the generated package lock. | Not run. Editor unavailable in the initialization environment. |
| Running Tools > RPG > Wire ORK Bootstrap adds exactly one ORK Game Starter with the saved Project asset. Repeating the command leaves the scene with one starter. | Not run. |
| Running the [batch validation](../../README.md#批处理启动验收) on the candidate commit returns exit code 0 and logs `RPG_BOOTSTRAP_VALIDATED`. | Not run. |
| With Reload Domain and Scene set, enter and exit Play Mode twice. Both runs load the ORK project without Console errors. | Not run. |

## Scope

CI 实现草稿见 [Unity / ORK P0 CI](../../docs/unity-p0-ci.md)。尚未运行；首次准备配置的成功结果不能替代干净提交验收。

Use `Assets/Game/Scenes/Bootstrap.unity` as the entry point. Preserve the project's ORK settings and scene metadata. After bootstrap passes, define the next playable prototype from the newly selected game direction.
