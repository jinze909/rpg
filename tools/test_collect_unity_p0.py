#!/usr/bin/env python3
"""Regression checks for Unity P0 log acceptance and compiler failures."""
import unittest
import tempfile
from pathlib import Path

from collect_unity_p0 import (
    EXPECTED_TESTS,
    INPUT_SYSTEM_TESTS,
    ORK_UI_TESTS,
    ORK_UI_MARKER,
    URP2D_MARKER,
    UNITY_COMPILER_ERROR,
    SOUNDWELL_MARKERS,
    SOUNDWELL_TESTS,
    expected_tests_for,
    format_performance_summary,
    validate_memory_phases,
    validate_build_profile_player,
    MEMORY_PHASES,
)



class PerformanceSummaryTests(unittest.TestCase):
    def setUp(self):
        self.performance = {
            "environment": "Unity Editor Play Mode (GameCI Linux; not player hardware)",
            "frame_samples": 240,
            "frame_time_ms_p50": 16.7,
            "frame_time_ms_p95": 23.4,
            "frame_time_ms_max": 41.2,
            "gc_gen0_collections_delta": 3,
            "gc_gen1_collections_delta": 1,
            "gc_gen2_collections_delta": 0,
            "managed_heap_start_bytes": 1048576,
            "managed_heap_end_bytes": 2097152,
            "managed_heap_peak_bytes": 3145728,
        }

    def test_summary_exposes_frame_time_gc_and_heap_trends(self):
        summary = format_performance_summary(self.performance)
        self.assertIn("Frames sampled: 240", summary)
        self.assertIn("P50 16.70 ms; P95 23.40 ms; max 41.20 ms", summary)
        self.assertIn("Gen0 +3; Gen1 +1; Gen2 +0", summary)
        self.assertIn("start 1.00 MiB; peak 3.00 MiB; end 2.00 MiB", summary)
        self.assertIn("not a player-hardware benchmark", summary)

    def test_missing_performance_sample_has_no_claim(self):
        self.assertEqual(format_performance_summary(None), "")
        self.assertEqual(format_performance_summary({}), "")
        self.assertEqual(format_performance_summary([]), "")

    def test_invalid_or_incomplete_samples_cannot_crash_summary(self):
        invalid = (
            {"frame_samples": 0},
            {"frame_time_ms_p95": float("nan")},
            {"frame_time_ms_p95": float("inf")},
            {"frame_time_ms_p95": 0},
            {"frame_time_ms_p95": 100},
            {"managed_heap_peak_bytes": 0},
            {"gc_gen1_collections_delta": -1},
            {"frame_samples": True},
            {"gc_gen0_collections_delta": 0.1},
            {"environment": ["not", "text"]},
        )
        for patch in invalid:
            with self.subTest(patch=patch):
                performance = dict(self.performance)
                performance.update(patch)
                self.assertEqual(format_performance_summary(performance), "")

    def test_environment_is_rendered_on_one_line(self):
        performance = dict(self.performance)
        performance["environment"] = "Unity Editor\nmalicious next line"
        self.assertIn("- Environment: Unity Editor malicious next line\n",
                      format_performance_summary(performance))

class BuildProfilePlayerEvidenceTests(unittest.TestCase):
    def sample(self):
        return {
            "schema_version": 1, "unity_version": "6000.6.8f1",
            "platform_guid": "cb423bfea44b4d658edb8bc5d91a3024",
            "target": "StandaloneLinux64",
            "test_profile": "Assets/Settings/Build Profiles/Soundwell Test Linux.asset",
            "scripting_backend": "Mono2x",
            "scenes": ["Assets/Game/Scenes/Bootstrap.unity",
                       "Assets/Game/Scenes/SoundwellTrial.unity"],
            "result": "Succeeded", "development_build": True,
            "total_errors": 0, "executable_bytes": 40000,
            "build_report_bytes": 90000, "data_directory_present": True,
            "environment": "Unity Linux Editor CI; real Player; not a user device",
        }

    def test_requires_real_build_and_global_scene_list(self):
        self.assertEqual(validate_build_profile_player(self.sample()), [])
        for name, changed in [
            ("no_executable", 0), ("non_linux", 1), ("test_as_release", False),
            ("wrong_scenes", []), ("bad_backend", "Other")
        ]:
            with self.subTest(name=name):
                report = self.sample()
                key = {"no_executable": "executable_bytes", "non_linux": "target",
                       "test_as_release": "development_build", "wrong_scenes": "scenes",
                       "bad_backend": "scripting_backend"}[name]
                report[key] = changed
                self.assertTrue(validate_build_profile_player(report))


class MemoryPhasesEvidenceTests(unittest.TestCase):
    def sample(self, phase, scene):
        return {
            "phase": phase, "scene": scene, "unity_frame": 100,
            "managed_gc_heap_bytes": 20000000,
            "unity_managed_used_bytes": 10000000,
            "unity_managed_reserved_bytes": 40000000,
            "unity_total_allocated_bytes": 50000000,
            "unity_total_reserved_bytes": 60000000,
            "unity_unused_reserved_bytes": 10000000,
            "loaded_game_objects": 120, "loaded_textures": 10,
            "loaded_audio_clips": 2,
        }

    def evidence(self):
        scenes = ("Bootstrap", "SoundwellTrial", "SoundwellTrial",
                  "SoundwellTrial", "Bootstrap")
        return {
            "schema_version": 1,
            "unity_version": "6000.6.8f1",
            "scenario": "soundwell-enter-rescue-reload-return",
            "environment": "Unity Editor Play Mode (GameCI Linux container; not player hardware)",
            "sample_source": "UnityEngine.Profiling.Profiler + System.GC",
            "snapshots": [self.sample(phase, scene)
                          for phase, scene in zip(MEMORY_PHASES, scenes)],
            "bootstrap_managed_gc_heap_delta_bytes": 0,
            "bootstrap_unity_total_allocated_delta_bytes": 0,
        }

    def test_real_metrics_can_be_compared_without_declaring_growth_a_leak(self):
        data = self.evidence()
        data["snapshots"][-1]["managed_gc_heap_bytes"] += 2048
        data["bootstrap_managed_gc_heap_delta_bytes"] = 2048
        self.assertEqual(validate_memory_phases(data), [])

    def test_missing_unreal_or_misordered_samples_are_rejected(self):
        for mutation in ("empty", "wrong_scene", "negative", "bool", "delta"):
            with self.subTest(mutation=mutation):
                data = self.evidence()
                if mutation == "empty":
                    data["snapshots"] = []
                elif mutation == "wrong_scene":
                    data["snapshots"][2]["scene"] = "Bootstrap"
                elif mutation == "negative":
                    data["snapshots"][1]["loaded_textures"] = -1
                elif mutation == "bool":
                    data["snapshots"][3]["managed_gc_heap_bytes"] = True
                elif mutation == "delta":
                    data["bootstrap_unity_total_allocated_delta_bytes"] = 999
                self.assertTrue(validate_memory_phases(data), mutation)


class SoundwellAcceptanceContractTests(unittest.TestCase):
    def test_ork_battle_test_and_runtime_marker_are_required(self):
        self.assertIn(
            "Rpg.Tests.Editor.SoundwellOrkBattleTests.ThreePlayerCombatantsEnterOneOrkTurnBasedBattle",
            SOUNDWELL_TESTS,
        )
        self.assertIn("RPG_SOUNDWELL_ORK_BATTLE_VALIDATED", SOUNDWELL_MARKERS)
        self.assertIn("RPG_SOUNDWELL_DIRECT_RESCUE_VALIDATED", SOUNDWELL_MARKERS)
        self.assertIn(
            "Rpg.Tests.Editor.SoundwellTrialTests.PlayerCanRingWrapRetreatAndRetry",
            SOUNDWELL_TESTS,
        )
        self.assertIn("RPG_SOUNDWELL_SIGNAL_VALIDATED", SOUNDWELL_MARKERS)


class OrkUiAcceptanceContractTests(unittest.TestCase):
    def test_unity_ui_surface_test_and_runtime_marker_are_required(self):
        self.assertEqual(
            ORK_UI_TESTS,
            {"Rpg.Tests.Editor.MakinomUiIntegrationSurfaceTests.UnityUiModuleExposesItsSystemSettingsAndCreationCommands"},
        )
        self.assertEqual(ORK_UI_MARKER, "RPG_ORK_UI_SURFACE_VALIDATED")


class Urp2DAcceptanceContractTests(unittest.TestCase):
    def test_real_urp_2d_runtime_test_and_marker_are_required(self):
        self.assertIn(
            "Rpg.Tests.Editor.P0BootstrapTests.Urp2DRendererIsTheOnlyActivePipeline",
            EXPECTED_TESTS,
        )
        self.assertEqual(URP2D_MARKER, "RPG_URP2D_VALIDATED")
        self.assertIn(
            "Rpg.Tests.Editor.SoundwellInputSystemTests.InputActionsHaveSeparateContextsAndKeepBothBackendsEnabled",
            INPUT_SYSTEM_TESTS,
        )
        self.assertIn(
            "Rpg.Tests.Editor.SoundwellInputSystemTests.EntryAndGameplayContextsRouteLiveKeyboardAndGamepadEvents",
            INPUT_SYSTEM_TESTS,
        )

    def test_prepare_and_verify_expect_only_the_tests_the_workflow_runs(self):
        self.assertEqual(len(EXPECTED_TESTS), 4)
        self.assertEqual(expected_tests_for("prepare", True), EXPECTED_TESTS)
        with tempfile.TemporaryDirectory() as root:
            source_root = Path(root)
            baseline = EXPECTED_TESTS | SOUNDWELL_TESTS | INPUT_SYSTEM_TESTS | ORK_UI_TESTS
            verify_tests = expected_tests_for("verify", True, source_root=source_root)
            self.assertEqual(verify_tests, baseline)
            self.assertEqual(len(verify_tests), 9)

            battle_file = source_root / "Assets/Game/Tests/Editor/SoundwellOrkBattleTests.cs"
            battle_file.parent.mkdir(parents=True)
            battle_file.write_text(
                "[UnityTest] public IEnumerator "
                "BattleBackCancelEventRetreatsAndReleasesOrkParticipants() { yield break; }",
                encoding="utf-8",
            )
            added = expected_tests_for("verify", True, source_root=source_root)
            self.assertEqual(
                added,
                baseline | {
                    "Rpg.Tests.Editor.SoundwellOrkBattleTests.BattleBackCancelEventRetreatsAndReleasesOrkParticipants",
                },
            )
            self.assertEqual(len(added), 10)

            direct_file = source_root / "Assets/Game/Tests/Editor/SoundwellDirectVictoryEscortTests.cs"
            direct_file.write_text(
                "[UnityTest] public IEnumerator "
                "OrkVictoryDoesNotBypassSurvivorEscortOrSafeArrival() { yield break; }",
                encoding="utf-8",
            )
            self.assertEqual(len(expected_tests_for("verify", True, source_root=source_root)), 11)
            self.assertEqual(
                expected_tests_for("prepare", True, source_root=source_root), EXPECTED_TESTS
            )
            # A method without UnityTest cannot satisfy the acceptance contract.
            direct_file.write_text(
                "public IEnumerator OrkVictoryDoesNotBypassSurvivorEscortOrSafeArrival()"
                " { yield break; }", encoding="utf-8"
            )
            self.assertEqual(len(expected_tests_for("verify", True, source_root=source_root)), 10)


class CompilerLogTests(unittest.TestCase):
    def test_successful_editor_updater_notice_is_not_compilation_failure(self):
        # Seen in a completed post-merge run with 4/4 tests passing and both
        # editor exit codes 0. It is not a compiler diagnostic by itself.
        log = (
            "Unable to update following assemblies: Makinom2.UnityUI.dll\n"
            "RPG_SOUNDWELL_SIGNAL_VALIDATED\n"
            "<test-run testcasecount=\"4\" result=\"Passed\"/>\n"
        )
        self.assertIsNone(UNITY_COMPILER_ERROR.search(log))

    def test_true_csharp_and_assembly_failures_remain_blocked(self):
        for diagnostic in (
            "Assets/Game/Soundwell/SoundwellView.cs(129,100): error CS0103: carry not found",
            "Scripts have compiler errors.",
            "Failed to resolve assembly 'Game.Tests'",
            "Assembly Game.Tests will not be loaded",
        ):
            with self.subTest(diagnostic=diagnostic):
                result = UNITY_COMPILER_ERROR.search(diagnostic)
                self.assertIsNotNone(result)

    def test_real_error_overrides_benign_updater_notice(self):
        log = (
            "Unable to update following assemblies\n"
            "Assets/Game/Soundwell/SoundwellSmokeHost.cs(429,26): error CS1739\n"
        )
        result = UNITY_COMPILER_ERROR.search(log)
        self.assertIsNotNone(result)
        self.assertEqual(result.group(0).lower(), "error cs1739")


if __name__ == "__main__":
    unittest.main()
