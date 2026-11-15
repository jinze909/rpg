# R5.3 · Unity scenes, Settings and serialized assets — first evidence pass

Audited at `1ced4ecee34460dbe1ce15b63d4a6332d2d3560c` on 2026-11-15; the following are checked repository YAML facts, not screenshots or runtime acceptance.

- `ProjectSettings/ProjectVersion.txt` pins Unity **6000.6.8f1**. `ProjectSettings/EditorBuildSettings.asset` enables `Bootstrap.unity` first and `SoundwellTrial.unity` second.
- `Assets/Game/Scenes/Bootstrap.unity` has three serialized scene GameObjects, including one ORK starter and the entry. `SoundwellTrial.unity` has nine serialized GameObjects and a Tilemap. There is **no serialized Canvas** in either; game-owned UGUI is currently built at runtime. Adding a dedicated Whitewater scene therefore requires explicit build-scene, profile and boot/return path verification.
- `ProjectSettings/GraphicsSettings.asset` points to the saved URP asset. `Assets/Game/Rendering/URP2D/` contains the pipeline and Renderer2D data. `Assets/Game/Resources/` includes the InputAction asset, SFX mixer and two Unity Localization StringTables.
- Both saved Linux Build Profiles currently serialize `m_Scenes: []`, which requires checking effective inherited scene selection via Unity instead of interpreting the empty list as “no scenes build”.
- Next separate implementation increment: test whether scene wiring, input, pixel-perfect lens and both profiles survive an **authored** Whitewater scene asset and reload. Do not edit or regenerate scene YAML manually; run the existing clean Unity import and build acceptance on the exact candidate SHA.

This branch preserves a concrete migration baseline and prohibits inferring a successful playable scene from serialized file counts.
