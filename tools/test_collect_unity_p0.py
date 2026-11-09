#!/usr/bin/env python3
"""Regression checks for Unity P0 log acceptance and compiler failures."""
import unittest

from collect_unity_p0 import UNITY_COMPILER_ERROR


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
