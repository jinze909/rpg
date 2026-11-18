#!/usr/bin/env python3
"""Regression checks for Unity P0 log acceptance and compiler failures."""
import unittest
import re
import tempfile
import json
from contextlib import chdir
from unittest.mock import patch
from tempfile import SpooledTemporaryFile
import install_ork
import verify_ork
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
    unregistered_unitytest_methods,
    format_performance_summary,
    validate_memory_phases,
    validate_soundwell_route_starts,
    validate_build_profile_player,
    MEMORY_PHASES,
)



import hashlib
import io
from zipfile import ZipFile

from restore_unity_p0 import restore, validate_candidate_contents


class VendorManifestBoundaryTests(unittest.TestCase):
    def test_only_canonical_vendor_assets_and_folder_metas_are_allowed(self):
        valid = verify_ork.valid_vendor_manifest_path
        for path in (
            "Assets/Gaming Is Love.meta",
            "Assets/Gizmos.meta",
            "Assets/Gaming Is Love/Makinom 2/DLL/ORK3.dll",
            "Assets/Gizmos/ORK/Icons/icon.png",
        ):
            with self.subTest(path=path):
                self.assertTrue(valid(path))

        for path in (
            "",
            " Assets/Gaming Is Love.meta",
            "Assets/Gaming Is Love/surplus.dll ",
            "Assets/Other/file.dll",
            "ProjectSettings/ProjectSettings.asset",
            "/Assets/Gaming Is Love/file.dll",
            "../Assets/Gaming Is Love/file.dll",
            "Assets/Gaming Is Love/../Other/file.dll",
            "Assets/Gaming Is Love/./file.dll",
            "Assets/Gaming Is Love//file.dll",
            r"Assets/Gaming Is Love\bad.dll",
            r"Assets/Gaming Is Love/sub\bad.dll",
            r"Assets/Gizmos\ORK/icon.png",
        ):
            with self.subTest(path=path):
                self.assertFalse(valid(path))


class CandidateRestorePreflightTests(unittest.TestCase):
    def test_missing_required_output_keeps_existing_files_and_mode(self):
        with tempfile.TemporaryDirectory() as directory, chdir(directory):
            output = Path("ProjectSettings/EditorSettings.asset")
            output.parent.mkdir()
            output.write_bytes(b"old settings")
            mode = Path("production/ci/p0-mode.txt")
            mode.parent.mkdir(parents=True)
            mode.write_text("prepare\n")
            evidence = Path("evidence")
            evidence.mkdir()
            generated = b"new settings"
            source_sha = "a" * 40
            (evidence / "validation.json").write_text(json.dumps({
                "source_sha": source_sha,
                "mode": "prepare",
                "status": "prepared-candidate",
                "changed_files_sha256": {
                    output.as_posix(): hashlib.sha256(generated).hexdigest(),
                },
            }))
            with ZipFile(evidence / "candidate-files.zip", "w") as archive:
                archive.writestr(output.as_posix(), generated)

            with patch("restore_unity_p0.subprocess.check_output", return_value=source_sha):
                with self.assertRaisesRegex(ValueError, "lacks required Unity output"):
                    restore(evidence)

            self.assertEqual(output.read_bytes(), b"old settings")
            self.assertEqual(mode.read_text(), "prepare\n")

    def test_corrupt_later_member_is_rejected_before_any_file_is_written(self):
        package = io.BytesIO()
        with ZipFile(package, "w") as z:
            z.writestr("ProjectSettings/AudioManager.asset", b"valid")
            z.writestr("ProjectSettings/GraphicsSettings.asset", b"corrupt")
        package.seek(0)
        expected = {
            "ProjectSettings/AudioManager.asset": hashlib.sha256(b"valid").hexdigest(),
            "ProjectSettings/GraphicsSettings.asset": hashlib.sha256(b"different").hexdigest(),
        }
        with ZipFile(package) as archive:
            with self.assertRaisesRegex(ValueError, "Candidate file digest differs"):
                validate_candidate_contents(archive, expected)

    def test_missing_new_asset_meta_is_rejected_before_restore(self):
        package = io.BytesIO()
        name = "Assets/Game/Resources/Soundwell/Tiles/NewUntrackedTile.asset"
        with ZipFile(package, "w") as z:
            z.writestr(name, b"test")
        package.seek(0)
        with ZipFile(package) as archive:
            with self.assertRaisesRegex(ValueError, "Candidate asset lacks metadata"):
                validate_candidate_contents(
                    archive, {name: hashlib.sha256(b"test").hexdigest()})


class InstallerFolderTests(unittest.TestCase):
    def test_directory_meta_without_asset_is_created_and_install_is_idempotent(self):
        with tempfile.TemporaryDirectory() as root:
            destination = Path(root)
            folder = Path("Assets/Gaming Is Love/EmptyVendorFolder")
            files = {Path(str(folder) + ".meta"): b"folder meta", folder: None}
            with patch.object(install_ork, "PROJECT", destination):
                self.assertEqual(install_ork.install(files), 2)
                self.assertTrue((destination / folder).is_dir())
                self.assertEqual((destination / (str(folder) + ".meta")).read_bytes(),
                                 b"folder meta")
                self.assertEqual(install_ork.install(files), 0)



class VendorAssetStreamingTests(unittest.TestCase):
    def test_spooled_large_vendor_asset_installs_without_whole_payload_read(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            name = Path("Assets/Gaming Is Love/Test Packages/large.bin")
            with SpooledTemporaryFile(max_size=1024) as spool:
                original = b"ORK3-stream-test" * 120000
                spool.write(original)
                spool.seek(0)
                self.assertTrue(spool._rolled)
                with patch.object(install_ork, "PROJECT", root):
                    self.assertEqual(install_ork.install({name: spool}), 1)
                    self.assertEqual((root / name).stat().st_size, len(original))
                    self.assertEqual(install_ork.install({name: spool}), 0,
                                     "An identical vendor payload is idempotent.")
                    with (root / name).open("ab") as output:
                        output.write(b"corrupted")
                    with self.assertRaisesRegex(ValueError, "Existing asset differs"):
                        install_ork.install({name: spool})



class SoundwellRouteComparisonTests(unittest.TestCase):
    def test_both_routes_must_have_exactly_one_equal_fresh_start(self):
        bell = "RPG_SOUNDWELL_ROUTE_START:BELL:shared"
        combat = "RPG_SOUNDWELL_ROUTE_START:COMBAT:shared"
        self.assertEqual(validate_soundwell_route_starts(bell + "\n" + combat), [])
        for invalid in (bell, bell + "\n" + combat + "\n" + combat,
                        bell + "\n" + combat.replace("shared", "different")):
            self.assertTrue(validate_soundwell_route_starts(invalid))


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
            "build_report_bytes": 90000, "data_directory_present": True, "save_restart_passed": True,
            "environment": "Unity Linux Editor CI; real Player; not a user device",
        }

    def test_requires_real_build_and_global_scene_list(self):
        self.assertEqual(validate_build_profile_player(self.sample()), [])
        for key, changed in [
            ("executable_bytes", 0), ("target", 1), ("development_build", False),
            ("scenes", []), ("scripting_backend", "Other"), ("save_restart_passed", False),
        ]:
            with self.subTest(key=key):
                report = self.sample()
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
            "snapshots": [dict(self.sample(phase, scene), unity_frame=100 + 100 * index)
                          for index, (phase, scene) in enumerate(zip(MEMORY_PHASES, scenes))],
            "bootstrap_managed_gc_heap_delta_bytes": 0,
            "bootstrap_unity_total_allocated_delta_bytes": 0,
        }

    def test_real_metrics_can_be_compared_without_declaring_growth_a_leak(self):
        data = self.evidence()
        data["snapshots"][-1]["managed_gc_heap_bytes"] += 2048
        data["bootstrap_managed_gc_heap_delta_bytes"] = 2048
        self.assertEqual(validate_memory_phases(data), [])

    def test_missing_unreal_or_misordered_samples_are_rejected(self):
        for mutation in ("empty", "wrong_scene", "negative", "bool", "delta",
                         "duplicate_frame", "rewound_frame"):
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
                elif mutation == "duplicate_frame":
                    data["snapshots"][2]["unity_frame"] = data["snapshots"][1]["unity_frame"]
                elif mutation == "rewound_frame":
                    data["snapshots"][3]["unity_frame"] = 1
                self.assertTrue(validate_memory_phases(data), mutation)


class SoundwellAcceptanceContractTests(unittest.TestCase):
    def test_new_nunit_case_is_discovered_from_real_fixture_without_weakening_manifest(self):
        with tempfile.TemporaryDirectory() as root:
            source_root = Path(root)
            case = source_root / "Assets/Game/Tests/Editor/NovelGameplayTests.cs"
            case.parent.mkdir(parents=True)
            fixture = (
                "namespace Rpg.Tests.Editor {\n"
                "  public sealed class NovelGameplayTests {\n"
                "    [Test, Order(9)] public void ChecksFreshMovement() {}\n"
                "  }\n"
                "}\n"
            )
            case.write_text(fixture, encoding="utf-8")
            name = "Rpg.Tests.Editor.NovelGameplayTests.ChecksFreshMovement"
            self.assertIn(name, expected_tests_for("verify", True, source_root))
            self.assertNotIn(name, expected_tests_for("prepare", True, source_root))
            self.assertNotIn(name, expected_tests_for("verify", False, source_root))
            # A real source fixture is required; comments, method names
            # without NUnit attributes, and foreign namespaces are not tests.
            case.write_text(fixture.replace("[Test, Order(9)]", "// [Test, Order(9)]"),
                            encoding="utf-8")
            self.assertNotIn(name, expected_tests_for("verify", True, source_root))
            case.write_text(fixture.replace("Rpg.Tests.Editor", "Other.Tests"),
                            encoding="utf-8")
            self.assertNotIn(name, expected_tests_for("verify", True, source_root))
            case.write_text(fixture.replace("class NovelGameplayTests", "class OtherTests"),
                            encoding="utf-8")
            self.assertNotIn(name, expected_tests_for("verify", True, source_root))

    def test_duplicate_nunit_declarations_are_rejected_in_preflight(self):
        with tempfile.TemporaryDirectory() as root:
            source_root = Path(root)
            case = source_root / "Assets/Game/Tests/Editor/DuplicateTests.cs"
            case.parent.mkdir(parents=True)
            case.write_text(
                "namespace Rpg.Tests.Editor { public sealed class DuplicateTests {\n"
                "  [Test] public void Repeated() {}\n"
                "  [Test] public void Repeated() {}\n"
                "} }\n", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Duplicate NUnit test declaration"):
                expected_tests_for("verify", True, source_root)
            # Prepare deliberately only preflights P0BootstrapTests.
            self.assertNotIn(
                "Rpg.Tests.Editor.DuplicateTests.Repeated",
                expected_tests_for("prepare", True, source_root))

    def test_formation_stepping_translation_requires_real_nunit_case(self):
        with tempfile.TemporaryDirectory() as root:
            source_root = Path(root)
            case_file = source_root / "Assets/Game/Tests/Editor/SoundwellLocalizedRescueCopyTests.cs"
            case_file.parent.mkdir(parents=True)
            method = "FormationToggleExplainsSteppingInBothLanguages"
            full_name = "Rpg.Tests.Editor.SoundwellLocalizedRescueCopyTests." + method
            case_file.write_text("[Test] public void " + method + "() { }",
                                 encoding="utf-8")
            for mode in ("verify", "prepare"):
                self.assertIn(full_name, expected_tests_for(mode, True, source_root))
            case_file.write_text("public void " + method + "() { }",
                                 encoding="utf-8")
            self.assertNotIn(full_name, expected_tests_for("verify", True, source_root))

    def test_destroyed_ork_actor_lookup_is_required_only_with_real_nunit_case(self):
        with tempfile.TemporaryDirectory() as root:
            source_root = Path(root)
            case_file = source_root / "Assets/Game/Tests/Editor/SoundwellTrialTests.cs"
            case_file.parent.mkdir(parents=True)
            method = "BattleChooserActorLookupIgnoresDestroyedOrMissingActors"
            full_name = "Rpg.Tests.Editor.SoundwellTrialTests." + method
            case_file.write_text("[Test] public void " + method + "() { }",
                                 encoding="utf-8")
            self.assertIn(full_name, expected_tests_for("verify", True, source_root))
            self.assertIn(full_name, expected_tests_for("prepare", True, source_root))
            # No source method, no expected test: neither missing tests nor
            # arbitrary unexpected test names may pass the P0 acceptance set.
            case_file.write_text("public void " + method + "() { }",
                                 encoding="utf-8")
            self.assertNotIn(full_name, expected_tests_for("verify", True, source_root))

    def test_nunit_step_boundary_test_must_be_exactly_declared(self):
        with tempfile.TemporaryDirectory() as root:
            source_root = Path(root)
            test_file = source_root / "Assets/Game/Tests/Editor/SoundwellCompanionActionTests.cs"
            test_file.parent.mkdir(parents=True)
            method = "OrkStepMustKeepWholeCombatantInsideSavedBattleBounds"
            full_name = "Rpg.Tests.Editor.SoundwellCompanionActionTests." + method
            test_file.write_text(
                "[Test] public void " + method + "() { }", encoding="utf-8"
            )
            self.assertIn(full_name, expected_tests_for("verify", True, source_root))
            self.assertIn(full_name, expected_tests_for("prepare", True, source_root))
            # A source-level name alone must not be accepted without NUnit's
            # Test attribute; the collector still rejects unexpected results.
            test_file.write_text(
                "public void " + method + "() { }", encoding="utf-8"
            )
            self.assertNotIn(full_name, expected_tests_for("verify", True, source_root))

    def test_paused_leader_animation_test_is_required_in_soundwell_verify(self):
        with tempfile.TemporaryDirectory() as root:
            source_root = Path(root)
            companion_file = source_root / "Assets/Game/Tests/Editor/SoundwellCompanionActionTests.cs"
            companion_file.parent.mkdir(parents=True)
            method_name = "LeaderActionAndHitFeedbackExpireDuringPausedOrkClock"
            companion_file.write_text(
                f"[UnityTest] public IEnumerator {method_name}() {{ yield break; }}",
                encoding="utf-8",
            )
            full_name = f"Rpg.Tests.Editor.SoundwellCompanionActionTests.{method_name}"
            self.assertIn(full_name, expected_tests_for("verify", True, source_root))
            self.assertNotIn(full_name, expected_tests_for("prepare", True, source_root))

    def test_real_carried_bell_knockback_is_an_exact_candidate_unitytest(self):
        with tempfile.TemporaryDirectory() as root:
            source_root = Path(root)
            case_file = source_root / "Assets/Game/Tests/Editor/SoundwellTrialTests.cs"
            case_file.parent.mkdir(parents=True)
            method = "BeastKnockbackKeepsCarriedBellWithRescuerOnContactFrame"
            case_file.write_text(
                "[UnityTest] public IEnumerator " + method +
                "() { yield break; }", encoding="utf-8",
            )
            full_name = "Rpg.Tests.Editor.SoundwellTrialTests." + method
            self.assertIn(full_name, expected_tests_for("verify", True, source_root))
            self.assertNotIn(full_name, expected_tests_for("prepare", True, source_root))
            # Removing the real UnityTest attribute cannot satisfy the
            # manifest merely by leaving the method name in source text.
            case_file.write_text(
                "public IEnumerator " + method + "() { yield break; }",
                encoding="utf-8",
            )
            self.assertNotIn(full_name, expected_tests_for("verify", True, source_root))

    def test_paused_escort_retry_is_required_without_loosening_p0_manifest(self):
        with tempfile.TemporaryDirectory() as root:
            source_root = Path(root)
            case_file = source_root / "Assets/Game/Tests/Editor/SoundwellTrialTests.cs"
            case_file.parent.mkdir(parents=True)
            method = "PausedEscortRetryClearsWaitingVisualBeforeNextFrame"
            name = "Rpg.Tests.Editor.SoundwellTrialTests." + method
            case_file.write_text(
                "[UnityTest] public IEnumerator " + method +
                "() { yield break; }", encoding="utf-8",
            )
            self.assertIn(name, expected_tests_for("verify", True, source_root))
            self.assertNotIn(name, expected_tests_for("prepare", True, source_root))
            case_file.write_text(
                "public IEnumerator " + method + "() { yield break; }",
                encoding="utf-8",
            )
            self.assertNotIn(name, expected_tests_for("verify", True, source_root))

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
        with tempfile.TemporaryDirectory() as root:
            source_root = Path(root)
            # Test manifest discovery must be isolated from the actual
            # checked-out candidate. The live M2 NUnit test is intentional.
            self.assertEqual(
                expected_tests_for("prepare", True, source_root=source_root),
                EXPECTED_TESTS,
            )
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

            localized_file = source_root / "Assets/Game/Tests/Editor/SoundwellTrialTests.cs"
            localized_file.write_text(
                "[UnityTest] public IEnumerator "
                "ReturnScreenKeepsRescueOutcomeLocalizedAcrossLanguageChanges()"
                " { yield break; }", encoding="utf-8"
            )
            self.assertEqual(len(expected_tests_for("verify", True, source_root=source_root)), 11)

            audio_file = source_root / "Assets/Game/Tests/Editor/P0BootstrapTests.cs"
            audio_file.write_text(
                "[UnityTest, Order(30)] public IEnumerator "
                "SoundwellAudioMuteSurvivesSceneRerouting() { yield break; }",
                encoding="utf-8"
            )
            self.assertEqual(len(expected_tests_for("verify", True, source_root=source_root)), 12)

            input_file = source_root / "Assets/Game/Tests/Editor/SoundwellInputSystemTests.cs"
            input_file.write_text(
                "[UnityTest] public IEnumerator "
                "GamepadSubmitOnFocusedEntrySfxButtonDoesNotStartTheTrial()"
                " { yield break; }", encoding="utf-8"
            )
            expected = expected_tests_for("verify", True, source_root=source_root)
            self.assertEqual(len(expected), 13)
            self.assertIn(
                "Rpg.Tests.Editor.SoundwellInputSystemTests.GamepadSubmitOnFocusedEntrySfxButtonDoesNotStartTheTrial",
                expected,
            )
            # An independent Story 003 test also becomes a required real
            # UnityTest when its source file exists in the final candidate.
            threat_file = source_root / "Assets/Game/Tests/Editor/SoundwellThreatPreviewTests.cs"
            threat_file.write_text(
                "[UnityTest] public IEnumerator "
                "SideBellLureDisplaysRealPursuitAndExpiresAfterWrapping()"
                " { yield break; }", encoding="utf-8"
            )
            expected = expected_tests_for("verify", True, source_root=source_root)
            self.assertEqual(len(expected), 14)
            self.assertIn(
                "Rpg.Tests.Editor.SoundwellThreatPreviewTests.SideBellLureDisplaysRealPursuitAndExpiresAfterWrapping",
                expected,
            )
            # Newly declared M9 Play Mode tests must be required by the
            # same-SHA collector; 15/15 Unity success cannot be rejected by
            # an outdated expected-test registry.
            localized_file.write_text(
                localized_file.read_text(encoding="utf-8") +
                "\n[UnityTest] public IEnumerator "
                "PlayerLanguageChoiceSurvivesFreshPlayAndUpdatesEntryUi()"
                " { yield break; }", encoding="utf-8"
            )
            expected = expected_tests_for("verify", True, source_root=source_root)
            self.assertEqual(len(expected), 15)
            self.assertIn(
                "Rpg.Tests.Editor.SoundwellTrialTests.PlayerLanguageChoiceSurvivesFreshPlayAndUpdatesEntryUi",
                expected,
            )
            self.assertEqual(expected_tests_for("prepare", True, source_root=source_root), EXPECTED_TESTS)
            # A newly declared bell-only rescue must be included in the exact
            # same-SHA acceptance set, not treated as an unexpected extra test.
            localized_file.write_text(
                localized_file.read_text(encoding="utf-8") +
                "\n[UnityTest] public IEnumerator "
                "BellOnlyRescueWorksWithoutOptionalCluesOrEscortRope()"
                " { yield break; }", encoding="utf-8"
            )
            with_bell = expected_tests_for("verify", True, source_root=source_root)
            self.assertEqual(len(with_bell), 16)
            self.assertIn(
                "Rpg.Tests.Editor.SoundwellTrialTests.BellOnlyRescueWorksWithoutOptionalCluesOrEscortRope",
                with_bell,
            )
            self.assertEqual(expected_tests_for("prepare", True, source_root=source_root), EXPECTED_TESTS)
            # An ORK attack-animation UnityTest must also be part of the
            # strict same-SHA manifest; actual Unity success alone is insufficient.
            localized_file.write_text(
                localized_file.read_text(encoding="utf-8") +
                "\n[UnityTest] public IEnumerator "
                "AcceptedOrkLeaderAttackShowsActualPlayerActionClip()"
                " { yield break; }", encoding="utf-8",
            )
            expected_attack = expected_tests_for("verify", True, source_root=source_root)
            self.assertEqual(len(expected_attack), 17)
            self.assertIn(
                "Rpg.Tests.Editor.SoundwellTrialTests.AcceptedOrkLeaderAttackShowsActualPlayerActionClip",
                expected_attack,
            )

            # Both actor animation regressions must be independently present
            # in the exact manifest for the combined attack-visual build.
            companion_file = source_root / "Assets/Game/Tests/Editor/SoundwellCompanionActionTests.cs"
            companion_file.write_text(
                "[UnityTest] public IEnumerator "
                "OrkAcceptedCompanionAttackChangesOnlyItsPixelPose()"
                " { yield break; }", encoding="utf-8",
            )
            combined = expected_tests_for("verify", True, source_root=source_root)
            self.assertEqual(len(combined), 18)
            self.assertIn(
                "Rpg.Tests.Editor.SoundwellCompanionActionTests.OrkAcceptedCompanionAttackChangesOnlyItsPixelPose",
                combined,
            )
            self.assertIn(
                "Rpg.Tests.Editor.SoundwellTrialTests.AcceptedOrkLeaderAttackShowsActualPlayerActionClip",
                combined,
            )

            # New S00 gamepad coverage must be recognized only when its
            # actual UnityTest method exists in the checked-out candidate.
            input_file.write_text(
                input_file.read_text(encoding="utf-8") +
                "\n[UnityTest] public IEnumerator "
                "WhitewaterCableRepairRequiresRealGamepadStickAndFreshSouthPress()"
                " { yield break; }", encoding="utf-8",
            )
            s00_pad = (
                "Rpg.Tests.Editor.SoundwellInputSystemTests."
                "WhitewaterCableRepairRequiresRealGamepadStickAndFreshSouthPress"
            )
            self.assertIn(s00_pad, expected_tests_for("verify", True, source_root))
            self.assertNotIn(s00_pad, expected_tests_for("prepare", True, source_root))
            self.assertEqual(len(expected_tests_for("verify", True, source_root)), 19)
            input_file.write_text(
                input_file.read_text(encoding="utf-8").replace(
                    "[UnityTest] public IEnumerator "
                    "WhitewaterCableRepairRequiresRealGamepadStickAndFreshSouthPress",
                    "public IEnumerator "
                    "WhitewaterCableRepairRequiresRealGamepadStickAndFreshSouthPress"
                ),
                encoding="utf-8",
            )
            self.assertNotIn(s00_pad, expected_tests_for("verify", True, source_root))

            # A new regular NUnit [Test] must count toward the exact
            # Unity acceptance set, including its prepare-mode subset.
            bootstrap = source_root / "Assets/Game/Tests/Editor/P0BootstrapTests.cs"
            bootstrap.write_text(
                bootstrap.read_text(encoding="utf-8") +
                "\n[Test, Order(8)] public void "
                "InvalidSavedBindingOverridesRecoverDefaultInputWithoutLosingValidRemaps() {}",
                encoding="utf-8",
            )
            added_test = (
                "Rpg.Tests.Editor.P0BootstrapTests."
                "InvalidSavedBindingOverridesRecoverDefaultInputWithoutLosingValidRemaps"
            )
            self.assertIn(added_test, expected_tests_for("verify", True, source_root))
            self.assertIn(added_test, expected_tests_for("prepare", True, source_root))
            bootstrap.write_text(
                bootstrap.read_text(encoding="utf-8").replace(
                    "[Test, Order(8)]", "// deleted Test attribute"
                ),
                encoding="utf-8",
            )
            self.assertNotIn(added_test, expected_tests_for("verify", True, source_root))


class PendingBranchCleanupWorkflowTests(unittest.TestCase):
    def test_reviewed_legacy_retirements_keep_exact_sha_evidence_and_valid_python(self):
        # The cleanup workflow is a YAML-embedded Python program. Compiler-
        # check its exact checked-out body before a green Unity verification
        # may deliver a replacement for any of these diverged branches.
        import textwrap
        source = (Path(__file__).resolve().parents[1] /
                  ".github/workflows/close-merged-pending.yml").read_text(encoding="utf-8")
        opening = "          python3 - <<'PY'\n"
        closing = "\n          PY"
        self.assertEqual(source.count(opening), 1)
        body = source.split(opening, 1)[1].split(closing, 1)[0]
        script = textwrap.dedent(body)
        compile(script, "close-merged-pending.yml:python", "exec")
        # Besides being syntactically valid, the original-run check must
        # execute inside the per-branch proof, never at module scope.
        import ast
        module = ast.parse(script)
        proof = next((node for node in module.body
                      if isinstance(node, ast.FunctionDef)
                      and node.name == "verified_archived_proof"), None)
        self.assertIsNotNone(proof)
        self.assertIn('if entry.get("original_run"):',
                      ast.get_source_segment(script, proof))

        expected = {
            "codex/pending/story-003-ork-attack-choice":
                ("6f546b89d4a2bc59688e82bb2451adf13c24290a", 43125796222),
            "codex/pending/s00-escort-separation-visual-regroup-nov13":
                ("e3cfeb109d8dece1db29ee400c055100856f5894", 43671553145),
            "codex/pending/story003-escort-checkpoint-follower-resync-nov14":
                ("878e2dbd8976dd840c9855229bda984e92ba5f57", 43619755853),
            "codex/pending/story003-fixed-full-rescue-evidence-nov14":
                ("4ca0a38d83a9de80b7b1bd9eaf6e32796eeb9834", 43590517380),
        }
        for name, (head, original_run) in expected.items():
            with self.subTest(name=name):
                start = script.index('"' + name + '": {')
                receipt = script[start:start + 450]
                self.assertIn('"head": "' + head + '"', receipt)
                self.assertIn('"verified": "84f7ff5a255b56c1099daa7421f7e22e8546ed50"', receipt)
                self.assertIn('"run": 43757378497', receipt)
                self.assertIn('"original_run": ' + str(original_run), receipt)
                self.assertIn('"pin_verified_snapshot": True', receipt)
        # Never accept a green replacement without confirming its original
        # SHA and artifact; this is stronger than the generic snapshot rule.
        self.assertIn('original.get("head_sha")==sha', script)
        self.assertIn('"original candidate exact-SHA artifact missing"', script)


    def test_cleanup_refuses_queued_or_running_branch_ci(self):
        # Execute only the cleanup workflow's exact runs_for function with
        # an in-memory API stub: no network, branch deletion or workflow edits.
        # A string-only assertion would pass even if its safety check were
        # accidentally moved outside the function or stopped executing.
        import ast
        import textwrap
        import urllib.parse

        workflow = (Path(__file__).resolve().parents[1] /
                    ".github/workflows/close-merged-pending.yml").read_text(
                        encoding="utf-8")
        opening = "          python3 - <<'PY'\n"
        closing = "\n          PY"
        self.assertEqual(workflow.count(opening), 1)
        script = textwrap.dedent(workflow.split(opening, 1)[1].split(closing, 1)[0])
        tree = ast.parse(script)
        function = next((node for node in tree.body
                         if isinstance(node, ast.FunctionDef)
                         and node.name == "runs_for"), None)
        self.assertIsNotNone(function, "The cleanup must keep a per-branch CI gate.")

        isolated = ast.fix_missing_locations(
            ast.Module(body=[function], type_ignores=[]))
        def require(ok, reason):
            if not ok:
                raise RuntimeError(reason)

        for status in ("queued", "in_progress", "waiting", "requested"):
            with self.subTest(status=status):
                calls = []
                def pages(path, key):
                    calls.append((path, key))
                    return [{"status": "completed"}, {"status": status}]
                scope = {"REPO": "jinze909/rpg", "urllib": urllib,
                         "pages": pages, "check": require}
                exec(compile(isolated, "cleanup:runs_for", "exec"), scope)
                with self.assertRaisesRegex(RuntimeError, "active or queued CI"):
                    scope["runs_for"]("codex/pending/r5-ci-safety")
                self.assertEqual(len(calls), 1)
                self.assertEqual(calls[0][1], "workflow_runs")
                self.assertIn("branch=codex%2Fpending%2Fr5-ci-safety", calls[0][0])

        # No active job must leave the branch eligible for the remaining
        # exact-SHA / artifact checks; it must not make the branch deletable.
        for statuses in ([], [{"status": "completed"}]):
            def pages(path, key):
                return statuses
            scope = {"REPO": "jinze909/rpg", "urllib": urllib,
                     "pages": pages, "check": require}
            exec(compile(isolated, "cleanup:runs_for", "exec"), scope)
            self.assertEqual(scope["runs_for"]("codex/pending/r5-ci-safety"),
                             statuses)


class UnityTestManifestPreflightTests(unittest.TestCase):
    def test_live_editor_unitytests_are_registered_before_unity_starts(self):
        root = Path(__file__).resolve().parents[1]
        unregistered = unregistered_unitytest_methods(root, "verify", True)
        self.assertEqual(unregistered, set(),
                         "Register every new UnityTest in the exact P0 manifest.")

    def test_missing_playmode_registration_is_detected_but_never_auto_accepted(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            editor = root / "Assets/Game/Tests/Editor"
            editor.mkdir(parents=True)
            fixture = editor / "SoundwellTrialTests.cs"
            fixture.write_text(
                "namespace Rpg.Tests.Editor { class SoundwellTrialTests {\n"
                "[UnityTest] public IEnumerator PlayerCanRingWrapRetreatAndRetry() { yield break; }\n"
                "[UnityTest, Order(77)] public IEnumerator ForgottenBridgeDefence() { yield break; }\n"
                "public IEnumerator NotAUnityTest() { yield break; }\n"
                "}}", encoding="utf-8",
            )
            missing = "Rpg.Tests.Editor.SoundwellTrialTests.ForgottenBridgeDefence"
            self.assertEqual(unregistered_unitytest_methods(root, "verify", True),
                             {missing})
            self.assertNotIn(missing, expected_tests_for("verify", True, root))
            self.assertEqual(unregistered_unitytest_methods(root, "prepare", True), set())
            self.assertEqual(unregistered_unitytest_methods(root, "verify", False), set())

            # Bootstrap tests also run in prepare mode, and must never be
            # exempted from a missing UnityTest manifest registration.
            fixture.unlink()
            (editor / "P0BootstrapTests.cs").write_text(
                "namespace Rpg.Tests.Editor { class P0BootstrapTests {\n"
                "[UnityTest] public IEnumerator FirstPlayInitializesFramework() { yield break; }\n"
                "[UnityTest] public IEnumerator UnregisteredBootstrapCase() { yield break; }\n"
                "}}", encoding="utf-8",
            )
            bootstrap_missing = (
                "Rpg.Tests.Editor.P0BootstrapTests.UnregisteredBootstrapCase"
            )
            self.assertEqual(unregistered_unitytest_methods(root, "prepare", True),
                             {bootstrap_missing})


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



class NarrativeV12WritingBaselineTests(unittest.TestCase):
    """Checks the authoritative v1.2 writing, never substitutes for Unity gameplay tests."""

    @staticmethod
    def docs():
        return Path(__file__).resolve().parents[1] / "docs/narrative"

    def test_all_29_v12_chapter_routes_retain_sources_and_outline(self):
        root = self.docs()
        outline = (root / "STORY_OUTLINE.md").read_text(encoding="utf-8")
        handoff = (root / "HANDOFF_FOR_CODEX.md").read_text(encoding="utf-8")
        routes = (["P0"] + [f"M{i:02d}" for i in range(17)] +
                  [f"RC{i:02d}" for i in range(1, 6)] +
                  [f"ST{i:02d}" for i in range(1, 7)])
        self.assertEqual(len(routes), 29)
        self.assertIn("29 份", handoff)
        self.assertIn("不是已实现内容", handoff)
        self.assertIn("不是已验收证据", handoff)
        for chapter in routes:
            filename = f"SCENE_SCRIPT_{chapter}.md"
            with self.subTest(chapter=chapter):
                self.assertTrue((root / filename).is_file(), filename)
                self.assertIn(f"({filename})", outline)

    def test_survivor_identity_and_st06_family_match_v12_sources(self):
        root = self.docs()
        characters = (root / "CHARACTERS.md").read_text(encoding="utf-8")
        facts = (root / "NARRATIVE_FACTS.md").read_text(encoding="utf-8")
        regional = (root / "REGIONAL_STORIES.md").read_text(encoding="utf-8")
        st06 = (root / "SCENE_SCRIPT_ST06.md").read_text(encoding="utf-8")
        self.assertIn("蕊亚（欧林之妹", characters)
        self.assertIn("survivor_ruiya", facts)
        self.assertIn("st_states", facts)
        self.assertIn("survivor_taoji", facts)
        for document in (characters, regional, st06):
            self.assertIn("陶霁", document)
        for document in (regional, st06):
            self.assertIn("陶小满", document)
        self.assertIn("ST-06", regional)

    def test_m00_choice_and_m02_independent_evidence_are_preserved(self):
        root = self.docs()
        m00 = (root / "SCENE_SCRIPT_M00.md").read_text(encoding="utf-8")
        m02 = (root / "SCENE_SCRIPT_M02.md").read_text(encoding="utf-8")
        facts = (root / "NARRATIVE_FACTS.md").read_text(encoding="utf-8")
        for choice in ("[应声：“在。”]", "[离开人群]"):
            self.assertIn(choice, m00)
        self.assertEqual(m00.count("[应声：“在。”]"), 3)
        self.assertEqual(m00.count("[离开人群]"), 3)
        self.assertIn("identity_reveal", m00)
        self.assertIn("四十一个", m00)
        for clue in ("缆口整齐", "桥夫旧记录", "战报底稿"):
            with self.subTest(clue=clue):
                self.assertIn(clue, m02)
                self.assertIn(clue, facts)
        self.assertIn("守桥老人", m02)
        self.assertIn("战报底稿", m02)

    def test_m03_m04_seeding_and_rc04_fog_stay_distinct(self):
        root = self.docs()
        m02 = (root / "SCENE_SCRIPT_M02.md").read_text(encoding="utf-8")
        m03 = (root / "SCENE_SCRIPT_M03.md").read_text(encoding="utf-8")
        m04 = (root / "SCENE_SCRIPT_M04.md").read_text(encoding="utf-8")
        regional = (root / "REGIONAL_STORIES.md").read_text(encoding="utf-8")
        cases = (root / "ROSTER_CASES.md").read_text(encoding="utf-8")
        facts = (root / "NARRATIVE_FACTS.md").read_text(encoding="utf-8")
        self.assertIn("伊蕾", m02)
        self.assertIn("陆钧", m03)
        self.assertIn("lu_confession", m03)
        self.assertIn("归属", m04)
        self.assertIn("ST-04", regional)
        self.assertIn("RC-04", cases)
        self.assertIn("听雾", cases)
        self.assertIn("rc04_ding_yue", facts)
        self.assertIn("听雾", facts)


    def test_v12_m00_prompt_and_rollcall_are_independent(self):
        root = self.docs()
        m00 = (root / "SCENE_SCRIPT_M00.md").read_text(encoding="utf-8")
        m01 = (root / "SCENE_SCRIPT_M01.md").read_text(encoding="utf-8")
        facts = (root / "NARRATIVE_FACTS.md").read_text(encoding="utf-8")
        for doc in (m00, m01, facts):
            self.assertIn("identity_reveal", doc)
            self.assertIn("xiaodou_prompted", doc)
        self.assertIn("若 M00 没有应声，但曾帮小豆提词", m01)
        self.assertIn("不能因提过词就改写", m01)

    def test_v12_optional_rescues_and_finale_do_not_override_player_choices(self):
        root = self.docs()
        m09 = (root / "SCENE_SCRIPT_M09.md").read_text(encoding="utf-8")
        m15 = (root / "SCENE_SCRIPT_M15.md").read_text(encoding="utf-8")
        m16 = (root / "SCENE_SCRIPT_M16.md").read_text(encoding="utf-8")
        rc03 = (root / "SCENE_SCRIPT_RC03.md").read_text(encoding="utf-8")
        for outcome in ("survivor_ruiya = 接出", "survivor_ruiya = 延后",
                        "survivor_ruiya = 未接"):
            self.assertIn(outcome, m09)
        self.assertIn("默认十八个铺位、二十三个待安排的名字", m09)
        self.assertIn("实际到场", m15)
        self.assertIn("不触发强制会面", m16)
        self.assertIn("不能算“改会章”已成功", rc03)

    def test_v12_writing_boundary_remains_distinct_from_playable_s00(self):
        root = self.docs()
        baseline = (root.parent / "gameplay/BASELINE.md").read_text(encoding="utf-8")
        handoff = (root / "HANDOFF_FOR_CODEX.md").read_text(encoding="utf-8")
        self.assertIn("v1.2", baseline)
        self.assertIn("唯一剧情写作权威", baseline)
        self.assertIn("P0 的基础应声选项", baseline)
        self.assertIn("独立原型部分实现", baseline)
        self.assertIn("整章剧情", baseline)
        self.assertIn("未验收", baseline)
        self.assertIn("M00 三遍点名", baseline)
        self.assertIn("不是已实现内容", handoff)

    def test_v12_document_links_resolve_in_place_without_old_combined_scripts(self):
        root = self.docs()
        project = root.parents[1]
        # v1.2 also retires the withdrawn v1.0-only extras. Excluding
        # them from the count used to hide accidental reintroductions.
        historical = ("RPG_PROGRESSION_AND_REWARDS.md",
                      "WORLD_FLAVOR_AND_SLANG.md")
        for obsolete_name in historical:
            with self.subTest(obsolete=obsolete_name):
                self.assertFalse((root / obsolete_name).exists(),
                                 f"Retired v1.0 draft has returned: {obsolete_name}")
        sources = sorted(root.glob("*.md"))
        self.assertEqual(len(sources), 39)
        self.assertIn("v1.2", (root / "STORY_OUTLINE.md").read_text(encoding="utf-8"))
        obsolete = ("SCENE_SCRIPT_P0_M00.md", "SCENE_SCRIPT_M03_M12.md")
        for doc in sources:
            content = doc.read_text(encoding="utf-8")
            for old_name in obsolete:
                with self.subTest(document=doc.name, old_name=old_name):
                    self.assertNotIn(old_name, content)
            for target in re.findall(r'(?<!!)\[[^\]]*\]\(([^)]+)\)', content):
                path = target.split("#", 1)[0].strip()
                if not path or path.startswith(("http://", "https://", "mailto:")):
                    continue
                destination = (doc.parent / path).resolve()
                with self.subTest(document=doc.name, link=target):
                    self.assertTrue(destination.is_relative_to(project),
                                    f"Link escapes repository: {doc.name}: {target}")
                    self.assertTrue(destination.is_file(),
                                    f"Missing relative link: {doc.name}: {target}")



if __name__ == "__main__":
    unittest.main()
