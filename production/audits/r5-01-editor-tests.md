# R5.1 · Editor tests — first evidence pass

Audited against `1ced4ecee34460dbe1ce15b63d4a6332d2d3560c` on 2026-11-15. This is an **audit result**, not a claim that the test suite was redesigned.

- `Assets/Game/Tests/Editor/` contains nine C# test sources and `Rpg.Bootstrap.EditorTests.asmdef`. The assembly is Editor-only, constrained by `UNITY_INCLUDE_TESTS`, and explicitly references NUnit, Makinom 2 and ORK 3.
- The existing full Story 003 candidate on this baseline completed 39/39 Unity tests in [Run 43696951771](https://github.com/jinze909/rpg/actions/runs/43696951771), with exact-SHA clean verification.
- Concrete coverage trap: `tools/collect_unity_p0.py` maintains an explicit manifest of Unity test names; a newly added passing `[Test]` can still fail delivery as `Unexpected Unity tests`. This happened in [M6 Run 43697514025](https://github.com/jinze909/rpg/actions/runs/43697514025) and [M9 Run 43696536640](https://github.com/jinze909/rpg/actions/runs/43696536640). Each fix belongs to its own implementation candidate, not to this audit branch.
- Next independent acceptance increment: compare all currently discovered NUnit/UnityTest methods against the exact evidence manifest, and add a Python unit guard for unmapped newly declared test names without weakening rejection of missing, duplicate or unexpected executed tests.
- Do not move `SoundwellSmokeHost` or alter assembly references until the runtime/test dependency graph is established. A compiler-green test project is not proof of a safe assembly migration.

Ownership: this audit branch tracks test coverage and editor assembly boundaries; implement and verify each code change as an independent reviewable increment.
