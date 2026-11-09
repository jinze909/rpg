#!/usr/bin/env python3
"""Regression checks for Unity P0 log acceptance and compiler failures."""
import unittest

from collect_unity_p0 import UNITY_COMPILER_ERROR, format_performance_summary



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
